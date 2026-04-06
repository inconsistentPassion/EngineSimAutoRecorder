using System;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Simulates car cabin acoustics by chaining:
    ///   1. Structure-borne rumble boost   (60–90 Hz peaking EQ)
    ///   2. Cabin resonance boost #1        (180 Hz peaking EQ)
    ///   3. Cabin resonance boost #2        (350 Hz peaking EQ)
    ///   4. Low-pass filter                 (1.5–3 kHz, car-dependent)
    ///   5. Transient smoothing / soft compression
    ///   6. Narrow stereo                   (mid-side width 20–40%)
    ///   7. Tiny cabin reverb               (short comb, ~30 ms, 5–10% mix)
    /// </summary>
    public sealed class InteriorProcessor
    {
        private readonly NAudio.Dsp.BiQuadFilter _lpf;
        private readonly NAudio.Dsp.BiQuadFilter _res180;
        private readonly NAudio.Dsp.BiQuadFilter _res350;
        private readonly NAudio.Dsp.BiQuadFilter _rumble;
        private readonly float _stereoWidth;

        // Compressor state (per-channel)
        private float _envL, _envR;
        private readonly float _compThreshold;  // linear
        private readonly float _compRatio;
        private readonly float _compAttack;
        private readonly float _compRelease;

        // Simple reverb (comb filter)
        private readonly float[] _reverbBufL;
        private readonly float[] _reverbBufR;
        private int _reverbPos;
        private readonly float _reverbFeedback;
        private readonly float _reverbMix;

        private readonly int _channels;

        /// <param name="sampleRate">Recording sample rate (44100 or 48000)</param>
        /// <param name="channels">1 = mono, 2 = stereo</param>
        /// <param name="cutoffHz">Low-pass cutoff: 1000–4000</param>
        /// <param name="width">Stereo width: 0.1 (narrow) to 1.0 (original)</param>
        /// <param name="rumbleHz">Rumble boost center freq</param>
        /// <param name="rumbleDb">Rumble boost gain in dB</param>
        /// <param name="res1Hz">Resonance 1 center freq</param>
        /// <param name="res1Db">Resonance 1 gain in dB</param>
        /// <param name="res2Hz">Resonance 2 center freq</param>
        /// <param name="res2Db">Resonance 2 gain in dB</param>
        /// <param name="reverbMs">Reverb delay in ms</param>
        /// <param name="reverbMix">Reverb wet mix 0–1</param>
        /// <param name="compRatio">Compressor ratio</param>
        /// <param name="compThreshDb">Compressor threshold in dB</param>
        public InteriorProcessor(int sampleRate, int channels,
            float cutoffHz = 2000f, float width = 0.3f,
            float rumbleHz = 80f, float rumbleDb = 6f,
            float res1Hz = 180f, float res1Db = 5f,
            float res2Hz = 350f, float res2Db = 4f,
            float reverbMs = 30f, float reverbMix = 0.07f,
            float compRatio = 3f, float compThreshDb = -12f)
        {
            _channels = channels;
            _stereoWidth = width;

            // 1. Structure-borne rumble
            _rumble = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, rumbleHz, 1.2f, rumbleDb);

            // 2. Cabin resonance — boost at res1 Hz
            _res180 = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, res1Hz, 1.5f, res1Db);

            // 3. Cabin resonance — boost at res2 Hz
            _res350 = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, res2Hz, 1.8f, res2Db);

            // 4. Low-pass
            _lpf = NAudio.Dsp.BiQuadFilter.LowPassFilter(sampleRate, cutoffHz, 0.707f);

            // 5. Compressor settings
            _compThreshold = DbToLinear(compThreshDb);
            _compRatio = compRatio;
            _compAttack = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.015));  // ~15 ms
            _compRelease = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.080)); // ~80 ms
            _envL = 0f;
            _envR = 0f;

            // 7. Reverb — comb filter
            int delaySamples = Math.Max(1, (int)(sampleRate * reverbMs / 1000f));
            _reverbBufL = new float[delaySamples];
            _reverbBufR = new float[delaySamples];
            _reverbPos = 0;
            _reverbFeedback = 0.25f;
            _reverbMix = reverbMix;
        }

        /// <summary>
        /// Process interleaved float32 samples in-place.
        /// </summary>
        public void Process(float[] samples, int count)
        {
            if (_channels >= 2)
            {
                // Interleaved stereo: L R L R ...
                for (int i = 0; i < count - 1; i += 2)
                {
                    float L = samples[i];
                    float R = samples[i + 1];

                    // EQ chain on each channel
                    L = _rumble.Transform(L);
                    L = _res180.Transform(L);
                    L = _res350.Transform(L);
                    L = _lpf.Transform(L);

                    R = _rumble.Transform(R);
                    R = _res180.Transform(R);
                    R = _res350.Transform(R);
                    R = _lpf.Transform(R);

                    // Compressor
                    L = Compress(L, ref _envL);
                    R = Compress(R, ref _envR);

                    // Stereo narrowing via mid-side
                    float mid = (L + R) * 0.5f;
                    float side = (L - R) * 0.5f;
                    side *= _stereoWidth;
                    L = mid + side;
                    R = mid - side;

                    // Reverb (comb)
                    float wetL = _reverbBufL[_reverbPos];
                    float wetR = _reverbBufR[_reverbPos];
                    _reverbBufL[_reverbPos] = L + wetL * _reverbFeedback;
                    _reverbBufR[_reverbPos] = R + wetR * _reverbFeedback;
                    _reverbPos = (_reverbPos + 1) % _reverbBufL.Length;

                    samples[i] = L + wetL * _reverbMix;
                    samples[i + 1] = R + wetR * _reverbMix;
                }
            }
            else
            {
                // Mono — skip stereo/reverb steps
                for (int i = 0; i < count; i++)
                {
                    float s = samples[i];
                    s = _rumble.Transform(s);
                    s = _res180.Transform(s);
                    s = _res350.Transform(s);
                    s = _lpf.Transform(s);
                    s = Compress(s, ref _envL);
                    samples[i] = s;
                }
            }
        }

        private static float Compress(float input, ref float envelope)
        {
            float abs = Math.Abs(input);
            // Peak detector with asymmetric attack/release
            if (abs > envelope)
                envelope = abs; // instant attack (clamped by _compAttack below)
            else
                envelope = envelope * 0.999f + abs * 0.001f; // smooth release

            float env = envelope;
            if (env > _compThreshold)
            {
                float overDb = 20f * (float)Math.Log10(env / _compThreshold);
                float gainDb = overDb * (1f / _compRatio - 1f); // negative gain
                float gainLinear = DbToLinear(gainDb);
                input *= gainLinear;
            }
            return input;
        }

        private static float DbToLinear(float db) => (float)Math.Pow(10.0, db / 20.0);

        /// <summary>
        /// Presets for common car types.
        /// Returns (cutoffHz, stereoWidth).
        /// </summary>
        public static (float cutoff, float width) GetPreset(string carType) => carType switch
        {
            "Sedan"      => (2200f, 0.30f),
            "Coupe"      => (2000f, 0.25f),
            "SUV"        => (2500f, 0.35f),
            "Hatchback"  => (2000f, 0.25f),
            "Supercar"   => (1800f, 0.20f),
            "Truck"      => (2800f, 0.40f),
            _            => (2000f, 0.30f),
        };
    }
}
