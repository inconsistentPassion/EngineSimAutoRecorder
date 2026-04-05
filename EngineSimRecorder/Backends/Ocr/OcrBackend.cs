using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using EngineSimRecorder.Core;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.LocalV4;
using Sdcb.PaddleInference;

namespace EngineSimRecorder.Backends.Ocr
{
    /// <summary>
    /// Non-invasive backend: reads RPM via screen OCR, controls throttle via keyboard simulation.
    /// No DLL injection, no admin rights needed. Window must be focused for SendInput to work.
    /// </summary>
    public sealed class OcrBackend : IEngineBackend
    {
        public string Name => "OCR (PaddleOCR + Keys)";

        // ── SendInput P/Invoke ─────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public INPUTUNION u; }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 2;
        private const ushort VK_W = 0x57;
        private const ushort VK_S = 0x53;
        private const ushort VK_RETURN = 0x0D;

        // ── State ─────────────────────────────────────────────────────

        private PaddleOcrAll? _ocr;
        private Rectangle _region;
        private DateTime _lastThrottleUpdate = DateTime.MinValue;

        public bool Initialize(RecorderConfig cfg, Action<string> log, CancellationToken ct)
        {
            _region = cfg.OcrRegion;
            log("Initializing PaddleOCR (V4 model, MKL-DNN)...");
            try
            {
                _ocr = new PaddleOcrAll(LocalFullModels.ChineseV4, PaddleDevice.Mkldnn())
                {
                    AllowRotateDetection = false,
                    Enable180Classification = false,
                };
                log("PaddleOCR ready.");
                return true;
            }
            catch (Exception ex)
            {
                log($"PaddleOCR init failed: {ex.Message}");
                return false;
            }
        }

        public double? ReadRpm()
        {
            if (_ocr == null) return null;
            try
            {
                using var bmp = new Bitmap(_region.Width, _region.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(_region.Location, Point.Empty, _region.Size);

                using var mat = BitmapConverter.ToMat(bmp);
                var result = _ocr.Run(mat);

                string text = Regex.Replace(result.Text?.Trim() ?? "", @"\D", "");
                if (text.Length > 0 && double.TryParse(text, out double rpm))
                    return rpm;
            }
            catch { }
            return null;
        }

        public void SetThrottle(double throttle)
        {
            // In OCR mode we simulate W (throttle up) / S (throttle down) keys.
            // The game's own throttle response maps key-hold-time → throttle position.
            // We approximate by holding W proportional to throttle value.
            //
            // The game updates throttle continuously while W is held.
            // We press-and-release W in a duty cycle: hold for (throttle * 100)ms, release for remainder.

            int holdMs = (int)(Math.Clamp(throttle, 0, 1) * 100);
            int cycleMs = 100; // 10Hz cycle

            if (throttle > 0.01)
            {
                // Press W for throttle up
                PressKey(VK_W, Math.Min(holdMs, cycleMs));
            }
            else if (throttle < -0.01)
            {
                // Press S for throttle down (negative PID output)
                PressKey(VK_S, Math.Min(-holdMs, cycleMs));
            }
            // else: near zero, don't press anything
        }

        public void StartEngine(Action<string> log, CancellationToken ct)
        {
            log("Starting engine (keyboard mode)...");
            log("Pressing Enter to crank engine...");
            PressKey(VK_RETURN, 200);

            // Hold Enter for a few seconds to start the engine
            DateTime start = DateTime.UtcNow;
            while (!ct.IsCancellationRequested)
            {
                double? rpm = ReadRpm();
                if (rpm.HasValue && rpm.Value > 200)
                {
                    log($"Engine started — RPM: {rpm.Value:F0}");
                    break;
                }
                if ((DateTime.UtcNow - start).TotalSeconds > 10)
                {
                    log("Engine may not have started — continuing anyway");
                    break;
                }
                // Keep pressing Enter periodically
                if ((DateTime.UtcNow - start).TotalMilliseconds % 1000 < 200)
                    PressKey(VK_RETURN, 150);
                Thread.Sleep(200, ct);
            }
        }

        public void StopEngine()
        {
            // Release all keys
            ReleaseKey(VK_W);
            ReleaseKey(VK_S);
        }

        public void Dispose()
        {
            _ocr?.Dispose();
            _ocr = null;
        }

        // ── Key simulation ────────────────────────────────────────────

        private static void PressKey(ushort vk, int durationMs)
        {
            var down = new INPUT { type = INPUT_KEYBOARD };
            down.u.ki = new KEYBDINPUT { wVk = vk };
            SendInput(1, new[] { down }, Marshal.SizeOf<INPUT>());

            if (durationMs > 0)
            {
                Thread.Sleep(durationMs);
                ReleaseKey(vk);
            }
        }

        private static void ReleaseKey(ushort vk)
        {
            var up = new INPUT { type = INPUT_KEYBOARD };
            up.u.ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP };
            SendInput(1, new[] { up }, Marshal.SizeOf<INPUT>());
        }
    }
}
