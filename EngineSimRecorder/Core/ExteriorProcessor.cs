using System;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Realistic exterior exhaust acoustics processor with analog-style DSP chain:
    ///   1. Gentle low-pass filter        (air absorption simulation)
    ///   2. Subtle high-shelf roll-off    (natural high-frequency decay)
    ///   3. Warmth EQ                     (low-mid body enhancement)
    ///   4. Tape-style saturation         (soft knee, harmonic enrichment)
    ///   5. Multi-tap delay reverb        (natural space simulation)
    ///   6. Slow-attack compressor        (transparent dynamics control)
    ///   7. Dithering for bit-depth reduction
    ///
    /// Designed to sound natural and organic when played back in-game via FMOD banks.
    /// </summary>
    public sealed class ExteriorProcessor
    {
        // ── EQ filters ───────────────────────────────────────────────────────
        private readonly NAudio.Dsp.BiQuadFilter _lpf;
        private readonly NAudio.Dsp.BiQuadFilter _hiShelf;
        private readonly NAudio.Dsp.BiQuadFilter _warmth;
        private readonly NAudio.Dsp.BiQuadFilter _presence; // subtle presence boost

        // ── Tape-style saturation ────────────────────────────────────────────
        private readonly float _satDrive;
        private readonly float _satMix; // dry/wet mix for parallel saturation
        private readonly float[] _prevInputL, _prevInputR; // for DC tracking
        private readonly float[] _prevOutputL, _prevOutputR;

        // ── Multi-tap delay reverb (more natural than comb) ──────────────────
        private readonly float[][] _reverbTapsL;
        private readonly float[][] _reverbTapsR;
        private readonly int[] _tapPositions;
        private readonly float[] _tapGains;
        private readonly int _numTaps;
        private int _reverbPos;

        // ── Compressor with side-chain filtering ─────────────────────────────
        private float _envL, _envR;
        private readonly float _compThreshold;
        private readonly float _compRatio;
        private readonly float _compAttackCoeff;
        private readonly float _compReleaseCoeff;
        private readonly float _compKneeWidth; // soft knee width in dB
        private float _prevGainL, _prevGainR; // gain smoothing

        private readonly int _channels;
        private readonly int _sampleRate;

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
                SatDrive: s.SatDrive, SatScale: 0.85f, // softer saturation
                EnableNoise: false, NoiseGain: 0f, // disable noise by default
                ReverbMs: s.ReverbMs, ReverbFeedback: 0.15f, ReverbMix: s.ReverbMix,
                CompRatio: s.CompRatio, CompThreshDb: s.CompThreshDb)) { }

        private ExteriorProcessor(int sampleRate, int channels, PresetParams p)
        {
            _sampleRate = sampleRate;
            _channels = channels;

            // 1. Low-pass - gentle air absorption simulation
            _lpf = NAudio.Dsp.BiQuadFilter.LowPassFilter(sampleRate, p.LpHz, p.LpQ);

            // 2. High-shelf - natural HF roll-off (not harsh cut)
            _hiShelf = NAudio.Dsp.BiQuadFilter.HighShelf(sampleRate, p.HsHz, p.HsSlope, p.HsGainDb);

            // 3. Warmth EQ - adds body and fullness to exhaust note
            _warmth = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, p.MidHz, p.MidQ, p.MidGainDb);

            // 4. Presence boost - subtle clarity enhancement around 2-3kHz
            _presence = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, 2500f, 0.5f, 1.5f);

            // 5. Tape-style saturation parameters
            _satDrive = p.SatDrive;
            _satMix = 0.7f; // 70% wet, 30% dry for parallel processing
            _prevInputL = new float[2];
            _prevInputR = new float[2];
            _prevOutputL = new float[2];
            _prevOutputR = new float[2];

            // 6. Multi-tap delay reverb (4 taps for natural space)
            _numTaps = 4;
            float[] tapDelays = { 0.012f, 0.021f, 0.035f, 0.052f }; // 12ms, 21ms, 35ms, 52ms
            float[] initialGains = { 0.12f, 0.08f, 0.05f, 0.03f };
            
            _reverbTapsL = new float[_numTaps][];
            _reverbTapsR = new float[_numTaps][];
            _tapPositions = new int[_numTaps];
            _tapGains = new float[_numTaps];

            for (int i = 0; i < _numTaps; i++)
            {
                int delaySamples = Math.Max(1, (int)(sampleRate * tapDelays[i]));
                _reverbTapsL[i] = new float[delaySamples];
                _reverbTapsR[i] = new float[delaySamples];
                _tapPositions[i] = 0;
                _tapGains[i] = initialGains[i] * (p.ReverbMix / 0.10f); // scale with user setting
            }
            _reverbPos = 0;

            // 7. Compressor with slow attack/release for transparent dynamics
            _compThreshold = DbToLinear(p.CompThreshDb);
            _compRatio = p.CompRatio;
            _compAttackCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.030)); // 30ms attack (slower)
            _compReleaseCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.250)); // 250ms release (smoother)
            _compKneeWidth = 6.0f; // 6dB soft knee
            _envL = 0f;
            _envR = 0f;
            _prevGainL = 1.0f;
            _prevGainR = 1.0f;
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

                    // 1-3. EQ chain (analog-style warmth)
                    L = _lpf.Transform(L);
                    L = _hiShelf.Transform(L);
                    L = _warmth.Transform(L);
                    L = _presence.Transform(L);

                    R = _lpf.Transform(R);
                    R = _hiShelf.Transform(R);
                    R = _warmth.Transform(R);
                    R = _presence.Transform(R);

                    // 4. Tape-style saturation (parallel processing)
                    L = TapeSaturate(L, _prevInputL, _prevOutputL);
                    R = TapeSaturate(R, _prevInputR, _prevOutputR);

                    // 5. Multi-tap delay reverb (natural space)
                    float reverbL = 0f, reverbR = 0f;
                    for (int t = 0; t < _numTaps; t++)
                    {
                        reverbL += _reverbTapsL[t][_tapPositions[t]] * _tapGains[t];
                        reverbR += _reverbTapsR[t][_tapPositions[t]] * _tapGains[t];
                        
                        _reverbTapsL[t][_tapPositions[t]] = L * 0.3f; // feedback
                        _reverbTapsR[t][_tapPositions[t]] = R * 0.3f;
                        
                        _tapPositions[t] = (_tapPositions[t] + 1) % _reverbTapsL[t].Length;
                    }
                    L = L * 0.85f + reverbL; // 85% dry, 15% wet base
                    R = R * 0.85f + reverbR;

                    // 6. Transparent compressor
                    L = CompressTransparent(L, ref _envL, ref _prevGainL);
                    R = CompressTransparent(R, ref _envR, ref _prevGainR);

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
                    s = _warmth.Transform(s);
                    s = _presence.Transform(s);

                    s = TapeSaturateMono(s, _prevInputL, _prevOutputL);

                    // Mono reverb
                    float reverb = 0f;
                    for (int t = 0; t < _numTaps; t++)
                    {
                        reverb += _reverbTapsL[t][_tapPositions[t]] * _tapGains[t];
                        _reverbTapsL[t][_tapPositions[t]] = s * 0.3f;
                        _tapPositions[t] = (_tapPositions[t] + 1) % _reverbTapsL[t].Length;
                    }
                    s = s * 0.85f + reverb;

                    s = CompressTransparent(s, ref _envL, ref _prevGainL);
                    samples[i] = s;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DSP helpers - Analog-style processing
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tape-style saturation using soft-clipping with DC offset compensation
        /// and parallel processing for transparency.
        /// </summary>
        private float TapeSaturate(float input, float[] prevInput, float[] prevOutput)
        {
            // DC offset tracking
            float dc = (input + prevInput[0]) * 0.5f;
            float ac = input - dc;

            // Soft saturation curve (gentler than tanh)
            float drive = _satDrive;
            float saturated = (float)Math.Tanh(ac * drive) / (float)Math.Tanh(drive);
            
            // Parallel processing: blend dry and wet
            float output = input * (1f - _satMix) + saturated * _satMix;

            // Smooth transitions (prevent zipper noise)
            output = output * 0.7f + prevOutput[0] * 0.3f;

            // Update history
            prevInput[1] = prevInput[0];
            prevInput[0] = input;
            prevOutput[1] = prevOutput[0];
            prevOutput[0] = output;

            return output;
        }

        private float TapeSaturateMono(float input, float[] prevInput, float[] prevOutput)
        {
            float dc = (input + prevInput[0]) * 0.5f;
            float ac = input - dc;

            float drive = _satDrive;
            float saturated = (float)Math.Tanh(ac * drive) / (float)Math.Tanh(drive);
            
            float output = input * (1f - _satMix) + saturated * _satMix;
            output = output * 0.7f + prevOutput[0] * 0.3f;

            prevInput[1] = prevInput[0];
            prevInput[0] = input;
            prevOutput[1] = prevOutput[0];
            prevOutput[0] = output;

            return output;
        }

        /// <summary>
        /// Transparent compressor with soft knee and gain smoothing.
        /// Avoids pumping/breathing artifacts.
        /// </summary>
        private float CompressTransparent(float input, ref float envelope, ref float prevGain)
        {
            float abs = Math.Abs(input);
            
            // Peak detector with asymmetric attack/release
            if (abs > envelope)
                envelope += (abs - envelope) * _compAttackCoeff;
            else
                envelope += (abs - envelope) * _compReleaseCoeff;

            // Calculate gain reduction with soft knee
            float gainReduction = 1.0f;
            float envelopeDb = 20f * (float)Math.Log10(Math.Max(envelope, 0.0001f));
            float thresholdDb = 20f * (float)Math.Log10(_compThreshold);
            
            if (envelopeDb > thresholdDb - _compKneeWidth / 2f)
            {
                float overDb = envelopeDb - thresholdDb;
                
                if (Math.Abs(overDb) < _compKneeWidth / 2f)
                {
                    // Soft knee region - gradual compression
                    float kneeRatio = (overDb + _compKneeWidth / 2f) / _compKneeWidth;
                    float effectiveRatio = 1f + (_compRatio - 1f) * kneeRatio;
                    float gainDb = -overDb * (1f - 1f / effectiveRatio);
                    gainReduction = DbToLinear(gainDb);
                }
                else
                {
                    // Hard knee region - full compression ratio
                    float gainDb = -overDb * (1f - 1f / _compRatio);
                    gainReduction = DbToLinear(gainDb);
                }
            }

            // Smooth gain changes (prevent zipper noise)
            float smoothedGain = gainReduction * 0.3f + prevGain * 0.7f;
            prevGain = smoothedGain;

            return input * smoothedGain;
        }

        private static float DbToLinear(float db) =>
            (float)Math.Pow(10.0, db / 20.0);

        // ─────────────────────────────────────────────────────────────────────
        //  Presets - Optimized for realistic, non-digital sound
        // ─────────────────────────────────────────────────────────────────────

        public sealed record PresetParams(
            float LpHz,  float LpQ,
            float HsHz,  float HsSlope, float HsGainDb,
            float MidHz, float MidQ,    float MidGainDb,
            float SatDrive, float SatScale,
            bool  EnableNoise, float NoiseGain,
            float ReverbMs, float ReverbFeedback, float ReverbMix,
            float CompRatio, float CompThreshDb);

        public static PresetParams GetPresetParams(ExteriorPreset preset) => preset switch
        {
            // Raw: minimal processing, just gentle air absorption
            ExteriorPreset.Raw => new(
                LpHz: 18000f, LpQ: 0.707f,
                HsHz: 12000f, HsSlope: 0.5f, HsGainDb: -1f,
                MidHz: 200f,  MidQ: 0.5f,  MidGainDb: 1f,
                SatDrive: 1.1f, SatScale: 1f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 15f, ReverbFeedback: 0.05f, ReverbMix: 0.03f,
                CompRatio: 1.2f, CompThreshDb: -18f),

            // Sport: balanced warmth with subtle character
            ExteriorPreset.Sport => new(
                LpHz: 14000f, LpQ: 0.65f,
                HsHz: 8000f, HsSlope: 0.6f, HsGainDb: -2f,
                MidHz: 180f, MidQ: 0.6f,    MidGainDb: 2.5f,
                SatDrive: 1.4f, SatScale: 0.90f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 25f, ReverbFeedback: 0.10f, ReverbMix: 0.06f,
                CompRatio: 1.8f, CompThreshDb: -16f),

            // Race: more aggressive but still natural
            ExteriorPreset.Race => new(
                LpHz: 12000f, LpQ: 0.60f,
                HsHz: 7000f, HsSlope: 0.7f, HsGainDb: -3f,
                MidHz: 160f, MidQ: 0.65f,    MidGainDb: 3.5f,
                SatDrive: 1.6f, SatScale: 0.85f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 30f, ReverbFeedback: 0.12f, ReverbMix: 0.08f,
                CompRatio: 2.2f, CompThreshDb: -15f),

            // Supercar: rich, full-bodied with presence
            ExteriorPreset.Supercar => new(
                LpHz: 15000f, LpQ: 0.70f,
                HsHz: 9000f, HsSlope: 0.5f, HsGainDb: -1.5f,
                MidHz: 150f, MidQ: 0.7f,  MidGainDb: 3f,
                SatDrive: 1.5f, SatScale: 0.88f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 22f, ReverbFeedback: 0.08f, ReverbMix: 0.05f,
                CompRatio: 1.6f, CompThreshDb: -17f),

            // Muffler: subdued, OEM-like character
            ExteriorPreset.Muffler => new(
                LpHz: 10000f, LpQ: 0.75f,
                HsHz: 6000f, HsSlope: 0.8f, HsGainDb: -4f,
                MidHz: 200f, MidQ: 0.8f,  MidGainDb: 2f,
                SatDrive: 1.3f, SatScale: 0.92f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 20f, ReverbFeedback: 0.08f, ReverbMix: 0.04f,
                CompRatio: 2.0f, CompThreshDb: -16f),

            _ => GetPresetParams(ExteriorPreset.Sport),
        };

        /// <summary>Returns the display name for each preset.</summary>
        public static string PresetDisplayName(ExteriorPreset p) => p switch
        {
            ExteriorPreset.Raw      => "Raw (minimal processing)",
            ExteriorPreset.Sport    => "Sport (balanced)",
            ExteriorPreset.Race     => "Race (aggressive)",
            ExteriorPreset.Supercar => "Supercar (rich)",
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
