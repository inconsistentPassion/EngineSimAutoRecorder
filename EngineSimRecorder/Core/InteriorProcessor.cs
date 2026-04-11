using System;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Simulates car cabin acoustics by chaining:
    ///   1. DC blocker                        (20 Hz HPF — kills DC offset)
    ///   2. Structure-borne rumble boost       (60–90 Hz peaking EQ, RPM-aware)
    ///   3. Cabin resonance boost #1           (180 Hz peaking EQ)
    ///   4. Cabin resonance boost #2           (350 Hz peaking EQ)
    ///   5. Low-pass filter                    (1.5–3 kHz, car-dependent, RPM-aware)
    ///   6. Character band boost               (engine "voice" through cabin, 1.5-2 kHz)
    ///   7. Subtle saturation                  (warm cabin harmonics)
    ///   8. Compressor                         (smooth dynamics)
    ///   9. Narrow stereo                      (mid-side width 20–40%)
    ///  10. Tiny cabin reverb                  (short comb, ~30 ms, 5–10% mix)
    /// </summary>
    public sealed class InteriorProcessor
    {
        private readonly NAudio.Dsp.BiQuadFilter _dcBlocker;
        private readonly NAudio.Dsp.BiQuadFilter _lpf;
        private readonly NAudio.Dsp.BiQuadFilter _res180;
        private readonly NAudio.Dsp.BiQuadFilter _res350;
        private readonly NAudio.Dsp.BiQuadFilter _rumble;
        private NAudio.Dsp.BiQuadFilter _character; // nullable — disabled if Hz = 0
        private readonly float _stereoWidth;

        // ── Subtle saturation ─────────────────────────────────────────────
        private readonly float _satDrive;
        private readonly float _satMix;
        private float _satPrevInL, _satPrevOutL;
        private float _satPrevInR, _satPrevOutR;

        // Compressor state (per-channel)
        private float _envL, _envR;
        private readonly float _compThreshold;
        private readonly float _compRatio;
        private readonly float _compAttackCoeff;
        private readonly float _compReleaseCoeff;

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
        /// <param name="characterHz">Character band center Hz (0 = disabled)</param>
        /// <param name="characterDb">Character band boost dB</param>
        /// <param name="satDrive">Subtle saturation drive (1.0 = none)</param>
        /// <param name="targetRpm">Target RPM for this recording (0 = RPM-aware disabled)</param>
        /// <param name="rpm">RPM scaling settings (null = disabled)</param>
        public InteriorProcessor(int sampleRate, int channels,
            float cutoffHz = 2000f, float width = 0.3f,
            float rumbleHz = 80f, float rumbleDb = 6f,
            float res1Hz = 180f, float res1Db = 5f,
            float res2Hz = 350f, float res2Db = 4f,
            float reverbMs = 30f, float reverbMix = 0.07f,
            float compRatio = 3f, float compThreshDb = -12f,
            float characterHz = 1800f, float characterDb = 2f,
            float satDrive = 1.15f,
            int targetRpm = 0, RpmProcessingSettings? rpm = null)
        {
            _channels = channels;
            _stereoWidth = width;

            // ── Apply RPM scaling if enabled ──────────────────────────────
            float effectiveCutoffHz = cutoffHz;
            float effectiveRumbleDb = rumbleDb;
            float effectiveCompThreshDb = compThreshDb;

            if (rpm != null && rpm.Enabled && targetRpm > 0)
            {
                effectiveCutoffHz = rpm.Lerp(rpm.LpfCutoffRange, targetRpm);
                effectiveRumbleDb = rpm.Lerp(rpm.RumbleBoostRange, targetRpm);
                effectiveCompThreshDb = rpm.Lerp(rpm.CompThreshRange, targetRpm);
            }

            // 0. DC blocker
            _dcBlocker = NAudio.Dsp.BiQuadFilter.HighPassFilter(sampleRate, 20f, 0.707f);

            // 1. Structure-borne rumble
            _rumble = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, rumbleHz, 1.2f, effectiveRumbleDb);

            // 2. Cabin resonance — boost at res1 Hz
            _res180 = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, res1Hz, 1.5f, res1Db);

            // 3. Cabin resonance — boost at res2 Hz
            _res350 = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, res2Hz, 1.8f, res2Db);

            // 4. Low-pass
            _lpf = NAudio.Dsp.BiQuadFilter.LowPassFilter(sampleRate, effectiveCutoffHz, 0.707f);

            // 5. Character band — engine "voice" through cabin
            if (characterHz > 0f && Math.Abs(characterDb) > 0.1f)
                _character = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, characterHz, 1.0f, characterDb);

            // 6. Subtle saturation
            _satDrive = satDrive;
            _satMix = satDrive > 1.01f ? 0.25f : 0f; // 25% wet if enabled
            _satPrevInL = 0f; _satPrevOutL = 0f;
            _satPrevInR = 0f; _satPrevOutR = 0f;

            // 7. Compressor
            _compThreshold = DbToLinear(effectiveCompThreshDb);
            _compRatio = compRatio;
            _compAttackCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.020));   // ~20 ms attack (slightly slower to preserve transients)
            _compReleaseCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.080));  // ~80 ms release
            _envL = 0f;
            _envR = 0f;

            // 8. Reverb — comb filter
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
                for (int i = 0; i < count - 1; i += 2)
                {
                    float L = samples[i];
                    float R = samples[i + 1];

                    // 0. DC blocker
                    L = _dcBlocker.Transform(L);
                    R = _dcBlocker.Transform(R);

                    // 1-5. EQ chain
                    L = _rumble.Transform(L);
                    L = _res180.Transform(L);
                    L = _res350.Transform(L);
                    L = _lpf.Transform(L);
                    if (_character != null) L = _character.Transform(L);

                    R = _rumble.Transform(R);
                    R = _res180.Transform(R);
                    R = _res350.Transform(R);
                    R = _lpf.Transform(R);
                    if (_character != null) R = _character.Transform(R);

                    // 6. Subtle saturation
                    if (_satMix > 0f)
                    {
                        L = SoftSaturate(L, ref _satPrevInL, ref _satPrevOutL);
                        R = SoftSaturate(R, ref _satPrevInR, ref _satPrevOutR);
                    }

                    // 7. Compressor
                    L = Compress(L, ref _envL);
                    R = Compress(R, ref _envR);

                    // 8. Stereo narrowing via mid-side
                    float mid = (L + R) * 0.5f;
                    float side = (L - R) * 0.5f;
                    side *= _stereoWidth;
                    L = mid + side;
                    R = mid - side;

                    // 9. Reverb (comb)
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
                for (int i = 0; i < count; i++)
                {
                    float s = samples[i];
                    s = _dcBlocker.Transform(s);
                    s = _rumble.Transform(s);
                    s = _res180.Transform(s);
                    s = _res350.Transform(s);
                    s = _lpf.Transform(s);
                    if (_character != null) s = _character.Transform(s);

                    if (_satMix > 0f)
                        s = SoftSaturate(s, ref _satPrevInL, ref _satPrevOutL);

                    s = Compress(s, ref _envL);
                    samples[i] = s;
                }
            }
        }

        /// <summary>
        /// Very gentle saturation for cabin warmth — tanh soft-clip with parallel mix.
        /// </summary>
        private float SoftSaturate(float input, ref float prevIn, ref float prevOut)
        {
            float saturated = (float)Math.Tanh(input * _satDrive) / (float)Math.Tanh(_satDrive);
            float output = input * (1f - _satMix) + saturated * _satMix;
            // Smooth to prevent zipper noise
            output = output * 0.8f + prevOut * 0.2f;
            prevIn = input;
            prevOut = output;
            return output;
        }

        private float Compress(float input, ref float envelope)
        {
            float abs = Math.Abs(input);

            if (abs > envelope)
                envelope += (abs - envelope) * _compAttackCoeff;
            else
                envelope += (abs - envelope) * _compReleaseCoeff;

            if (envelope > _compThreshold)
            {
                float overDb = 20f * (float)Math.Log10(envelope / _compThreshold);
                float gainDb = overDb * (1f / _compRatio - 1f);
                float gainLinear = DbToLinear(gainDb);
                input *= gainLinear;
            }
            return input;
        }

        private static float DbToLinear(float db) => (float)Math.Pow(10.0, db / 20.0);

        public sealed record PresetParams(
            float CutoffHz, float Width,
            float RumbleHz, float RumbleDb,
            float Res1Hz,   float Res1Db,
            float Res2Hz,   float Res2Db,
            float ReverbMs, float ReverbMix,
            float CompRatio, float CompThreshDb);

        public static PresetParams GetPresetParams(string carType)
        {
            var (cutoff, width) = GetPreset(carType);
            return new PresetParams(
                CutoffHz: cutoff, Width: width,
                RumbleHz: 80f,    RumbleDb: 6f,
                Res1Hz: 180f,     Res1Db: 5f,
                Res2Hz: 350f,     Res2Db: 4f,
                ReverbMs: 30f,    ReverbMix: 0.07f,
                CompRatio: 3f,    CompThreshDb: -12f);
        }

        /// <summary>
        /// Presets for common car types.
        /// Returns (cutoffHz, stereoWidth).
        /// </summary>
        public static (float cutoff, float width) GetPreset(string carType) => carType switch
        {
            "Sedan"     => (2200f, 0.30f),
            "Coupe"     => (2000f, 0.25f),
            "SUV"       => (2500f, 0.35f),
            "Hatchback" => (2000f, 0.25f),
            "Supercar"  => (1800f, 0.20f),
            "Truck"     => (2800f, 0.40f),
            _           => (2000f, 0.30f),
        };
    }
}
