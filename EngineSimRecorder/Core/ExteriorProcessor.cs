using System;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Simulates exterior exhaust acoustics by chaining:
    ///   1. Low-pass filter         (preset-dependent cutoff)
    ///   2. High-shelf cut          (upper harmonic roll-off)
    ///   3. Low-mid boost           (exhaust fundamental body)
    ///   4. Soft-clip saturation    (tanh drive)
    ///   5. Optional mechanical noise (white noise at ~-22 dB)
    ///   6. Short comb reverb       (~25 ms exhaust pipe resonance)
    ///   7. Light compressor        (ratio ~3:1, threshold -12 dB)
    /// </summary>
    public sealed class ExteriorProcessor
    {
        // ── EQ filters ───────────────────────────────────────────────────────
        private readonly NAudio.Dsp.BiQuadFilter _lpf;
        private readonly NAudio.Dsp.BiQuadFilter _hiShelf;
        private readonly NAudio.Dsp.BiQuadFilter _lowMid;

        // ── Saturation ───────────────────────────────────────────────────────
        private readonly float _satDrive;
        private readonly float _satScale;

        // ── Mechanical noise ─────────────────────────────────────────────────
        private readonly bool _enableNoise;
        private readonly float _noiseGain;
        private readonly Random _rng = new(12345); // deterministic seed

        // ── Comb reverb (exhaust pipe resonance) ─────────────────────────────
        private readonly float[] _reverbBufL;
        private readonly float[] _reverbBufR;
        private int _reverbPos;
        private readonly float _reverbFeedback;
        private readonly float _reverbMix;

        // ── Compressor ───────────────────────────────────────────────────────
        private float _envL, _envR;
        private readonly float _compThreshold;
        private readonly float _compRatio;
        private readonly float _compAttackCoeff;
        private readonly float _compReleaseCoeff;

        private readonly int _channels;

        // ─────────────────────────────────────────────────────────────────────

        /// <param name="sampleRate">44100 or 48000</param>
        /// <param name="channels">1 = mono, 2 = stereo</param>
        /// <param name="preset">Exhaust DSP preset (Raw = no processing)</param>
        public ExteriorProcessor(int sampleRate, int channels,
            ExteriorPreset preset = ExteriorPreset.Raw)
            : this(sampleRate, channels, GetPresetParams(preset)) { }

        /// <summary>Custom-params constructor — mirrors InteriorProcessor's explicit-param constructor.</summary>
        public ExteriorProcessor(int sampleRate, int channels, ExteriorSettings s)
            : this(sampleRate, channels, new PresetParams(
                LpHz: s.LpHz,   LpQ: s.LpQ,
                HsHz: s.HsHz,   HsSlope: 1f, HsGainDb: s.HsGainDb,
                MidHz: s.MidHz, MidQ: 0.7f,  MidGainDb: s.MidGainDb,
                SatDrive: s.SatDrive, SatScale: 0.6f,
                EnableNoise: s.EnableNoise, NoiseGain: 0.08f,
                ReverbMs: s.ReverbMs, ReverbFeedback: 0.20f, ReverbMix: s.ReverbMix,
                CompRatio: s.CompRatio, CompThreshDb: s.CompThreshDb)) { }

        private ExteriorProcessor(int sampleRate, int channels, PresetParams p)
        {
            _channels = channels;

            // 1. Low-pass
            _lpf = NAudio.Dsp.BiQuadFilter.LowPassFilter(sampleRate, p.LpHz, p.LpQ);

            // 2. High-shelf cut
            _hiShelf = NAudio.Dsp.BiQuadFilter.HighShelf(sampleRate, p.HsHz, p.HsSlope, p.HsGainDb);

            // 3. Low-mid boost (exhaust fundamental)
            _lowMid = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, p.MidHz, p.MidQ, p.MidGainDb);

            // 4. Saturation
            _satDrive = p.SatDrive;
            _satScale = p.SatScale;

            // 5. Noise
            _enableNoise = p.EnableNoise;
            _noiseGain   = p.NoiseGain;

            // 6. Comb reverb — exhaust pipe resonance (~25 ms)
            int delaySamples = Math.Max(1, (int)(sampleRate * p.ReverbMs / 1000f));
            _reverbBufL = new float[delaySamples];
            _reverbBufR = new float[delaySamples];
            _reverbPos = 0;
            _reverbFeedback = p.ReverbFeedback;
            _reverbMix = p.ReverbMix;

            // 7. Compressor — one-pole smoothing coefficients
            _compThreshold    = DbToLinear(p.CompThreshDb);
            _compRatio        = p.CompRatio;
            _compAttackCoeff  = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.010)); // ~10 ms attack
            _compReleaseCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.120)); // ~120 ms release
            _envL = 0f;
            _envR = 0f;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Process interleaved float32 samples in-place.</summary>
        public void Process(float[] samples, int count)
        {
            if (_channels >= 2)
            {
                for (int i = 0; i < count - 1; i += 2)
                {
                    float L = samples[i];
                    float R = samples[i + 1];

                    // 1-3. EQ chain
                    L = _lpf.Transform(L);
                    L = _hiShelf.Transform(L);
                    L = _lowMid.Transform(L);

                    R = _lpf.Transform(R);
                    R = _hiShelf.Transform(R);
                    R = _lowMid.Transform(R);

                    // 4. Soft-clip saturation
                    L = Saturate(L);
                    R = Saturate(R);

                    // 5. Noise
                    if (_enableNoise)
                    {
                        L += (float)(_rng.NextDouble() * 2.0 - 1.0) * _noiseGain;
                        R += (float)(_rng.NextDouble() * 2.0 - 1.0) * _noiseGain;
                    }

                    // 6. Comb reverb
                    float wetL = _reverbBufL[_reverbPos];
                    float wetR = _reverbBufR[_reverbPos];
                    _reverbBufL[_reverbPos] = L + wetL * _reverbFeedback;
                    _reverbBufR[_reverbPos] = R + wetR * _reverbFeedback;
                    _reverbPos = (_reverbPos + 1) % _reverbBufL.Length;

                    L = L + wetL * _reverbMix;
                    R = R + wetR * _reverbMix;

                    // 7. Compressor
                    L = Compress(L, ref _envL);
                    R = Compress(R, ref _envR);

                    samples[i]     = L;
                    samples[i + 1] = R;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    float s = samples[i];

                    s = _lpf.Transform(s);
                    s = _hiShelf.Transform(s);
                    s = _lowMid.Transform(s);

                    s = Saturate(s);

                    if (_enableNoise)
                        s += (float)(_rng.NextDouble() * 2.0 - 1.0) * _noiseGain;

                    float wet = _reverbBufL[_reverbPos];
                    _reverbBufL[_reverbPos] = s + wet * _reverbFeedback;
                    _reverbPos = (_reverbPos + 1) % _reverbBufL.Length;
                    s = s + wet * _reverbMix;

                    s = Compress(s, ref _envL);
                    samples[i] = s;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DSP helpers
        // ─────────────────────────────────────────────────────────────────────

        private float Saturate(float x) =>
            (float)Math.Tanh(x * _satDrive) * _satScale;

        private float Compress(float input, ref float envelope)
        {
            float abs = Math.Abs(input);
            if (abs > envelope)
                envelope += (abs - envelope) * _compAttackCoeff;
            else
                envelope += (abs - envelope) * _compReleaseCoeff;

            if (envelope > _compThreshold)
            {
                float overDb  = 20f * (float)Math.Log10(envelope / _compThreshold);
                float gainDb  = overDb * (1f / _compRatio - 1f);
                input *= DbToLinear(gainDb);
            }
            return input;
        }

        private static float DbToLinear(float db) =>
            (float)Math.Pow(10.0, db / 20.0);

        // ─────────────────────────────────────────────────────────────────────
        //  Presets
        // ─────────────────────────────────────────────────────────────────────

        private sealed record PresetParams(
            float LpHz,  float LpQ,
            float HsHz,  float HsSlope, float HsGainDb,
            float MidHz, float MidQ,    float MidGainDb,
            float SatDrive, float SatScale,
            bool  EnableNoise, float NoiseGain,
            float ReverbMs, float ReverbFeedback, float ReverbMix,
            float CompRatio, float CompThreshDb);

        private static PresetParams GetPresetParams(ExteriorPreset preset) => preset switch
        {
            ExteriorPreset.Raw => new(
                LpHz: 20000f, LpQ: 0.707f,
                HsHz: 10000f, HsSlope: 1f, HsGainDb: 0f,
                MidHz: 150f,  MidQ: 0.7f,  MidGainDb: 0f,
                SatDrive: 1f, SatScale: 1f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 1f, ReverbFeedback: 0f, ReverbMix: 0f,
                CompRatio: 1f, CompThreshDb: 0f),

            ExteriorPreset.Sport => new(
                LpHz: 8000f, LpQ: 0.7f,
                HsHz: 5000f, HsSlope: 0.8f, HsGainDb: -3f,
                MidHz: 150f, MidQ: 0.7f,    MidGainDb: 3f,
                SatDrive: 2.0f, SatScale: 0.65f,
                EnableNoise: true, NoiseGain: 0.08f,
                ReverbMs: 22f, ReverbFeedback: 0.18f, ReverbMix: 0.08f,
                CompRatio: 3f, CompThreshDb: -12f),

            ExteriorPreset.Race => new(
                LpHz: 7000f, LpQ: 0.65f,
                HsHz: 5000f, HsSlope: 0.8f, HsGainDb: -4f,
                MidHz: 150f, MidQ: 0.6f,    MidGainDb: 4f,
                SatDrive: 2.5f, SatScale: 0.60f,
                EnableNoise: true, NoiseGain: 0.10f,
                ReverbMs: 25f, ReverbFeedback: 0.22f, ReverbMix: 0.12f,
                CompRatio: 3.5f, CompThreshDb: -14f),

            ExteriorPreset.Supercar => new(
                LpHz: 9000f, LpQ: 0.75f,
                HsHz: 5500f, HsSlope: 1f, HsGainDb: -2f,
                MidHz: 120f, MidQ: 0.8f,  MidGainDb: 2.5f,
                SatDrive: 2.8f, SatScale: 0.55f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 20f, ReverbFeedback: 0.15f, ReverbMix: 0.10f,
                CompRatio: 2.5f, CompThreshDb: -10f),

            ExteriorPreset.Muffler => new(
                LpHz: 5500f, LpQ: 0.85f,
                HsHz: 4000f, HsSlope: 1f, HsGainDb: -6f,
                MidHz: 180f, MidQ: 0.9f,  MidGainDb: 2f,
                SatDrive: 1.8f, SatScale: 0.70f,
                EnableNoise: true, NoiseGain: 0.05f,
                ReverbMs: 30f, ReverbFeedback: 0.25f, ReverbMix: 0.06f,
                CompRatio: 4f, CompThreshDb: -10f),

            _ => GetPresetParams(ExteriorPreset.Race),
        };

        /// <summary>Returns the display name for each preset.</summary>
        public static string PresetDisplayName(ExteriorPreset p) => p switch
        {
            ExteriorPreset.Raw      => "Raw (no processing)",
            ExteriorPreset.Sport    => "Sport",
            ExteriorPreset.Race     => "Race",
            ExteriorPreset.Supercar => "Supercar",
            ExteriorPreset.Muffler  => "Muffled / OEM",
            ExteriorPreset.Custom   => "Custom",
            _ => p.ToString()
        };
    }

    public enum ExteriorPreset
    {
        Raw = 0,
        Sport,
        Race,
        Supercar,
        Muffler,
        Custom
    }
}
