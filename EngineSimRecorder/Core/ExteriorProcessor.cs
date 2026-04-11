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
    ///  11. Multi-tap delay reverb            (natural space simulation)
    ///  12. Slow-attack compressor            (transparent dynamics control)
    ///
    /// Designed to sound natural and organic when played back in-game via FMOD banks.
    /// </summary>
    public sealed class ExteriorProcessor
    {
        // ── EQ filters ───────────────────────────────────────────────────────
        private readonly NAudio.Dsp.BiQuadFilter _dcBlocker;
        private readonly NAudio.Dsp.BiQuadFilter _lpf;
        private readonly NAudio.Dsp.BiQuadFilter _hiShelf;
        private readonly NAudio.Dsp.BiQuadFilter _warmth;
        private NAudio.Dsp.BiQuadFilter _mudCut;       // nullable — disabled if Hz = 0
        private NAudio.Dsp.BiQuadFilter _character;    // nullable — disabled if Hz = 0
        private readonly NAudio.Dsp.BiQuadFilter _presence;

        // ── Transient shaper ─────────────────────────────────────────────────
        private readonly TransientShaper? _transientShaper;

        // ── Harmonic exciter ─────────────────────────────────────────────────
        private readonly HarmonicExciter? _harmonicExciter;

        // ── Tape-style saturation ────────────────────────────────────────────
        private readonly float _satDrive;
        private readonly float _satMix;
        private readonly float[] _prevInputL, _prevInputR;
        private readonly float[] _prevOutputL, _prevOutputR;

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

            // 0. DC blocker
            _dcBlocker = NAudio.Dsp.BiQuadFilter.HighPassFilter(sampleRate, 20f, 0.707f);

            // 1. Low-pass — air absorption
            _lpf = NAudio.Dsp.BiQuadFilter.LowPassFilter(sampleRate, p.LpHz, p.LpQ);

            // 2. High-shelf — natural HF roll-off
            _hiShelf = NAudio.Dsp.BiQuadFilter.HighShelf(sampleRate, p.HsHz, p.HsSlope, p.HsGainDb);

            // 3. Warmth EQ — low-mid body
            _warmth = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, p.MidHz, p.MidQ, p.MidGainDb);

            // 4. Mud cut — surgical dip to clean up boxy range
            if (mudCutHz > 0f && Math.Abs(mudCutDb) > 0.1f)
                _mudCut = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, mudCutHz, p.MudCutQ, mudCutDb);

            // 5. Character band — engine "voice"
            if (characterHz > 0f && Math.Abs(characterDb) > 0.1f)
                _character = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, characterHz, p.CharacterQ, characterDb);

            // 6. Presence boost
            _presence = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, 2500f, 0.5f, 1.5f);

            // 7. Transient shaper
            if (transientBoostDb > 0.5f)
                _transientShaper = new TransientShaper(sampleRate, channels, transientBoostDb);

            // 8. Harmonic exciter
            if (p.ExciterHz > 0f && exciterMix > 0.01f)
                _harmonicExciter = new HarmonicExciter(sampleRate, channels,
                    p.ExciterHz, p.ExciterBandwidthHz, p.ExciterDrive, exciterMix);

            // 9. Tape saturation
            _satDrive = satDrive;
            _satMix = 0.7f;
            _prevInputL = new float[2];
            _prevInputR = new float[2];
            _prevOutputL = new float[2];
            _prevOutputR = new float[2];

            // 10. Multi-tap delay reverb
            _numTaps = 4;
            float[] tapDelays = { 0.012f, 0.021f, 0.035f, 0.052f };
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
                _tapGains[i] = initialGains[i] * (p.ReverbMix / 0.10f);
            }

            // 11. Compressor
            _compThreshold = DbToLinear(p.CompThreshDb);
            _compRatio = p.CompRatio;
            _compAttackCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.030));
            _compReleaseCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.250));
            _compKneeWidth = 6.0f;
            _envL = 0f; _envR = 0f;
            _prevGainL = 1.0f; _prevGainR = 1.0f;
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

                    // 0. DC blocker
                    L = _dcBlocker.Transform(L);
                    R = _dcBlocker.Transform(R);

                    // 1-6. EQ chain
                    L = _lpf.Transform(L);
                    L = _hiShelf.Transform(L);
                    L = _warmth.Transform(L);
                    if (_mudCut != null) L = _mudCut.Transform(L);
                    if (_character != null) L = _character.Transform(L);
                    L = _presence.Transform(L);

                    R = _lpf.Transform(R);
                    R = _hiShelf.Transform(R);
                    R = _warmth.Transform(R);
                    if (_mudCut != null) R = _mudCut.Transform(R);
                    if (_character != null) R = _character.Transform(R);
                    R = _presence.Transform(R);

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

                    // 9. Tape saturation
                    L = TapeSaturate(L, _prevInputL, _prevOutputL);
                    R = TapeSaturate(R, _prevInputR, _prevOutputR);

                    // 10. Multi-tap reverb
                    float reverbL = 0f, reverbR = 0f;
                    for (int t = 0; t < _numTaps; t++)
                    {
                        reverbL += _reverbTapsL[t][_tapPositions[t]] * _tapGains[t];
                        reverbR += _reverbTapsR[t][_tapPositions[t]] * _tapGains[t];

                        _reverbTapsL[t][_tapPositions[t]] = L * 0.3f;
                        _reverbTapsR[t][_tapPositions[t]] = R * 0.3f;

                        _tapPositions[t] = (_tapPositions[t] + 1) % _reverbTapsL[t].Length;
                    }
                    L = L * 0.85f + reverbL;
                    R = R * 0.85f + reverbR;

                    // 11. Compressor
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

                    s = _dcBlocker.Transform(s);
                    s = _lpf.Transform(s);
                    s = _hiShelf.Transform(s);
                    s = _warmth.Transform(s);
                    if (_mudCut != null) s = _mudCut.Transform(s);
                    if (_character != null) s = _character.Transform(s);
                    s = _presence.Transform(s);

                    if (_transientShaper != null)
                        s = _transientShaper.ProcessLeft(s);

                    if (_harmonicExciter != null)
                        s = _harmonicExciter.ProcessLeft(s);

                    s = TapeSaturateMono(s, _prevInputL, _prevOutputL);

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
        //  DSP helpers
        // ─────────────────────────────────────────────────────────────────────

        private float TapeSaturate(float input, float[] prevInput, float[] prevOutput)
        {
            float dc = (input + prevInput[0]) * 0.5f;
            float ac = input - dc;
            float saturated = (float)Math.Tanh(ac * _satDrive) / (float)Math.Tanh(_satDrive);
            float output = input * (1f - _satMix) + saturated * _satMix;
            output = output * 0.7f + prevOutput[0] * 0.3f;
            prevInput[1] = prevInput[0]; prevInput[0] = input;
            prevOutput[1] = prevOutput[0]; prevOutput[0] = output;
            return output;
        }

        private float TapeSaturateMono(float input, float[] prevInput, float[] prevOutput)
        {
            float dc = (input + prevInput[0]) * 0.5f;
            float ac = input - dc;
            float saturated = (float)Math.Tanh(ac * _satDrive) / (float)Math.Tanh(_satDrive);
            float output = input * (1f - _satMix) + saturated * _satMix;
            output = output * 0.7f + prevOutput[0] * 0.3f;
            prevInput[1] = prevInput[0]; prevInput[0] = input;
            prevOutput[1] = prevOutput[0]; prevOutput[0] = output;
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

            float smoothedGain = gainReduction * 0.3f + prevGain * 0.7f;
            prevGain = smoothedGain;
            return input * smoothedGain;
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
