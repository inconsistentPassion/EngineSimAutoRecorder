using System;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Envelope-following transient shaper for NAudio float32 pipelines.
    /// Detects the attack portion of each combustion cycle and boosts it,
    /// creating perceived "punch" without raising peak level.
    ///
    /// Works on interleaved stereo or mono float samples.
    /// </summary>
    public sealed class TransientShaper
    {
        // Envelope follower state (per-channel)
        private float _envL, _envR;
        private float _prevEnvL, _prevEnvR;
        private float _smoothedGainL, _smoothedGainR;

        private readonly float _attackCoeff;   // fast envelope follower for attack detection
        private readonly float _releaseCoeff;  // slower release to track sustain
        private readonly float _attackBoost;   // max linear gain multiplier for detected transients
        private readonly float _gainSmoothing; // smoothing coefficient for gain changes
        private readonly int _channels;

        /// <param name="sampleRate">Recording sample rate</param>
        /// <param name="channels">1 = mono, 2 = stereo</param>
        /// <param name="attackBoostDb">Max boost applied to transient portion in dB (0-6 dB typical)</param>
        /// <param name="attackMs">Envelope follower attack time in ms (1-5 ms for fast transient detection)</param>
        /// <param name="releaseMs">Envelope follower release time in ms (50-200 ms)</param>
        public TransientShaper(int sampleRate, int channels,
            float attackBoostDb = 3f,
            float attackMs = 2f,
            float releaseMs = 100f)
        {
            _channels = channels;
            _attackBoost = DbToLinear(attackBoostDb);
            _attackCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * attackMs / 1000.0));
            _releaseCoeff = 1f - (float)Math.Exp(-1.0 / (sampleRate * releaseMs / 1000.0));
            _gainSmoothing = 1f - (float)Math.Exp(-1.0 / (sampleRate * 0.003)); // 3ms gain smoothing

            _envL = 0f; _envR = 0f;
            _prevEnvL = 0f; _prevEnvR = 0f;
            _smoothedGainL = 1f; _smoothedGainR = 1f;
        }

        /// <summary>Process interleaved float32 samples in-place.</summary>
        public void Process(float[] samples, int count)
        {
            if (_channels >= 2)
            {
                for (int i = 0; i < count - 1; i += 2)
                {
                    samples[i] = ProcessSample(samples[i], ref _envL, ref _prevEnvL, ref _smoothedGainL);
                    samples[i + 1] = ProcessSample(samples[i + 1], ref _envR, ref _prevEnvR, ref _smoothedGainR);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                    samples[i] = ProcessSample(samples[i], ref _envL, ref _prevEnvL, ref _smoothedGainL);
            }
        }

        /// <summary>Process a single left-channel sample (use for per-sample stereo processing without allocation).</summary>
        public float ProcessLeft(float input) =>
            ProcessSample(input, ref _envL, ref _prevEnvL, ref _smoothedGainL);

        /// <summary>Process a single right-channel sample (use for per-sample stereo processing without allocation).</summary>
        public float ProcessRight(float input) =>
            ProcessSample(input, ref _envR, ref _prevEnvR, ref _smoothedGainR);

        private float ProcessSample(float input, ref float env, ref float prevEnv, ref float smoothedGain)
        {
            float abs = Math.Abs(input);

            // Fast attack, slower release envelope follower
            if (abs > env)
                env += (abs - env) * _attackCoeff;
            else
                env += (abs - env) * _releaseCoeff;

            // Rate of envelope change — positive = transient, zero/negative = sustain
            float envDelta = env - prevEnv;
            prevEnv = env;

            // Target gain: unity by default, boosted during transients only
            float targetGain = 1f;

            if (envDelta > 0f && env > 0.001f)
            {
                // How fast is the envelope rising relative to its current level?
                // This normalizes across amplitude — a small crack and a loud bang
                // both get boosted proportionally.
                float normalizedRate = Math.Min(1f, envDelta / (env + 0.0001f));
                targetGain = 1f + (_attackBoost - 1f) * normalizedRate;
            }
            // else: sustain or silence → unity gain (no attenuation)

            // Smooth gain to prevent clicks
            smoothedGain += (targetGain - smoothedGain) * _gainSmoothing;

            return input * smoothedGain;
        }

        private static float DbToLinear(float db) => (float)Math.Pow(10.0, db / 20.0);
    }
}
