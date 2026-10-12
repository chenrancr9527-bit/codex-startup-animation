using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CompanionStart
{
    public interface IStartupTarget
    {
        void Activate();
        IntPtr FindResponsiveWindow();
        void Reveal(IntPtr window);
    }

    public sealed class StartupSnapshot
    {
        public readonly bool Ready;
        public readonly IntPtr Window;
        public readonly string Error;
        public StartupSnapshot(bool ready, IntPtr window, string error)
        { Ready = ready; Window = window; Error = error; }
    }

    public sealed class StartupCoordinator : IDisposable
    {
        readonly IStartupTarget target;
        readonly object sync = new object();
        readonly ManualResetEvent cancel = new ManualResetEvent(false);
        StartupSnapshot snapshot = new StartupSnapshot(false, IntPtr.Zero, null);
        int started, disposed;
        public StartupCoordinator(IStartupTarget target) { this.target = target; }
        public StartupSnapshot Snapshot { get { lock (sync) return snapshot; } }
        public void Begin()
        {
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            var thread = new Thread(Run) { IsBackground = true, Name = "Codex startup readiness" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        void Run()
        {
            var watch = Stopwatch.StartNew();
            try
            {
                target.Activate();
                Program.Log("parallel_activation_complete elapsed=" + watch.Elapsed.TotalSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                IntPtr previous = IntPtr.Zero;
                int consecutive = 0;
                bool wasReady = false;
                while (!cancel.WaitOne(0))
                {
                    IntPtr candidate;
                    try { candidate = target.FindResponsiveWindow(); }
                    catch { candidate = IntPtr.Zero; }
                    consecutive = candidate != IntPtr.Zero ? (candidate == previous ? consecutive + 1 : 1) : 0;
                    previous = candidate;
                    // Three separate responsive observations prevent a transient window from ending the intro.
                    bool ready = consecutive >= 3;
                    lock (sync) snapshot = new StartupSnapshot(ready, candidate, null);
                    if (ready != wasReady)
                        Program.Log((ready ? "target_window_ready" : "target_window_not_ready") + " elapsed=" + watch.Elapsed.TotalSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " hwnd=" + candidate);
                    wasReady = ready;
                    if (cancel.WaitOne(150)) break;
                }
            }
            catch (Exception e)
            {
                lock (sync) snapshot = new StartupSnapshot(false, IntPtr.Zero, e.Message);
                Program.Log("parallel_activation_error=" + e.GetType().Name + ": " + e.Message);
            }
            finally { cancel.Dispose(); }
        }
        public void Reveal(IntPtr window) { target.Reveal(window); }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            try { cancel.Set(); } catch (ObjectDisposedException) { }
        }
    }

    public sealed class CodexStartupTarget : IStartupTarget
    {
        readonly string id;
        readonly string family;
        public CodexStartupTarget(string id)
        {
            this.id = id;
            int separator = id.IndexOf('!');
            family = separator > 0 ? id.Substring(0, separator) : id;
        }
        public void Activate()
        {
            Program.ActivationRequested = true;
            Program.ActivateApplication(id);
        }
        public IntPtr FindResponsiveWindow() { return NativeWindowProbe.FindForPackage(id, family); }
        public void Reveal(IntPtr window) { NativeWindowProbe.Reveal(window); }
    }

    public static class NativeWindowProbe
    {
        delegate bool EnumWindowProc(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowProc callback, IntPtr data);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out WindowRect rect);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process, ref uint length, StringBuilder id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetPackageFamilyName(IntPtr process, ref uint length, StringBuilder family);
        [StructLayout(LayoutKind.Sequential)] struct WindowRect { public int Left, Top, Right, Bottom; }

        public static IntPtr FindForProcess(uint processId)
        { return Find(delegate(uint pid) { return pid == processId; }); }
        public static IntPtr FindForPackage(string id, string family)
        { return Find(delegate(uint pid) { return MatchesPackage(pid, id, family); }); }
        static IntPtr Find(Predicate<uint> matches)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr window, IntPtr unused)
            {
                // Ignore helper/owned windows. A visible, titled top-level surface must answer WM_NULL.
                if (!IsWindowVisible(window) || GetWindow(window, 4) != IntPtr.Zero || GetWindowTextLength(window) == 0) return true;
                WindowRect rect;
                if (!GetWindowRect(window, out rect) || (!IsIconic(window) && (rect.Right - rect.Left < 240 || rect.Bottom - rect.Top < 120))) return true;
                uint pid;
                GetWindowThreadProcessId(window, out pid);
                if (!matches(pid)) return true;
                UIntPtr response;
                if (SendMessageTimeout(window, 0, UIntPtr.Zero, IntPtr.Zero, 0x0002 | 0x0020, 120, out response) == IntPtr.Zero) return true;
                found = window;
                return false;
            }, IntPtr.Zero);
            return found;
        }
        static bool MatchesPackage(uint pid, string id, string family)
        {
            IntPtr process = OpenProcess(0x1000, false, pid);
            if (process == IntPtr.Zero) return false;
            try
            {
                uint length = 512;
                var name = new StringBuilder((int)length);
                if (GetApplicationUserModelId(process, ref length, name) == 0 && name.Length > 0)
                    return string.Equals(name.ToString(), id, StringComparison.OrdinalIgnoreCase);
                length = 512;
                name.Clear();
                return GetPackageFamilyName(process, ref length, name) == 0 && string.Equals(name.ToString(), family, StringComparison.OrdinalIgnoreCase);
            }
            finally { CloseHandle(process); }
        }
        public static void Reveal(IntPtr window)
        {
            if (window == IntPtr.Zero || !IsWindow(window)) return;
            if (IsIconic(window)) ShowWindowAsync(window, 9);
            bool foreground = SetForegroundWindow(window);
            Program.Log("target_revealed hwnd=" + window + " foreground=" + foreground);
        }
    }
}
