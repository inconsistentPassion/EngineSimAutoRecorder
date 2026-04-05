namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Reusable PID controller. Stateless between calls — caller stores integral/prevError.
    /// </summary>
    public sealed class PidController
    {
        public double Kp { get; set; }
        public double Ki { get; set; }
        public double Kd { get; set; }

        private double _integral;
        private double _prevError;

        public PidController(double kp, double ki, double kd)
        {
            Kp = kp; Ki = ki; Kd = kd;
        }

        /// <summary>
        /// Compute PID output for one step.
        /// </summary>
        /// <param name="error">Target - measured</param>
        /// <param name="dtSeconds">Time since last call in seconds</param>
        /// <returns>Raw PID output (caller clamps to desired range)</returns>
        public double Update(double error, double dtSeconds)
        {
            _integral += error * dtSeconds;
            double derivative = dtSeconds > 0 ? (error - _prevError) / dtSeconds : 0;
            _prevError = error;
            return Kp * error + Ki * _integral + Kd * derivative;
        }

        public void Reset()
        {
            _integral = 0;
            _prevError = 0;
        }
    }
}
