using System;
using NAudio.Dsp;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Estimates engine RPM from audio by finding the dominant frequency
  /// using FFT. Engine Sim's audio output has a fundamental frequency
    /// that directly correlates with crankshaft speed.
    ///
    /// For a 4-stroke engine: RPM = frequency * 60 / (cylinders / 2)
    /// For simplicity we expose the dominant frequency and let the
    /// caller configure the conversion factor.
    /// </summary>
    public sealed class AudioRpmEstimator
    {
   private readonly int _fftSize;        // must be power of 2
        private readonly int _sampleRate;
        private readonly float[] _buffer;
    private int _bufferPos;
     private readonly object _lock = new object();

   // Configurable: how many firing events per revolution
     // 4-cyl 4-stroke = 2 firings/rev, so RPM = freq * 60 / 2
        // V8 4-stroke = 4 firings/rev, so RPM = freq * 60 / 4
        // Default assumes 1 firing/rev (user can adjust)
        public double FiringsPerRevolution { get; set; } = 1.0;

  // Frequency band to search for the engine fundamental
        public double MinFreqHz { get; set; } = 20;   // lowest engine idle ~20 Hz
  public double MaxFreqHz { get; set; } = 500; // ~30000 RPM range

       private double _lastRpm;

        public AudioRpmEstimator(int sampleRate = 44100, int fftSize = 4096)
     {
            _sampleRate = sampleRate;
   _fftSize = fftSize;
  _buffer = new float[fftSize];
    _bufferPos = 0;
        }

        /// <summary>
        /// Feed audio samples (mono float, -1..1) into the estimator.
     /// When enough samples accumulate, an FFT is automatically run.
      /// </summary>
      public void AddSamples(float[] samples, int offset, int count)
        {
        lock (_lock)
      {
     for (int i = 0; i < count; i++)
   {
     _buffer[_bufferPos++] = samples[offset + i];
    if (_bufferPos >= _fftSize)
    {
   RunFft();
        _bufferPos = 0;
  }
 }
     }
     }

    /// <summary>
 /// Get the latest estimated RPM. Returns 0 if not enough data yet.
   /// </summary>
     public double GetRpm()
     {
    lock (_lock)
     {
         return _lastRpm;
  }
    }

        private void RunFft()
        {
    // Prepare complex array for NAudio FFT
 int m = (int)Math.Log2(_fftSize);
  var complex = new Complex[_fftSize];

     for (int i = 0; i < _fftSize; i++)
            {
 // Apply Hann window to reduce spectral leakage
      double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (_fftSize - 1)));
  complex[i].X = (float)(_buffer[i] * window);
               complex[i].Y = 0;
     }

   FastFourierTransform.FFT(true, m, complex);

         // Find dominant frequency in the engine range
   double binWidth = (double)_sampleRate / _fftSize;
   int minBin = Math.Max(1, (int)(MinFreqHz / binWidth));
    int maxBin = Math.Min(_fftSize / 2 - 1, (int)(MaxFreqHz / binWidth));

     double maxMagnitude = 0;
     int maxIndex = minBin;

     for (int i = minBin; i <= maxBin; i++)
            {
        double mag = Math.Sqrt(complex[i].X * complex[i].X + complex[i].Y * complex[i].Y);
 if (mag > maxMagnitude)
      {
     maxMagnitude = mag;
   maxIndex = i;
      }
     }

     // Parabolic interpolation for sub-bin accuracy
    double freq;
            if (maxIndex > minBin && maxIndex < maxBin)
            {
          double magLeft = Math.Sqrt(complex[maxIndex - 1].X * complex[maxIndex - 1].X +
     complex[maxIndex - 1].Y * complex[maxIndex - 1].Y);
         double magRight = Math.Sqrt(complex[maxIndex + 1].X * complex[maxIndex + 1].X +
          complex[maxIndex + 1].Y * complex[maxIndex + 1].Y);
   double delta = 0.5 * (magRight - magLeft) / (2.0 * maxMagnitude - magLeft - magRight);
   freq = (maxIndex + delta) * binWidth;
  }
            else
     {
          freq = maxIndex * binWidth;
            }

    // Convert frequency to RPM
    // RPM = (freq / firingsPerRevolution) * 60
     double rpm = (freq / FiringsPerRevolution) * 60.0;

    // Basic sanity check and smoothing
      if (rpm < 100) rpm = 0; // below idle, probably noise
if (maxMagnitude < 0.001) rpm = 0; // too quiet, no engine sound

  // Simple exponential smoothing to reduce jitter
if (_lastRpm > 0 && rpm > 0)
           _lastRpm = _lastRpm * 0.7 + rpm * 0.3;
          else
      _lastRpm = rpm;
        }
    }
}
