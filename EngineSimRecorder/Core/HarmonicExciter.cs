using System;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Band-limited harmonic exciter for NAudio float32 pipelines.
    /// Bandpasses a copy of the signal through the "character band" (2-6 kHz),
    /// applies soft-clipping saturation to generate upper harmonics, then mixes
    /// the saturated signal back with the dry original.
    ///
    /// This adds "bite" and "presence" without broadband distortion — only the
    /// target frequency range gets excited.
    /// </summary>
    public sealed class HarmonicExciter
    {
        // Bandpass filter state (2nd-order IIR, per-channel)
        private float _bpX1L, _bpX2L, _bpY1L, _bpY2L;
        private float _bpX1R, _bpX2R, _bpY1R, _bpY2R;

        // Bandpass coefficients
        private readonly float _b0, _b1, _b2, _a1, _a2;

        // Saturation + mix
        private readonly float _drive;
        private readonly float _wetMix;   // 0-1, how much saturated signal to add
        private readonly float _dryMix;   // typically 1.0 (full dry passthrough)

        private readonly int _channels;

        /// <param name="sampleRate">Recording sample rate</param>
        /// <param name="channels">1 = mono, 2 = stereo</param>
        /// <param name="centerHz">Bandpass center frequency (1500-6000 Hz)</param>
        /// <param name="bandwidthHz">Bandpass bandwidth in Hz (higher = wider)</param>
        /// <param name="drive">Saturation drive (1.5-4.0, higher = more harmonics)</param>
        /// <param name="wetMix">Mix level for saturated signal (0.05-0.25 typical)</param>
        public HarmonicExciter(int sampleRate, int channels,
            float centerHz = 3000f,
            float bandwidthHz = 3000f,
            float drive = 2.5f,
            float wetMix = 0.15f)
        {
            _channels = channels;
            _drive = drive;
            _wetMix = wetMix;
            _dryMix = 1f;

            // Design 2nd-order bandpass filter (BPF with constant skirt gain)
            float omega = 2f * MathF.PI * centerHz / sampleRate;
            float sinW = MathF.Sin(omega);
            float cosW = MathF.Cos(omega);
            float alpha = sinW / (2f * MathF.Max(0.1f, centerHz / bandwidthHz)); // Q

            float a0 = 1f + alpha;

            _b0 = alpha / a0;
            _b1 = 0f;
            _b2 = -alpha / a0;
            _a1 = -2f * cosW / a0;
            _a2 = (1f - alpha) / a0;
        }

        /// <summary>Process interleaved float32 samples in-place.</summary>
        public void Process(float[] samples, int count)
        {
            if (_channels >= 2)
            {
                for (int i = 0; i < count - 1; i += 2)
                {
                    samples[i] = ProcessSample(samples[i],
                        ref _bpX1L, ref _bpX2L, ref _bpY1L, ref _bpY2L);
                    samples[i + 1] = ProcessSample(samples[i + 1],
                        ref _bpX1R, ref _bpX2R, ref _bpY1R, ref _bpY2R);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                    samples[i] = ProcessSample(samples[i],
                        ref _bpX1L, ref _bpX2L, ref _bpY1L, ref _bpY2L);
            }
        }

        /// <summary>Process a single left-channel sample (zero-allocation).</summary>
        public float ProcessLeft(float input) =>
            ProcessSample(input, ref _bpX1L, ref _bpX2L, ref _bpY1L, ref _bpY2L);

        /// <summary>Process a single right-channel sample (zero-allocation).</summary>
        public float ProcessRight(float input) =>
            ProcessSample(input, ref _bpX1R, ref _bpX2R, ref _bpY1R, ref _bpY2R);

        private float ProcessSample(float input,
            ref float x1, ref float x2, ref float y1, ref float y2)
        {
            // Bandpass filter: extract the target band
            float bandpassed = _b0 * input + _b1 * x1 + _b2 * x2 - _a1 * y1 - _a2 * y2;

            // Shift delay line
            x2 = x1; x1 = input;
            y2 = y1; y1 = bandpassed;

            // Soft-clip the bandpassed signal (tanh saturation)
            float saturated = MathF.Tanh(bandpassed * _drive);

            // Mix: dry + wet * saturated
            return input * _dryMix + saturated * _wetMix;
        }
    }
}
