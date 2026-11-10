using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Autotests run while the developer keeps working: the game window must not grab the focus. Unity activates its
    /// window on start whatever the launcher asks, so from before the splash screen a background thread checks 20 times a
    /// second: if the game has the focus, it gives it back to the window the developer was in (Tools/launch_quiet.py passes
    /// it as -return-focus) and moves behind every other window — still on screen, as it has to render for screenshots.
    /// Only with -autotest and without -autotest-front; Windows player only.
    /// </summary>
    public static class DevWindow
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int SwShowNoActivate = 4;
        const uint SwpNoSize = 0x0001, SwpNoMove = 0x0002, SwpNoActivate = 0x0010;
        static readonly IntPtr HwndBottom = new(1);

        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr param);

        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern void SwitchToThisWindow(IntPtr hwnd, bool altTab);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint attach, uint to, bool on);

        static IntPtr s_Own;

        /// <summary>The player's main (top-level, unowned) window.</summary>
        static IntPtr Find()
        {
            uint me = GetCurrentProcessId();
            IntPtr found = IntPtr.Zero;
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid != me || GetWindow(hwnd, 4 /* GW_OWNER */) != IntPtr.Zero) return true;
                found = hwnd;
                return !IsWindowVisible(hwnd); // keep looking for a visible one
            }, IntPtr.Zero);
            return found;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-autotest") < 0 || Array.IndexOf(args, "-autotest-front") >= 0) return;
            int i = Array.IndexOf(args, "-return-focus");
            long returnTo = i >= 0 && i + 1 < args.Length && long.TryParse(args[i + 1], out var h) ? h : 0;
            new Thread(() =>
            {
                while (true)
                {
                    StayBehind(returnTo);
                    Thread.Sleep(50);
                }
            }) { IsBackground = true, Name = "DevWindow" }.Start();
        }

        /// <summary>If this window has the focus, hand it to <paramref name="returnTo"/> and step back (plain Win32, any thread).</summary>
        static void StayBehind(long returnTo)
        {
            if (s_Own == IntPtr.Zero || !IsWindow(s_Own)) s_Own = Find();
            if (s_Own == IntPtr.Zero) return;
            if (IsIconic(s_Own)) StepBack(); // started minimized: back on screen (behind the others) to render
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return;
            GetWindowThreadProcessId(foreground, out uint pid);
            if (pid != GetCurrentProcessId()) return;
            // A foreground process may hand the focus to any window.
            var target = new IntPtr(returnTo);
            if (returnTo != 0 && IsWindow(target))
            {
                // From a thread without windows: share the input state of the foreground thread for the switch.
                uint fgThread = GetWindowThreadProcessId(foreground, out _), me = GetCurrentThreadId();
                bool attached = fgThread != me && AttachThreadInput(me, fgThread, true);
                if (!SetForegroundWindow(target)) SwitchToThisWindow(target, true);
                if (attached) AttachThreadInput(me, fgThread, false);
            }
            StepBack();
        }

        static void StepBack()
        {
            ShowWindow(s_Own, SwShowNoActivate);
            SetWindowPos(s_Own, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
#endif
    }
}
