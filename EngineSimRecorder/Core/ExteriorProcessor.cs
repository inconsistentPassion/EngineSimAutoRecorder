using System;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Realistic exterior exhaust acoustics processor with analog-style DSP chain:
    ///   1. DC blocker                       (20 Hz HPF — kills DC offset at source)
    ///   2. Gentle low-pass filter            (air absorption simulation)
    ///   3. Subtle high-shelf roll-off        (natural high-frequency decay)
    ///   4. Warmth EQ                         (low-mid body enhancement)
    ///   5. Mud cut                           (surgical dip at 300-420 Hz, RPM-aware)
    ///   6. Character band boost              (engine "voice" at 1.2-2.5 kHz, RPM-aware)
    ///   7. Presence boost                    (subtle clarity at 2.5 kHz)
    ///   8. Transient shaper                  (attack punch enhancement)
    ///   9. Harmonic exciter                  (band-limited saturation at 2-6 kHz)
    ///  10. Tape-style saturation             (soft knee, harmonic enrichment)
    ///  11. Slow-attack compressor            (transparent dynamics control)
    ///  12. Multi-tap delay reverb            (natural space simulation)
    ///  13. Brick-wall limiter                (prevents clipping, no artifacts)
    ///
    /// Designed to sound natural and organic when played back in-game via FMOD banks.
    /// </summary>
    public sealed class ExteriorProcessor
    {
        // ── EQ filters (separate L/R instances to prevent state corruption) ──
        private readonly NAudio.Dsp.BiQuadFilter _dcBlockerL, _dcBlockerR;
        private readonly NAudio.Dsp.BiQuadFilter _lpfL, _lpfR;
        private readonly NAudio.Dsp.BiQuadFilter _hiShelfL, _hiShelfR;
        private readonly NAudio.Dsp.BiQuadFilter _warmthL, _warmthR;
        private NAudio.Dsp.BiQuadFilter? _mudCutL, _mudCutR;
        private NAudio.Dsp.BiQuadFilter? _characterL, _characterR;
        private readonly NAudio.Dsp.BiQuadFilter _presenceL, _presenceR;

        // ── Transient shaper ─────────────────────────────────────────────────
        private readonly TransientShaper? _transientShaper;

        // ── Harmonic exciter ─────────────────────────────────────────────────
        private readonly HarmonicExciter? _harmonicExciter;

        // ── Tape-style saturation ────────────────────────────────────────────
        private readonly float _satDrive;
        private readonly float _satMix;
        private float _satPrevOutL, _satPrevOutR;

        // ── Multi-tap delay reverb ───────────────────────────────────────────
        private readonly float[][] _reverbTapsL;
        private readonly float[][] _reverbTapsR;
        private readonly int[] _tapPositions;
        private readonly float[] _tapGains;
        private readonly int _numTaps;

        // ── Compressor ───────────────────────────────────────────────────────
        private float _envL, _envR;
        private readonly float _compThreshold;
        private readonly float _compRatio;
        private readonly float _compAttackCoeff;
        private readonly float _compReleaseCoeff;
        private readonly float _compKneeWidth;
        private float _prevGainL, _prevGainR;
        private readonly float _compGainSmooth; // one-pole IIR coefficient

        // ── Output limiter (brick-wall, smooth) ─────────────────────────────
        private readonly float _limiterCeiling;
        private readonly float _limiterAttackCoeff;
        private readonly float _limiterReleaseCoeff;
        private float _limiterEnvL, _limiterEnvR;
        private float _limiterGainL, _limiterGainR;
        private readonly float _limiterGainSmooth; // one-pole IIR coefficient

        private readonly int _channels;
        private readonly int _sampleRate;

        // ─────────────────────────────────────────────────────────────────────

        /// <param name="sampleRate">44100 or 48000</param>
        /// <param name="channels">1 = mono, 2 = stereo</param>
        /// <param name="preset">Exhaust DSP preset (Raw = no processing)</param>
        /// <param name="targetRpm">Target RPM for this recording (0 = RPM-aware disabled)</param>
        public ExteriorProcessor(int sampleRate, int channels,
            ExteriorPreset preset = ExteriorPreset.Raw, int targetRpm = 0)
            : this(sampleRate, channels, GetPresetParams(preset), rpm: null, targetRpm: targetRpm) { }

        /// <summary>Custom-params constructor.</summary>
        /// <param name="targetRpm">Target RPM for this recording (0 = RPM-aware disabled)</param>
        public ExteriorProcessor(int sampleRate, int channels, ExteriorSettings s,
            RpmProcessingSettings? rpm = null, int targetRpm = 0)
            : this(sampleRate, channels, new PresetParams(
                LpHz: s.LpHz,   LpQ: s.LpQ,
                HsHz: s.HsHz,   HsSlope: 1f, HsGainDb: s.HsGainDb,
                MidHz: s.MidHz, MidQ: 0.7f,  MidGainDb: s.MidGainDb,
                SatDrive: s.SatDrive, SatScale: 0.85f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: s.ReverbMs, ReverbFeedback: 0.15f, ReverbMix: s.ReverbMix,
                CompRatio: s.CompRatio, CompThreshDb: s.CompThreshDb,
                // New psychoacoustic params
                MudCutHz: s.MudCutHz, MudCutDb: s.MudCutDb, MudCutQ: s.MudCutQ,
                CharacterHz: s.CharacterHz, CharacterDb: s.CharacterDb, CharacterQ: s.CharacterQ,
                TransientBoostDb: s.TransientBoostDb,
                ExciterHz: s.ExciterHz, ExciterBandwidthHz: s.ExciterBandwidthHz,
                ExciterDrive: s.ExciterDrive, ExciterMix: s.ExciterMix),
                rpm, targetRpm) { }

        private ExteriorProcessor(int sampleRate, int channels, PresetParams p,
            RpmProcessingSettings? rpm = null, int targetRpm = 0)
        {
            _sampleRate = sampleRate;
            _channels = channels;

            // ── Apply RPM scaling if enabled ──────────────────────────────
            float mudCutHz = p.MudCutHz;
            float mudCutDb = p.MudCutDb;
            float characterHz = p.CharacterHz;
            float characterDb = p.CharacterDb;
            float satDrive = p.SatDrive;
            float transientBoostDb = p.TransientBoostDb;
            float exciterMix = p.ExciterMix;

            if (rpm != null && rpm.Enabled && targetRpm > 0)
            {
                mudCutHz = rpm.Lerp(rpm.MudCutHzRange, targetRpm);
                characterHz = rpm.Lerp(rpm.CharacterHzRange, targetRpm);
                satDrive = rpm.Lerp(rpm.SatDriveRange, targetRpm);
                transientBoostDb = rpm.Lerp(rpm.TransientBoostRange, targetRpm);
                exciterMix = rpm.Lerp(rpm.ExciterMixRange, targetRpm);
            }

            // 0. DC blocker — separate L/R instances
            _dcBlockerL = NAudio.Dsp.BiQuadFilter.HighPassFilter(sampleRate, 20f, 0.707f);
            _dcBlockerR = NAudio.Dsp.BiQuadFilter.HighPassFilter(sampleRate, 20f, 0.707f);

            // 1. Low-pass — air absorption
            _lpfL = NAudio.Dsp.BiQuadFilter.LowPassFilter(sampleRate, p.LpHz, p.LpQ);
            _lpfR = NAudio.Dsp.BiQuadFilter.LowPassFilter(sampleRate, p.LpHz, p.LpQ);

            // 2. High-shelf — natural HF roll-off
            _hiShelfL = NAudio.Dsp.BiQuadFilter.HighShelf(sampleRate, p.HsHz, p.HsSlope, p.HsGainDb);
            _hiShelfR = NAudio.Dsp.BiQuadFilter.HighShelf(sampleRate, p.HsHz, p.HsSlope, p.HsGainDb);

            // 3. Warmth EQ — low-mid body
            _warmthL = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, p.MidHz, p.MidQ, p.MidGainDb);
            _warmthR = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, p.MidHz, p.MidQ, p.MidGainDb);

            // 4. Mud cut — surgical dip to clean up boxy range
            if (mudCutHz > 0f && Math.Abs(mudCutDb) > 0.1f)
            {
                _mudCutL = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, mudCutHz, p.MudCutQ, mudCutDb);
                _mudCutR = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, mudCutHz, p.MudCutQ, mudCutDb);
            }

            // 5. Character band — engine "voice"
            if (characterHz > 0f && Math.Abs(characterDb) > 0.1f)
            {
                _characterL = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, characterHz, p.CharacterQ, characterDb);
                _characterR = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, characterHz, p.CharacterQ, characterDb);
            }

            // 6. Presence boost
            _presenceL = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, 2500f, 0.5f, 1.5f);
            _presenceR = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, 2500f, 0.5f, 1.5f);

            // 7. Transient shaper
            if (transientBoostDb > 0.5f)
                _transientShaper = new TransientShaper(sampleRate, channels, transientBoostDb);

            // 8. Harmonic exciter
            if (p.ExciterHz > 0f && exciterMix > 0.01f)
                _harmonicExciter = new HarmonicExciter(sampleRate, channels,
                    p.ExciterHz, p.ExciterBandwidthHz, p.ExciterDrive, exciterMix);

            // 9. Tape saturation — simplified, no fake DC extraction
            _satDrive = satDrive;
            _satMix = 0.5f;
            _satPrevOutL = 0f;
            _satPrevOutR = 0f;

            // 10. Multi-tap delay reverb
            _numTaps = 4;
            float[] tapDelays = { 0.012f, 0.021f, 0.035f, 0.052f };
            float[] initialGains = { 0.10f, 0.06f, 0.04f, 0.02f };

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
                _tapGains[i] = initialGains[i] * (p.ReverbMix / 0.06f);
            }

            // 11. Compressor — placed BEFORE reverb to tame transients
            _compThreshold = DbToLinear(p.CompThreshDb);
            _compRatio = p.CompRatio;
            _compAttackCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.030));
            _compReleaseCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.250));
            _compKneeWidth = 6.0f;
            _envL = 0f; _envR = 0f;
            _prevGainL = 1.0f; _prevGainR = 1.0f;
            // Fix #5: proper one-pole smoothing (~2ms time constant)
            _compGainSmooth = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.002));

            // 12. Output limiter — smooth brick-wall to prevent clipping
            _limiterCeiling = 0.98f;
            _limiterAttackCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.0005)); // 0.5ms attack
            _limiterReleaseCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.100)); // 100ms release
            _limiterEnvL = 0f; _limiterEnvR = 0f;
            _limiterGainL = 1.0f; _limiterGainR = 1.0f;
            // Fix #5: proper one-pole smoothing (~1ms time constant)
            _limiterGainSmooth = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.001));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Process interleaved float32 samples in-place. count = total float samples.</summary>
        public void Process(float[] samples, int count)
        {
            if (_channels >= 2)
            {
                for (int i = 0; i < count - 1; i += 2)
                {
                    float L = samples[i];
                    float R = samples[i + 1];

                    // 0. DC blocker (separate filter instances per channel)
                    L = _dcBlockerL.Transform(L);
                    R = _dcBlockerR.Transform(R);

                    // 1-6. EQ chain (separate L/R filter instances)
                    L = _lpfL.Transform(L);
                    L = _hiShelfL.Transform(L);
                    L = _warmthL.Transform(L);
                    if (_mudCutL != null) L = _mudCutL.Transform(L);
                    if (_characterL != null) L = _characterL.Transform(L);
                    L = _presenceL.Transform(L);

                    R = _lpfR.Transform(R);
                    R = _hiShelfR.Transform(R);
                    R = _warmthR.Transform(R);
                    if (_mudCutR != null) R = _mudCutR.Transform(R);
                    if (_characterR != null) R = _characterR.Transform(R);
                    R = _presenceR.Transform(R);

                    // 7. Transient shaper
                    if (_transientShaper != null)
                    {
                        L = _transientShaper.ProcessLeft(L);
                        R = _transientShaper.ProcessRight(R);
                    }

                    // 8. Harmonic exciter
                    if (_harmonicExciter != null)
                    {
                        L = _harmonicExciter.ProcessLeft(L);
                        R = _harmonicExciter.ProcessRight(R);
                    }

                    // 9. Tape saturation (clean — no fake DC extraction)
                    L = TapeSaturate(L, ref _satPrevOutL);
                    R = TapeSaturate(R, ref _satPrevOutR);

                    // 10. Compressor — BEFORE reverb to control transients
                    L = CompressTransparent(L, ref _envL, ref _prevGainL);
                    R = CompressTransparent(R, ref _envR, ref _prevGainR);

                    // 11. Multi-tap reverb (dry+wet blend, reduced gain)
                    float reverbL = 0f, reverbR = 0f;
                    for (int t = 0; t < _numTaps; t++)
                    {
                        reverbL += _reverbTapsL[t][_tapPositions[t]] * _tapGains[t];
                        reverbR += _reverbTapsR[t][_tapPositions[t]] * _tapGains[t];

                        _reverbTapsL[t][_tapPositions[t]] = L * 0.25f;
                        _reverbTapsR[t][_tapPositions[t]] = R * 0.25f;

                        _tapPositions[t] = (_tapPositions[t] + 1) % _reverbTapsL[t].Length;
                    }
                    L = L * 0.80f + reverbL;
                    R = R * 0.80f + reverbR;

                    // 12. Brick-wall limiter — prevents any clipping
                    L = LimitOutput(L, ref _limiterEnvL, ref _limiterGainL);
                    R = LimitOutput(R, ref _limiterEnvR, ref _limiterGainR);

                    samples[i]     = L;
                    samples[i + 1] = R;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    float s = samples[i];

                    s = _dcBlockerL.Transform(s);
                    s = _lpfL.Transform(s);
                    s = _hiShelfL.Transform(s);
                    s = _warmthL.Transform(s);
                    if (_mudCutL != null) s = _mudCutL.Transform(s);
                    if (_characterL != null) s = _characterL.Transform(s);
                    s = _presenceL.Transform(s);

                    if (_transientShaper != null)
                        s = _transientShaper.ProcessLeft(s);

                    if (_harmonicExciter != null)
                        s = _harmonicExciter.ProcessLeft(s);

                    s = TapeSaturateMono(s, ref _satPrevOutL);

                    s = CompressTransparent(s, ref _envL, ref _prevGainL);

                    float reverb = 0f;
                    for (int t = 0; t < _numTaps; t++)
                    {
                        reverb += _reverbTapsL[t][_tapPositions[t]] * _tapGains[t];
                        _reverbTapsL[t][_tapPositions[t]] = s * 0.25f;
                        _tapPositions[t] = (_tapPositions[t] + 1) % _reverbTapsL[t].Length;
                    }
                    s = s * 0.80f + reverb;

                    s = LimitOutput(s, ref _limiterEnvL, ref _limiterGainL);

                    samples[i] = s;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DSP helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Tape-style saturation without fake DC extraction (which caused clicks).</summary>
        private float TapeSaturate(float input, ref float prevOut)
        {
            // Direct tanh saturation — no 2-sample "DC extraction" artifact
            float saturated = (float)Math.Tanh(input * _satDrive) / (float)Math.Tanh(_satDrive);
            float output = input * (1f - _satMix) + saturated * _satMix;
            // Gentle smoothing to prevent zipper noise
            output = output * 0.8f + prevOut * 0.2f;
            prevOut = output;
            return output;
        }

        private float TapeSaturateMono(float input, ref float prevOut)
        {
            float saturated = (float)Math.Tanh(input * _satDrive) / (float)Math.Tanh(_satDrive);
            float output = input * (1f - _satMix) + saturated * _satMix;
            output = output * 0.8f + prevOut * 0.2f;
            prevOut = output;
            return output;
        }

        private float CompressTransparent(float input, ref float envelope, ref float prevGain)
        {
            float abs = Math.Abs(input);
            if (abs > envelope)
                envelope += (abs - envelope) * _compAttackCoeff;
            else
                envelope += (abs - envelope) * _compReleaseCoeff;

            float gainReduction = 1.0f;
            float envelopeDb = 20f * (float)Math.Log10(Math.Max(envelope, 0.0001f));
            float thresholdDb = 20f * (float)Math.Log10(_compThreshold);

            if (envelopeDb > thresholdDb - _compKneeWidth / 2f)
            {
                float overDb = envelopeDb - thresholdDb;
                if (Math.Abs(overDb) < _compKneeWidth / 2f)
                {
                    float kneeRatio = (overDb + _compKneeWidth / 2f) / _compKneeWidth;
                    float effectiveRatio = 1f + (_compRatio - 1f) * kneeRatio;
                    float gainDb = -overDb * (1f - 1f / effectiveRatio);
                    gainReduction = DbToLinear(gainDb);
                }
                else
                {
                    float gainDb = -overDb * (1f - 1f / _compRatio);
                    gainReduction = DbToLinear(gainDb);
                }
            }

            // Fix #5: proper one-pole IIR smoothing with time-constant coefficient
            prevGain += (gainReduction - prevGain) * _compGainSmooth;
            return input * prevGain;
        }

        /// <summary>Smooth brick-wall limiter with soft-knee tanh saturation instead of hard clip.</summary>
        private float LimitOutput(float input, ref float envelope, ref float prevGain)
        {
            float abs = Math.Abs(input);

            // Track peak with fast attack
            if (abs > envelope)
                envelope += (abs - envelope) * _limiterAttackCoeff;
            else
                envelope += (abs - envelope) * _limiterReleaseCoeff;

            // Compute gain needed to keep signal at or below ceiling
            float targetGain = (envelope > _limiterCeiling)
                ? _limiterCeiling / envelope
                : 1.0f;

            // Fix #5: proper one-pole IIR smoothing with time-constant coefficient
            prevGain += (targetGain - prevGain) * _limiterGainSmooth;

            float output = input * prevGain;

            // Fix #4: soft-knee tanh saturation instead of hard clip — no discontinuities
            if (Math.Abs(output) > _limiterCeiling * 0.95f)
            {
                output = _limiterCeiling * (float)Math.Tanh(output / _limiterCeiling);
            }

            return output;
        }

        private static float DbToLinear(float db) =>
            (float)Math.Pow(10.0, db / 20.0);

        // ─────────────────────────────────────────────────────────────────────
        //  Presets
        // ─────────────────────────────────────────────────────────────────────

        public sealed record PresetParams(
            float LpHz,  float LpQ,
            float HsHz,  float HsSlope, float HsGainDb,
            float MidHz, float MidQ,    float MidGainDb,
            float SatDrive, float SatScale,
            bool  EnableNoise, float NoiseGain,
            float ReverbMs, float ReverbFeedback, float ReverbMix,
            float CompRatio, float CompThreshDb,
            // Psychoacoustic enhancement
            float MudCutHz = 350f, float MudCutDb = -3f, float MudCutQ = 2.0f,
            float CharacterHz = 1500f, float CharacterDb = 2f, float CharacterQ = 1.2f,
            float TransientBoostDb = 3f,
            float ExciterHz = 3000f, float ExciterBandwidthHz = 3000f,
            float ExciterDrive = 2.5f, float ExciterMix = 0.15f);

        public static PresetParams GetPresetParams(ExteriorPreset preset) => preset switch
        {
            ExteriorPreset.Raw => new(
                LpHz: 18000f, LpQ: 0.707f,
                HsHz: 12000f, HsSlope: 0.5f, HsGainDb: -1f,
                MidHz: 200f,  MidQ: 0.5f,  MidGainDb: 1f,
                SatDrive: 1.1f, SatScale: 1f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 15f, ReverbFeedback: 0.05f, ReverbMix: 0.03f,
                CompRatio: 1.2f, CompThreshDb: -18f,
                // Minimal enhancement for raw
                MudCutHz: 0f, MudCutDb: 0f,
                CharacterHz: 0f, CharacterDb: 0f,
                TransientBoostDb: 0f,
                ExciterHz: 0f, ExciterMix: 0f),

            ExteriorPreset.Sport => new(
                LpHz: 14000f, LpQ: 0.65f,
                HsHz: 8000f, HsSlope: 0.6f, HsGainDb: -2f,
                MidHz: 180f, MidQ: 0.6f,    MidGainDb: 2.5f,
                SatDrive: 1.4f, SatScale: 0.90f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 25f, ReverbFeedback: 0.10f, ReverbMix: 0.06f,
                CompRatio: 1.8f, CompThreshDb: -16f,
                MudCutHz: 350f, MudCutDb: -2f, MudCutQ: 2.0f,
                CharacterHz: 1500f, CharacterDb: 2f, CharacterQ: 1.2f,
                TransientBoostDb: 2.5f,
                ExciterHz: 3000f, ExciterBandwidthHz: 3000f,
                ExciterDrive: 2.0f, ExciterMix: 0.10f),

            ExteriorPreset.Race => new(
                LpHz: 12000f, LpQ: 0.60f,
                HsHz: 7000f, HsSlope: 0.7f, HsGainDb: -3f,
                MidHz: 160f, MidQ: 0.65f,    MidGainDb: 3.5f,
                SatDrive: 1.6f, SatScale: 0.85f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 30f, ReverbFeedback: 0.12f, ReverbMix: 0.08f,
                CompRatio: 2.2f, CompThreshDb: -15f,
                MudCutHz: 330f, MudCutDb: -3f, MudCutQ: 2.0f,
                CharacterHz: 1800f, CharacterDb: 3f, CharacterQ: 1.0f,
                TransientBoostDb: 3.5f,
                ExciterHz: 3500f, ExciterBandwidthHz: 3500f,
                ExciterDrive: 3.0f, ExciterMix: 0.18f),

            ExteriorPreset.Supercar => new(
                LpHz: 15000f, LpQ: 0.70f,
                HsHz: 9000f, HsSlope: 0.5f, HsGainDb: -1.5f,
                MidHz: 150f, MidQ: 0.7f,  MidGainDb: 3f,
                SatDrive: 1.5f, SatScale: 0.88f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 22f, ReverbFeedback: 0.08f, ReverbMix: 0.05f,
                CompRatio: 1.6f, CompThreshDb: -17f,
                MudCutHz: 350f, MudCutDb: -2.5f, MudCutQ: 2.0f,
                CharacterHz: 1600f, CharacterDb: 2.5f, CharacterQ: 1.1f,
                TransientBoostDb: 3f,
                ExciterHz: 2800f, ExciterBandwidthHz: 2500f,
                ExciterDrive: 2.5f, ExciterMix: 0.12f),

            ExteriorPreset.Muffler => new(
                LpHz: 10000f, LpQ: 0.75f,
                HsHz: 6000f, HsSlope: 0.8f, HsGainDb: -4f,
                MidHz: 200f, MidQ: 0.8f,  MidGainDb: 2f,
                SatDrive: 1.3f, SatScale: 0.92f,
                EnableNoise: false, NoiseGain: 0f,
                ReverbMs: 20f, ReverbFeedback: 0.08f, ReverbMix: 0.04f,
                CompRatio: 2.0f, CompThreshDb: -16f,
                // Muffler: minimal enhancement, it's supposed to be subdued
                MudCutHz: 380f, MudCutDb: -1.5f, MudCutQ: 2.0f,
                CharacterHz: 1400f, CharacterDb: 1f, CharacterQ: 1.5f,
                TransientBoostDb: 1.5f,
                ExciterHz: 2500f, ExciterBandwidthHz: 2000f,
                ExciterDrive: 1.8f, ExciterMix: 0.06f),

            _ => GetPresetParams(ExteriorPreset.Sport),
        };

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
