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
    ///   8. Narrow stereo                      (mid-side width 20–40%)
    ///   9. Compressor                         (smooth dynamics — before reverb)
    ///  10. Tiny cabin reverb                  (short comb, ~30 ms, 5–10% mix)
    ///  11. Brick-wall limiter                 (prevents clipping)
    /// </summary>
    public sealed class InteriorProcessor
    {
        // ── Separate L/R filter instances to prevent state corruption ─────
        private readonly NAudio.Dsp.BiQuadFilter _dcBlockerL, _dcBlockerR;
        private readonly NAudio.Dsp.BiQuadFilter _lpfL, _lpfR;
        private readonly NAudio.Dsp.BiQuadFilter _res180L, _res180R;
        private readonly NAudio.Dsp.BiQuadFilter _res350L, _res350R;
        private readonly NAudio.Dsp.BiQuadFilter _rumbleL, _rumbleR;
        private NAudio.Dsp.BiQuadFilter? _characterL, _characterR;
        private readonly float _stereoWidth;

        // ── Subtle saturation ─────────────────────────────────────────────
        private readonly float _satDrive;
        private readonly float _satMix;
        private float _satPrevOutL, _satPrevOutR;

        // Compressor state (per-channel)
        private float _envL, _envR;
        private readonly float _compThreshold;
        private readonly float _compRatio;
        private readonly float _compAttackCoeff;
        private readonly float _compReleaseCoeff;
        private float _prevCompGainL, _prevCompGainR;
        private readonly float _compGainSmooth; // one-pole IIR coefficient

        // Simple reverb (comb filter)
        private readonly float[] _reverbBufL;
        private readonly float[] _reverbBufR;
        private int _reverbPos;
        private readonly float _reverbFeedback;
        private readonly float _reverbMix;

        // ── Output limiter ────────────────────────────────────────────────
        private readonly float _limiterCeiling;
        private readonly float _limiterAttackCoeff;
        private readonly float _limiterReleaseCoeff;
        private float _limiterEnvL, _limiterEnvR;
        private float _limiterGainL, _limiterGainR;
        private readonly float _limiterGainSmooth; // one-pole IIR coefficient

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

            // 0. DC blocker — separate L/R instances
            _dcBlockerL = NAudio.Dsp.BiQuadFilter.HighPassFilter(sampleRate, 20f, 0.707f);
            _dcBlockerR = NAudio.Dsp.BiQuadFilter.HighPassFilter(sampleRate, 20f, 0.707f);

            // 1. Structure-borne rumble
            _rumbleL = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, rumbleHz, 1.2f, effectiveRumbleDb);
            _rumbleR = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, rumbleHz, 1.2f, effectiveRumbleDb);

            // 2. Cabin resonance — boost at res1 Hz
            _res180L = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, res1Hz, 1.5f, res1Db);
            _res180R = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, res1Hz, 1.5f, res1Db);

            // 3. Cabin resonance — boost at res2 Hz
            _res350L = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, res2Hz, 1.8f, res2Db);
            _res350R = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, res2Hz, 1.8f, res2Db);

            // 4. Low-pass
            _lpfL = NAudio.Dsp.BiQuadFilter.LowPassFilter(sampleRate, effectiveCutoffHz, 0.707f);
            _lpfR = NAudio.Dsp.BiQuadFilter.LowPassFilter(sampleRate, effectiveCutoffHz, 0.707f);

            // 5. Character band — engine "voice" through cabin
            if (characterHz > 0f && Math.Abs(characterDb) > 0.1f)
            {
                _characterL = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, characterHz, 1.0f, characterDb);
                _characterR = NAudio.Dsp.BiQuadFilter.PeakingEQ(sampleRate, characterHz, 1.0f, characterDb);
            }

            // 6. Subtle saturation
            _satDrive = satDrive;
            _satMix = satDrive > 1.01f ? 0.20f : 0f;
            _satPrevOutL = 0f; _satPrevOutR = 0f;

            // 7. Compressor — placed BEFORE reverb
            _compThreshold = DbToLinear(effectiveCompThreshDb);
            _compRatio = compRatio;
            _compAttackCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.020));
            _compReleaseCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.080));
            _envL = 0f; _envR = 0f;
            _prevCompGainL = 1f; _prevCompGainR = 1f;
            // Fix #5: proper one-pole smoothing (~2ms time constant)
            _compGainSmooth = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.002));

            // 8. Reverb — comb filter
            int delaySamples = Math.Max(1, (int)(sampleRate * reverbMs / 1000f));
            _reverbBufL = new float[delaySamples];
            _reverbBufR = new float[delaySamples];
            _reverbPos = 0;
            _reverbFeedback = 0.20f;
            _reverbMix = reverbMix;

            // 9. Output limiter
            _limiterCeiling = 0.98f;
            _limiterAttackCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.0005)); // 0.5ms attack
            _limiterReleaseCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.100));
            _limiterEnvL = 0f; _limiterEnvR = 0f;
            _limiterGainL = 1f; _limiterGainR = 1f;
            // Fix #5: proper one-pole smoothing (~1ms time constant)
            _limiterGainSmooth = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.001));
        }

        /// <summary>
        /// Process interleaved float32 samples in-place. count = total float samples.
        /// </summary>
        public void Process(float[] samples, int count)
        {
            if (_channels >= 2)
            {
                for (int i = 0; i < count - 1; i += 2)
                {
                    float L = samples[i];
                    float R = samples[i + 1];

                    // 0. DC blocker (separate per channel)
                    L = _dcBlockerL.Transform(L);
                    R = _dcBlockerR.Transform(R);

                    // 1-5. EQ chain (separate L/R filter instances)
                    L = _rumbleL.Transform(L);
                    L = _res180L.Transform(L);
                    L = _res350L.Transform(L);
                    L = _lpfL.Transform(L);
                    if (_characterL != null) L = _characterL.Transform(L);

                    R = _rumbleR.Transform(R);
                    R = _res180R.Transform(R);
                    R = _res350R.Transform(R);
                    R = _lpfR.Transform(R);
                    if (_characterR != null) R = _characterR.Transform(R);

                    // 6. Subtle saturation
                    if (_satMix > 0f)
                    {
                        L = SoftSaturate(L, ref _satPrevOutL);
                        R = SoftSaturate(R, ref _satPrevOutR);
                    }

                    // 7. Stereo narrowing via mid-side
                    float mid = (L + R) * 0.5f;
                    float side = (L - R) * 0.5f;
                    side *= _stereoWidth;
                    L = mid + side;
                    R = mid - side;

                    // 8. Compressor — BEFORE reverb to control transients
                    L = Compress(L, ref _envL, ref _prevCompGainL);
                    R = Compress(R, ref _envR, ref _prevCompGainR);

                    // 9. Reverb (comb) — reduced gain to avoid clipping
                    float wetL = _reverbBufL[_reverbPos];
                    float wetR = _reverbBufR[_reverbPos];
                    _reverbBufL[_reverbPos] = L + wetL * _reverbFeedback;
                    _reverbBufR[_reverbPos] = R + wetR * _reverbFeedback;
                    _reverbPos = (_reverbPos + 1) % _reverbBufL.Length;

                    L = L * 0.80f + wetL * _reverbMix;
                    R = R * 0.80f + wetR * _reverbMix;

                    // 10. Brick-wall limiter — prevents any clipping
                    L = LimitOutput(L, ref _limiterEnvL, ref _limiterGainL);
                    R = LimitOutput(R, ref _limiterEnvR, ref _limiterGainR);

                    samples[i] = L;
                    samples[i + 1] = R;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    float s = samples[i];
                    s = _dcBlockerL.Transform(s);
                    s = _rumbleL.Transform(s);
                    s = _res180L.Transform(s);
                    s = _res350L.Transform(s);
                    s = _lpfL.Transform(s);
                    if (_characterL != null) s = _characterL.Transform(s);

                    if (_satMix > 0f)
                        s = SoftSaturate(s, ref _satPrevOutL);

                    s = Compress(s, ref _envL, ref _prevCompGainL);

                    // Reverb for mono
                    float wet = _reverbBufL[_reverbPos];
                    _reverbBufL[_reverbPos] = s + wet * _reverbFeedback;
                    _reverbPos = (_reverbPos + 1) % _reverbBufL.Length;
                    s = s * 0.80f + wet * _reverbMix;

                    s = LimitOutput(s, ref _limiterEnvL, ref _limiterGainL);

                    samples[i] = s;
                }
            }
        }

        /// <summary>
        /// Very gentle saturation for cabin warmth — tanh soft-clip with parallel mix.
        /// </summary>
        private float SoftSaturate(float input, ref float prevOut)
        {
            float saturated = (float)Math.Tanh(input * _satDrive) / (float)Math.Tanh(_satDrive);
            float output = input * (1f - _satMix) + saturated * _satMix;
            // Smooth to prevent zipper noise
            output = output * 0.8f + prevOut * 0.2f;
            prevOut = output;
            return output;
        }

        private float Compress(float input, ref float envelope, ref float prevGain)
        {
            float abs = Math.Abs(input);

            if (abs > envelope)
                envelope += (abs - envelope) * _compAttackCoeff;
            else
                envelope += (abs - envelope) * _compReleaseCoeff;

            float gainReduction = 1.0f;
            if (envelope > _compThreshold)
            {
                float overDb = 20f * (float)Math.Log10(envelope / _compThreshold);
                float gainDb = overDb * (1f / _compRatio - 1f);
                gainReduction = DbToLinear(gainDb);
            }

            // Fix #5: proper one-pole IIR smoothing with time-constant coefficient
            prevGain += (gainReduction - prevGain) * _compGainSmooth;
            return input * prevGain;
        }

        /// <summary>Smooth brick-wall limiter with soft-knee tanh instead of hard clip.</summary>
        private float LimitOutput(float input, ref float envelope, ref float prevGain)
        {
            float abs = Math.Abs(input);

            if (abs > envelope)
                envelope += (abs - envelope) * _limiterAttackCoeff;
            else
                envelope += (abs - envelope) * _limiterReleaseCoeff;

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
