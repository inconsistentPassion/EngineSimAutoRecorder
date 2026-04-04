using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using Tesseract;

namespace EngineSimRecorder
{
    /// <summary>
    /// Engine Simulator Auto-Recorder
    ///
    /// Workflow:
    ///   1. A background task loops through each target RPM.
    ///   2. For every RPM target the PID controller synthesises keystrokes
    ///      (W = throttle up, S = throttle down) to hold the engine at that RPM.
    ///   3. Once the RPM is stable for the configured hold time, a WASAPI-loopback
    ///      recording is captured and saved as a WAV file.
    ///   4. Tesseract OCR reads the RPM value from a configurable screen region.
    /// </summary>
    public partial class Form1 : Form
    {
        // ── Win32 SendInput helpers ──────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy, mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL, wParamH;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 2;
        private const ushort VK_W = 0x57;
        private const ushort VK_S = 0x53;

        // ── State ────────────────────────────────────────────────────────────
        private CancellationTokenSource? _cts;
        private Task? _workerTask;

        // ── Constructor ──────────────────────────────────────────────────────
        public Form1()
        {
            InitializeComponent();
            // Populate default RPM targets
            foreach (int rpm in new[] { 800, 1500, 3000, 4500, 6000, 7500 })
                lstTargetRpms.Items.Add(rpm);
        }

        // ── UI event handlers ────────────────────────────────────────────────
        private void btnBrowseOutput_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Select output folder for WAV recordings" };
            if (dlg.ShowDialog() == DialogResult.OK)
                txtOutputDir.Text = dlg.SelectedPath;
        }

