using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CompanionStart
{
    public static class Program
    {
        public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
        public static double Duration = 15.0;
        public static string Target = "OpenAI.Codex_2p2nqsd0c76g0!App";
        public static bool Sound = true;
        public static bool Preview;
        public static volatile bool ActivationRequested;
        static readonly object logSync = new object();
        static Mutex gate;

        [STAThread]
        public static int Main(string[] args)
        {
            LoadSettings();
            #if PREVIEW
            Preview = true;
            #else
            Preview = Array.IndexOf(args, "--preview") >= 0;
            #endif
            if (Array.IndexOf(args, "--silent") >= 0) Sound = false;
            if (args.Length >= 2 && args[0] == "--render")
            {
                ExportFrames(args[1], args.Length > 2 ? int.Parse(args[2]) : 30);
                return 0;
            }
            if (args.Length >= 3 && (args[0] == "--still" || args[0] == "--still-wait" || args[0] == "--still-error"))
            {
                ExportStill(double.Parse(args[1], CultureInfo.InvariantCulture), args[2], args[0]);
                return 0;
            }
            bool owner;
            gate = new Mutex(true, "Local\\CodexCompanionStart", out owner);
            if (!owner) return 0;
            try
            {
                var app = new Application();
                var window = new StartWindow(args);
                app.Run(window);
                return 0;
            }
            catch (Exception e)
            {
                Log("animation_error=" + e.GetType().Name + ": " + e.Message);
                if (!Preview && !ActivationRequested) ActivateTarget();
                return 1;
            }
            finally { gate.ReleaseMutex(); gate.Dispose(); }
        }

        static void LoadSettings()
        {
            string file = Path.Combine(Root, "启动设置.ini");
            if (!File.Exists(file)) return;
            foreach (var line in File.ReadAllLines(file))
            {
                int split = line.IndexOf('=');
                if (split < 1 || line.TrimStart().StartsWith("#")) continue;
                string key = line.Substring(0, split).Trim();
                string value = line.Substring(split + 1).Trim();
                if (key == "AppUserModelId" && value.Length > 0) Target = value;
                if (key == "Sound") Sound = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                double seconds;
                if (key == "Duration" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && !double.IsNaN(seconds) && !double.IsInfinity(seconds))
                    Duration = Math.Max(5, Math.Min(20, seconds));
            }
        }

        public static void Log(string text)
        {
            lock (logSync)
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexCompanionStart");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "launcher.log");
                if (File.Exists(path) && new FileInfo(path).Length > 32768) File.WriteAllText(path, "");
                File.AppendAllText(path, DateTime.Now.ToString("s") + " " + text + Environment.NewLine);
            }
            catch { }
        }

        public static bool ActivateTarget()
        {
            try
            {
                ActivationRequested = true;
                ActivateApplication(Target);
                return true;
            }
            catch (Exception e)
            {
                Log("activation_error=" + e.Message);
                MessageBox.Show("暂时没有打开 Codex。请确认它仍安装在这台电脑上。\n你可以先用原来的图标打开，动画和设置文件都已保留。", "Codex 启动提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
        }

        public static void ActivateApplication(string id)
        {
            var manager = (IApplicationActivationManager)new ApplicationActivationManager();
            try
            {
                uint pid;
                // AO_NOERRORUI: startup errors are shown by our own waiting surface.
                int hr = manager.ActivateApplication(id, "", 2, out pid);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                Log("activation_ok target=" + id + " pid=" + pid);
            }
            finally { Marshal.ReleaseComObject(manager); }
        }

        [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
        class ApplicationActivationManager { }
        [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IApplicationActivationManager
        {
            [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint pid);
            [PreserveSig] int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint pid);
            [PreserveSig] int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr items, out uint pid);
        }

        static void ExportFrames(string folder, int fps)
        {
            Directory.CreateDirectory(folder);
            var scene = new Scene();
            scene.Measure(new Size(1280, 720));
            scene.Arrange(new Rect(0, 0, 1280, 720));
            int count = (int)Math.Ceiling(Duration * fps);
            for (int i = 0; i < count; i++) Render(scene, (double)i / fps, Path.Combine(folder, i.ToString("D5") + ".png"));
            File.WriteAllText(Path.Combine(folder, "frames.txt"), "frames=" + count + "\nfps=" + fps + "\nduration=" + Duration.ToString(CultureInfo.InvariantCulture));
        }
        static void ExportStill(double time, string file, string mode)
        {
            var scene = new Scene();
            if (mode != "--still")
            {
                scene.StartupMonitored = true;
                scene.Waiting = true;
                scene.ActivityTime = time;
                scene.StartupError = mode == "--still-error" ? "diagnostic" : null;
                time = 14.2 / 15.0 * Duration;
            }
            scene.Measure(new Size(1920, 1080));
            scene.Arrange(new Rect(0, 0, 1920, 1080));
            Render(scene, time, file);
        }
        static void Render(Scene scene, double time, string file)
        {
            scene.T = time / Duration * 15.0;
            scene.Export = true;
            scene.InvalidateVisual();
            scene.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)scene.ActualWidth, (int)scene.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(scene);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(file)) encoder.Save(stream);
        }
    }

    public class StartWindow : Window
    {
        readonly Scene scene = new Scene();
        readonly Stopwatch timer = new Stopwatch();
        SoundPlayer player;
        Window solidHost;
        StartupCoordinator startup;
        bool done, animationFinished, waitingLogged, coverRendered, startupStarted;
        double handoffStart = -1;
        double hold = -1;
        public StartWindow(string[] args) : this(args, null) { }
        public StartWindow(string[] args, StartupCoordinator startupOverride)
        {
            Title = "Codex 启动动画";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = true;
            Left = 0; Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            if (Array.IndexOf(args, "--windowed") >= 0)
            {
                Width = 1280; Height = 720;
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
            }
            foreach (string arg in args)
                if (arg.StartsWith("--hold=")) { hold = double.Parse(arg.Substring(7), CultureInfo.InvariantCulture); Program.Preview = true; }
            if (!Program.Preview) startup = startupOverride ?? new StartupCoordinator(new CodexStartupTarget(Program.Target));
            scene.StartupMonitored = !Program.Preview;
            Content = scene;
            KeyDown += OnKey;
            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                Point p = e.GetPosition(scene);
                if (scene.IsSkipPoint(p)) RequestEnter();
            };
            Loaded += delegate
            {
                Activate(); Focus();
                timer.Start();
                Program.Log("start preview=" + Program.Preview + " hold=" + hold.ToString(CultureInfo.InvariantCulture));
                // Keep the transparent logo stage clear. Activation starts after the opaque host paints.
                if (Program.Sound && hold < 0)
                {
                    try { player = new SoundPlayer(Path.Combine(Program.Root, "启动音.wav")); player.Load(); player.Play(); }
                    catch { player = null; }
                }
                scene.Muted = !Program.Sound;
                CompositionTarget.Rendering += Tick;
            };
            Closed += delegate
            {
                CompositionTarget.Rendering -= Tick;
                if (player != null) { player.Stop(); player.Dispose(); }
                if (startup != null) startup.Dispose();
                if (!done)
                {
                    done = true;
                    if (solidHost != null) solidHost.Close();
                    Program.Log("launcher_closed elapsed=" + Seconds(timer.Elapsed.TotalSeconds));
                }
            };
        }
        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && animationFinished) { Finish(false); e.Handled = true; }
            else if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Space) { RequestEnter(); e.Handled = true; }
            if (e.Key == Key.M)
            {
                scene.Muted = true;
                if (player != null) player.Stop();
                scene.InvalidateVisual();
            }
        }
        void Tick(object sender, EventArgs e)
        {
            if (done) return;
            double elapsed = timer.Elapsed.TotalSeconds;
            double t = hold >= 0 ? hold : elapsed / Program.Duration * 15.0;
            if (startup != null)
            {
                StartupSnapshot state = startup.Snapshot;
                scene.TargetReady = state.Ready;
                scene.ActivityTime = elapsed;
                scene.StartupError = animationFinished ? state.Error : null;
                if (handoffStart >= 0)
                {
                    // Readiness can be lost if the target window closes during the transition.
                    if (!state.Ready) handoffStart = -1;
                    else
                    {
                        double p = Math.Max(0, Math.Min(1, (elapsed - handoffStart) / .3));
                        t = 14.65 + .35 * p;
                        scene.Waiting = false;
                        if (p >= 1) { Finish(true); return; }
                    }
                }
                if (handoffStart < 0 && animationFinished)
                {
                    t = 14.2;
                    scene.Waiting = true;
                    if (state.Ready) BeginHandoff();
                }
                else if (!animationFinished && elapsed >= Program.Duration)
                {
                    // A ready target has already been revealed through the final cinematic fade.
                    EndAnimation();
                    if (state.Ready) { Finish(true); return; }
                    t = 14.2;
                    scene.Waiting = true;
                }
                else if (!state.Ready) t = Math.Min(t, 14.6);
            }
            scene.T = t;
            if (scene.Waiting && !waitingLogged)
            {
                waitingLogged = true;
                Program.Log("waiting_begin elapsed=" + Seconds(elapsed));
            }
            if (scene.T >= 5.22 && solidHost == null) PromoteWindow();
            if (solidHost != null)
            {
                double fade = Math.Max(0, Math.Min(1, (scene.T - 14.65) / .35));
                solidHost.Opacity = 1 - fade * fade * (3 - 2 * fade);
            }
            scene.InvalidateVisual();
            if (startup == null && hold < 0 && elapsed >= Program.Duration) Finish(false);
        }
        void PromoteWindow()
        {
            // The desktop overlay hands off to a regular, GPU-composited window.
            Content = null;
            solidHost = new Window {
                Title = Title, WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, Topmost = true,
                ShowInTaskbar = true, Left = Left, Top = Top,
                Width = Width, Height = Height,
                Background = new SolidColorBrush(Color.FromRgb(4, 11, 13)),
                Content = scene
            };
            solidHost.KeyDown += OnKey;
            solidHost.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                Point p = e.GetPosition(scene);
                if (scene.IsSkipPoint(p)) RequestEnter();
            };
            solidHost.Closed += delegate { solidHost = null; if (!done) Finish(false); };
            solidHost.ContentRendered += delegate
            {
                if (done || coverRendered) return;
                coverRendered = true;
                Program.Log("opaque_cover_rendered elapsed=" + Seconds(timer.Elapsed.TotalSeconds) + " opacity=" + solidHost.Opacity.ToString("F2", CultureInfo.InvariantCulture));
                // Run after the render/layout callbacks, so activation cannot expose a window before cover.
                Dispatcher.BeginInvoke(new Action(BeginCoveredStartup), System.Windows.Threading.DispatcherPriority.Background);
            };
            solidHost.Show();
            solidHost.Activate();
            solidHost.Focus();
            Hide();
        }
        void BeginCoveredStartup()
        {
            if (done || startup == null || startupStarted || !coverRendered || solidHost == null || !solidHost.IsVisible || solidHost.Opacity < .999) return;
            startupStarted = true;
            Program.Log("covered_startup_begin elapsed=" + Seconds(timer.Elapsed.TotalSeconds));
            startup.Begin();
        }
        static string Seconds(double value) { return value.ToString("F2", CultureInfo.InvariantCulture); }
        void EndAnimation()
        {
            if (animationFinished) return;
            animationFinished = true;
            if (player != null) player.Stop();
            Program.Log("animation_end elapsed=" + Seconds(timer.Elapsed.TotalSeconds) + " rendered_frames=" + scene.RenderCount + " viewport=" + scene.ActualWidth.ToString("F0", CultureInfo.InvariantCulture) + "x" + scene.ActualHeight.ToString("F0", CultureInfo.InvariantCulture));
        }
        void RequestEnter()
        {
            if (done) return;
            if (startup == null) { EndAnimation(); Finish(false); return; }
            if (animationFinished && startup.Snapshot.Error != null)
            {
                startup.Dispose();
                startup = new StartupCoordinator(new CodexStartupTarget(Program.Target));
                startupStarted = false;
                scene.StartupError = null;
                waitingLogged = false;
                Program.Log("startup_retry");
                BeginCoveredStartup();
            }
            EndAnimation();
            if (solidHost == null) PromoteWindow();
            if (startup.Snapshot.Ready) BeginHandoff();
        }
        void BeginHandoff()
        {
            if (handoffStart >= 0) return;
            handoffStart = timer.Elapsed.TotalSeconds;
            scene.StartupError = null;
            scene.Waiting = false;
            Program.Log("handoff_begin elapsed=" + Seconds(handoffStart));
        }
        void Finish(bool reveal)
        {
            if (done) return;
            done = true;
            CompositionTarget.Rendering -= Tick;
            if (!animationFinished) EndAnimation();
            if (player != null) player.Stop();
            if (solidHost != null) solidHost.Hide();
            Hide();
            if (startup != null)
            {
                StartupSnapshot state = startup.Snapshot;
                if (reveal && state.Ready) startup.Reveal(state.Window);
                startup.Dispose();
                Program.Log((reveal ? "handoff_complete" : "waiting_cancelled") + " elapsed=" + Seconds(timer.Elapsed.TotalSeconds));
            }
            else Program.Log("preview_complete");
            if (solidHost != null) solidHost.Close();
            Close();
        }
    }

}
