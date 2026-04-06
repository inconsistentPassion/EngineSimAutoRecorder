using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Keyboard simulation for Engine Simulator via PostMessage.
    /// 
    /// Engine Sim uses Delta Studio (DX11 + Win32 WinProc), NOT GLFW.
    /// PostMessage with WM_KEYDOWN/WM_KEYUP works perfectly since
    /// the input goes through the standard Win32 message loop.
    ///
    /// For keys that need to be "held" (like throttle R, starter S),
    /// we send repeated WM_KEYDOWN messages to simulate the auto-repeat
    /// that Windows generates when a physical key is held down.
    /// </summary>
    public static class KeyboardSim
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP   = 0x0101;
        private const uint WM_CHAR    = 0x0102;

        // ?? Virtual key codes for Engine Sim ??????????????????????????

        public const ushort VK_S = 0x53; // Starter
        public const ushort VK_D = 0x44; // Dyno
        public const ushort VK_H = 0x48; // Hold RPM (Dyno mode)
        public const ushort VK_R = 0x52; // Throttle (hold to rev)
        public const ushort VK_A = 0x41; // Ignition
        public const ushort VK_W = 0x57; // Throttle tap (brief, for starting)

        // ?? Public API ????????????????????????????????????????????????

        /// <summary>Send a single WM_KEYDOWN to the window.</summary>
        public static void KeyDown(IntPtr hwnd, ushort vk)
        {
            uint scanCode = MapVirtualKey(vk, 0);
            IntPtr lParam = MakeLParam(1, scanCode, false, false);
            PostMessage(hwnd, WM_KEYDOWN, (IntPtr)vk, lParam);
        }

        /// <summary>Send a single WM_KEYUP to the window.</summary>
        public static void KeyUp(IntPtr hwnd, ushort vk)
        {
            uint scanCode = MapVirtualKey(vk, 0);
            IntPtr lParam = MakeLParam(1, scanCode, true, true);
            PostMessage(hwnd, WM_KEYUP, (IntPtr)vk, lParam);
        }

        /// <summary>
        /// Press and release a key (for toggle-style keys like A, D, H).
        /// </summary>
        public static void KeyPress(IntPtr hwnd, ushort vk, int holdMs = 120)
        {
            KeyDown(hwnd, vk);
            if (holdMs <= 20)
            {
                // For very short taps, busy-wait with Stopwatch for accuracy
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < holdMs) { /* spin */ }
            }
            else
            {
                Thread.Sleep(holdMs);
            }
            KeyUp(hwnd, vk);
        }

        /// <summary>
        /// Build the lParam for WM_KEYDOWN / WM_KEYUP.
        /// See: https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-keydown
        /// </summary>
        private static IntPtr MakeLParam(uint repeatCount, uint scanCode, bool isKeyUp, bool wasPreviouslyDown)
        {
            uint lp = repeatCount & 0xFFFF;       // bits 0-15: repeat count
            lp |= (scanCode & 0xFF) << 16;            // bits 16-23: scan code
            // bit 24: extended key (0 for normal keys)
            // bits 25-28: reserved
            if (wasPreviouslyDown) lp |= (1u << 30);  // bit 30: previous key state
            if (isKeyUp)          lp |= (1u << 31);   // bit 31: transition state
            return (IntPtr)lp;
        }

        // ?? Window helpers ????????????????????????????????????????????

        /// <summary>Find the main visible window for the given process ID.</summary>
        public static IntPtr FindMainWindow(int pid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out uint windowPid);
                if (windowPid == (uint)pid && IsWindowVisible(hWnd))
                {
                    var sb = new System.Text.StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    if (!string.IsNullOrWhiteSpace(sb.ToString()))
                    {
                        found = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>Bring the window to the foreground (optional, for visibility).</summary>
        public static IntPtr FocusWindow(IntPtr hwnd)
        {
            IntPtr prev = GetForegroundWindow();
            SetForegroundWindow(hwnd);
            Thread.Sleep(50);
            return prev;
        }

        public static void RestoreFocus(IntPtr previousHwnd)
        {
            if (previousHwnd != IntPtr.Zero)
                SetForegroundWindow(previousHwnd);
        }

        /// <summary>Minimize the window to the taskbar.</summary>
        public static void MinimizeWindow(IntPtr hwnd)
        {
            ShowWindow(hwnd, SW_MINIMIZE);
        }

        /// <summary>Restore a minimized window.</summary>
        public static void RestoreWindow(IntPtr hwnd)
        {
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
    }
}