        private void btnBrowseTess_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Select tessdata folder (contains eng.traineddata)" };
            if (dlg.ShowDialog() == DialogResult.OK)
                txtTessData.Text = dlg.SelectedPath;
        }

        private void btnAddRpm_Click(object sender, EventArgs e)
        {
            int rpm = (int)numRpmList.Value;
            if (!lstTargetRpms.Items.Contains(rpm))
                lstTargetRpms.Items.Add(rpm);
        }

        private void btnRemoveRpm_Click(object sender, EventArgs e)
        {
            if (lstTargetRpms.SelectedItem != null)
                lstTargetRpms.Items.Remove(lstTargetRpms.SelectedItem);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (lstTargetRpms.Items.Count == 0)
            {
                MessageBox.Show("Add at least one target RPM before starting.", "No targets", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputDir = txtOutputDir.Text.Trim();
            string tessData = txtTessData.Text.Trim();

            if (!Directory.Exists(tessData))
            {
                MessageBox.Show($"tessdata folder not found:\n{tessData}", "Missing tessdata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Directory.CreateDirectory(outputDir);

            var targets = new List<int>();
            foreach (var item in lstTargetRpms.Items)
                targets.Add(Convert.ToInt32(item));

            var cfg = new RecorderConfig
            {
                OutputDir = outputDir,
                TessDataPath = tessData,
                OcrRegion = new Rectangle((int)numOcrX.Value, (int)numOcrY.Value,
                                          (int)numOcrW.Value, (int)numOcrH.Value),
                TargetRpms = targets,
                RpmTolerance = (int)numRpmTol.Value,
                HoldSeconds = (int)numHoldSec.Value,
                Kp = (double)numKp.Value,
                Ki = (double)numKi.Value,
                Kd = (double)numKd.Value,
            };

            btnStart.Enabled = false;
            btnStop.Enabled = true;
            pbarProgress.Value = 0;
            pbarProgress.Maximum = targets.Count;
            txtLog.Clear();

            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => RunAsync(cfg, _cts.Token));
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cts?.Cancel();
        }

        // ── Thread-safe UI helpers ────────────────────────────────────────────
        private void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (InvokeRequired)
                Invoke(new Action(() => AppendLog(line)));
            else
                AppendLog(line);
        }

        private void AppendLog(string line)
        {
            txtLog.AppendText(line + Environment.NewLine);
            txtLog.ScrollToCaret();
        }

        private void SetStatus(string text) =>
            Invoke(new Action(() => lblStatus.Text = text));

        private void SetRpm(string text) =>
            Invoke(new Action(() => lblCurrentRpm.Text = text));

        private void IncProgress() =>
            Invoke(new Action(() => pbarProgress.Value = Math.Min(pbarProgress.Value + 1, pbarProgress.Maximum)));

        private void ResetControls() =>
            Invoke(new Action(() =>
            {
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                lblStatus.Text = "Done.";
            }));

        // ── Main worker ───────────────────────────────────────────────────────
        private void RunAsync(RecorderConfig cfg, CancellationToken ct)
        {
            try
            {
                using var tess = new TesseractEngine(cfg.TessDataPath, "eng", EngineMode.Default);
                // Restrict Tesseract to digits only for speed
                tess.SetVariable("tessedit_char_whitelist", "0123456789");

                for (int i = 0; i < cfg.TargetRpms.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    int targetRpm = cfg.TargetRpms[i];
                    Log($"── Target {i + 1}/{cfg.TargetRpms.Count}: {targetRpm} RPM ──");
                    SetStatus($"Approaching {targetRpm} RPM…");

                    HoldRpm(tess, cfg, targetRpm, ct);
                    if (ct.IsCancellationRequested) break;

                    string wavPath = Path.Combine(cfg.OutputDir, $"rpm_{targetRpm}.wav");
                    Log($"Recording → {wavPath}");
                    SetStatus($"Recording at {targetRpm} RPM…");
                    RecordWasapi(wavPath, cfg, tess, targetRpm, ct);
                    IncProgress();
                    Log($"Saved: {wavPath}");
                }
            }
            catch (OperationCanceledException)
            {
                Log("Stopped by user.");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                Invoke(new Action(() =>
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
            finally
            {
                ResetControls();
            }
        }

        // ── PID-based RPM holding ─────────────────────────────────────────────
        private void HoldRpm(TesseractEngine tess, RecorderConfig cfg, int targetRpm, CancellationToken ct)
        {
            double integral = 0;
            double prevError = 0;
            const int loopMs = 200; // OCR polling interval
            DateTime stableStart = DateTime.MinValue;
            bool wasStable = false;

            while (!ct.IsCancellationRequested)
            {
                int currentRpm = ReadRpm(tess, cfg.OcrRegion);
                SetRpm($"RPM: {(currentRpm < 0 ? "???" : currentRpm.ToString())}");

                if (currentRpm < 0)
                {
                    Thread.Sleep(loopMs);
                    continue;
                }

                double error = targetRpm - currentRpm;
                integral += error * (loopMs / 1000.0);
                double derivative = (error - prevError) / (loopMs / 1000.0);
                prevError = error;
                double output = cfg.Kp * error + cfg.Ki * integral + cfg.Kd * derivative;

                // Map PID output to throttle keystrokes
                ApplyThrottle(output);

                bool stable = Math.Abs(error) <= cfg.RpmTolerance;
                if (stable)
                {
                    if (!wasStable) stableStart = DateTime.UtcNow;
                    wasStable = true;
                    double heldFor = (DateTime.UtcNow - stableStart).TotalSeconds;
                    SetStatus($"Stable at {currentRpm} RPM – hold {heldFor:F1}s / {cfg.HoldSeconds}s");
                    if (heldFor >= cfg.HoldSeconds) break;
                }
                else
                {
                    wasStable = false;
                    stableStart = DateTime.MinValue;
                }

                Thread.Sleep(loopMs);
            }
        }

        // ── Throttle control via SendInput ────────────────────────────────────
        private static void ApplyThrottle(double pidOutput)
        {
            // pidOutput > 0 → need more throttle (press W)
            // pidOutput < 0 → need less throttle (press S)
            int holdMs = (int)Math.Min(Math.Abs(pidOutput) * 0.5, 200);
            if (holdMs < 10) return;

            ushort key = pidOutput > 0 ? VK_W : VK_S;
            PressKey(key, holdMs);
        }

        private static void PressKey(ushort vk, int durationMs)
        {
            var down = new INPUT { type = INPUT_KEYBOARD };
            down.u.ki = new KEYBDINPUT { wVk = vk };
            SendInput(1, new[] { down }, Marshal.SizeOf<INPUT>());
            Thread.Sleep(durationMs);
            var up = new INPUT { type = INPUT_KEYBOARD };
            up.u.ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP };
            SendInput(1, new[] { up }, Marshal.SizeOf<INPUT>());
        }

        // ── Tesseract OCR: read RPM from screen ───────────────────────────────
        private static int ReadRpm(TesseractEngine tess, Rectangle region)
        {
            try
            {
                using var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(region.Location, Point.Empty, region.Size);

                // Pre-process: grayscale + threshold for better OCR accuracy
                using var processed = PreProcess(bmp);
                using var pix = BitmapToPix(processed);
                using var page = tess.Process(pix);
                string text = page.GetText().Trim();
                text = Regex.Replace(text, @"\D", "");
                return text.Length > 0 ? int.Parse(text) : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static Bitmap PreProcess(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            var srcData = src.LockBits(
                new Rectangle(0, 0, src.Width, src.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            var dstData = dst.LockBits(
                new Rectangle(0, 0, dst.Width, dst.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                int byteCount = srcData.Stride * src.Height;
                byte[] srcPixels = new byte[byteCount];
                byte[] dstPixels = new byte[byteCount];
                Marshal.Copy(srcData.Scan0, srcPixels, 0, byteCount);

                // BGRA layout: B=0, G=1, R=2, A=3
                for (int i = 0; i < byteCount; i += 4)
                {
                    byte b = srcPixels[i];
                    byte g = srcPixels[i + 1];
                    byte r = srcPixels[i + 2];
                    // Luminance-weighted grayscale, then hard-threshold → binary
                    int gray = (int)(r * 0.299 + g * 0.587 + b * 0.114);
                    byte binary = (byte)(gray > 128 ? 255 : 0);
                    dstPixels[i] = binary;
                    dstPixels[i + 1] = binary;
                    dstPixels[i + 2] = binary;
                    dstPixels[i + 3] = 255;
                }

                Marshal.Copy(dstPixels, 0, dstData.Scan0, byteCount);
            }
            finally
            {
                src.UnlockBits(srcData);
                dst.UnlockBits(dstData);
            }

            return dst;
        }

        private static Pix BitmapToPix(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            return Pix.LoadFromMemory(ms.ToArray());
        }

        // ── WASAPI loopback recording ─────────────────────────────────────────
        private void RecordWasapi(string outputPath, RecorderConfig cfg,
                                   TesseractEngine tess, int targetRpm, CancellationToken ct)
        {
            using var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

            using var writer = new WaveFileWriter(outputPath, capture.WaveFormat);
            var recordingEnd = new ManualResetEventSlim(false);
            DateTime startTime = DateTime.UtcNow;

            capture.DataAvailable += (s, e) =>
            {
                if (e.BytesRecorded > 0)
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
            };

            capture.RecordingStopped += (s, e) => recordingEnd.Set();

            capture.StartRecording();
            Log($"WASAPI capture started (hold {cfg.HoldSeconds}s)");

            // Keep recording while stable; abort if RPM drifts badly
            while (!ct.IsCancellationRequested)
            {
                double elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                if (elapsed >= cfg.HoldSeconds) break;

                int currentRpm = ReadRpm(tess, cfg.OcrRegion);
                SetRpm($"RPM: {(currentRpm < 0 ? "???" : currentRpm.ToString())}");
                if (currentRpm >= 0 && Math.Abs(currentRpm - targetRpm) > cfg.RpmTolerance * 2)
                {
                    Log($"RPM drifted to {currentRpm} – rebalancing…");
                    HoldRpm(tess, cfg, targetRpm, ct);
                    // Restart the hold timer
                    startTime = DateTime.UtcNow;
                }

                Thread.Sleep(200);
            }

            capture.StopRecording();
            recordingEnd.Wait(TimeSpan.FromSeconds(5));
        }

        // ── Config DTO ────────────────────────────────────────────────────────
        private sealed class RecorderConfig
        {
            public string OutputDir { get; set; } = "recordings";
            public string TessDataPath { get; set; } = "tessdata";
            public Rectangle OcrRegion { get; set; } = new Rectangle(860, 45, 160, 40);
            public List<int> TargetRpms { get; set; } = new();
            public int RpmTolerance { get; set; } = 50;
            public int HoldSeconds { get; set; } = 5;
            public double Kp { get; set; } = 0.0005;
            public double Ki { get; set; } = 0.00001;
            public double Kd { get; set; } = 0.0005;
        }
    }
}
