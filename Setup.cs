using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CompanionInstaller
{
    public class CodexInstall
    {
        public string AppId;
        public string PackageRoot;
        public string Version;
        public override string ToString() { return "Codex " + Version; }
    }

    public class InstallContext
    {
        public string Root, AllowedParent, Desktop, Programs, RegistryPath, SetupExecutable;
        public CodexInstall Codex;
        public static InstallContext ForCurrentUser(CodexInstall codex)
        {
            string parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
            return new InstallContext {
                Root = Path.Combine(parent, "CodexStartupAnimation"), AllowedParent = parent,
                Desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                RegistryPath = Installer.UninstallKey,
                SetupExecutable = Assembly.GetExecutingAssembly().Location, Codex = codex
            };
        }
    }

    public static class Installer
    {
        public const string Version = "1.0.0";
        public const string Name = "Codex 启动动画";
        public const string LinkName = "Codex · 启动动画.lnk";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CodexStartupAnimation";
        static readonly string[] OwnedFiles = {
            "Codex-启动动画.exe", "只看动画.exe", "启动音.wav", "Companion.ico", "README.md", "LICENSE",
            "启动设置.ini", "OpenAI-logo.png", "Codex.ico", "Uninstall.exe", "install-manifest.json"
        };
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static CodexInstall[] DiscoverCodex()
        {
            const string script = @"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$ErrorActionPreference = 'Stop'
$items = @()
foreach ($package in @(Get-AppxPackage -Name 'OpenAI.Codex')) {
    $manifest = Get-AppxPackageManifest -Package $package.PackageFullName
    $application = @($manifest.Package.Applications.Application) | Where-Object { $_.Executable -match '(?i)(ChatGPT|Codex)\.exe$' } | Select-Object -First 1
    if ($application) {
        $items += [pscustomobject]@{ AppId = $package.PackageFamilyName + '!' + $application.Id; PackageRoot = $package.InstallLocation; Version = $package.Version.ToString() }
    }
}
ConvertTo-Json -InputObject @($items) -Compress
";
            string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            var start = new ProcessStartInfo(shell, "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8
            };
            using (var process = Process.Start(start))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException("检测 Codex 时遇到问题，请确认 Windows 应用服务可用。" + (error.Length > 0 ? "\n" + error.Trim() : ""));
                return Json.Deserialize<CodexInstall[]>(output.Trim()) ?? new CodexInstall[0];
            }
        }

        public static void Validate(InstallContext context)
        {
            string parent = Path.GetFullPath(context.AllowedParent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string root = Path.GetFullPath(context.Root).TrimEnd(Path.DirectorySeparatorChar);
            if (!root.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("安装目录不在指定程序目录内。");
            if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("安装目录不能是目录链接。");
            if (context.RegistryPath != UninstallKey && !context.RegistryPath.StartsWith(@"Software\CodexStartupAnimation\Tests\", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("卸载注册位置无效。");
        }

        static string Inside(string root, string filename)
        {
            if (filename != Path.GetFileName(filename) || Array.IndexOf(OwnedFiles, filename) < 0) throw new InvalidDataException("安装包包含未知路径。");
            string full = Path.GetFullPath(Path.Combine(root, filename));
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("文件路径超出安装目录。");
            if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("程序文件不能是文件链接。");
            return full;
        }

        public static void ExtractPayload(string root)
        {
            Directory.CreateDirectory(root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("解压目录不能是目录链接。");
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Companion.Payload"))
            {
                if (stream == null) throw new InvalidDataException("安装程序缺少内置文件。");
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    foreach (var entry in archive.Entries)
                        using (var output = File.Create(Inside(root, entry.FullName)))
                        using (var input = entry.Open()) input.CopyTo(output);
            }
        }

        static void EnsureLauncherClosed(string root)
        {
            foreach (string name in new[] { "Codex-启动动画", "只看动画" })
                foreach (var process in Process.GetProcessesByName(name))
                    using (process)
                    {
                        string path = null;
                        try { path = process.MainModule.FileName; } catch { }
                        if (path != null && string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("请先关闭正在播放的启动动画，再进行安装或卸载。");
                    }
        }

        public static void Install(InstallContext context)
        {
            Validate(context);
            if (context.Codex == null || string.IsNullOrWhiteSpace(context.Codex.AppId) || !Directory.Exists(context.Codex.PackageRoot)) throw new InvalidOperationException("请先安装 Windows 版 Codex，然后重新检测。");
            EnsureLauncherClosed(context.Root);
            EnsureShortcutAvailable(Path.Combine(context.Desktop, LinkName), context.Root);
            EnsureShortcutAvailable(Path.Combine(context.Programs, LinkName), context.Root);
            Directory.CreateDirectory(context.Root);
            ExtractPayload(context.Root);
            string icon = Path.Combine(context.Codex.PackageRoot, @"app\resources\icon-chatgpt.ico");
            if (File.Exists(icon)) File.Copy(icon, Inside(context.Root, "Codex.ico"), true);
            string logo = Path.Combine(context.Codex.PackageRoot, @"assets\Square44x44Logo.targetsize-256_altform-unplated.png");
            if (File.Exists(logo)) File.Copy(logo, Inside(context.Root, "OpenAI-logo.png"), true);
            else if (File.Exists(Inside(context.Root, "OpenAI-logo.png"))) File.Delete(Inside(context.Root, "OpenAI-logo.png"));

            string settings = Inside(context.Root, "启动设置.ini");
            var preserved = new List<string>();
            if (File.Exists(settings))
            {
                foreach (string line in File.ReadAllLines(settings))
                    if (!line.TrimStart().StartsWith("AppUserModelId=", StringComparison.OrdinalIgnoreCase)) preserved.Add(line);
            }
            else preserved.AddRange(new[] { "# Codex 启动动画 / AI改变世界", "Duration=15", "Sound=true" });
            preserved.Add("AppUserModelId=" + context.Codex.AppId);
            File.WriteAllLines(settings, preserved.ToArray(), new UTF8Encoding(true));
            string uninstall = Inside(context.Root, "Uninstall.exe");
            if (!string.Equals(Path.GetFullPath(context.SetupExecutable), uninstall, StringComparison.OrdinalIgnoreCase)) File.Copy(context.SetupExecutable, uninstall, true);
            CreateShortcut(Path.Combine(context.Desktop, LinkName), context.Root);
            CreateShortcut(Path.Combine(context.Programs, LinkName), context.Root);

            using (var key = Registry.CurrentUser.CreateSubKey(context.RegistryPath))
            {
                key.SetValue("DisplayName", Name); key.SetValue("DisplayVersion", Version);
                key.SetValue("Publisher", "chenrancr9527-bit (community project)"); key.SetValue("InstallLocation", context.Root);
                key.SetValue("DisplayIcon", Path.Combine(context.Root, File.Exists(Path.Combine(context.Root, "Codex.ico")) ? "Codex.ico" : "Companion.ico"));
                key.SetValue("UninstallString", "\"" + uninstall + "\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("URLInfoAbout", "https://github.com/chenrancr9527-bit/codex-startup-animation");
            }
            File.WriteAllText(Inside(context.Root, "install-manifest.json"), Json.Serialize(new { Product = "CodexStartupAnimation", Version = Version, Root = Path.GetFullPath(context.Root) }), new UTF8Encoding(true));
        }

        static void EnsureShortcutAvailable(string path, string root)
        {
            if (!File.Exists(path)) return;
            string existing = ShortcutFiles.TargetOf(path);
            if (!string.Equals(existing, Path.Combine(root, "Codex-启动动画.exe"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("同名快捷方式已用于另一个程序，请先为它改名后重试。");
        }
        static void CreateShortcut(string path, string root)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EnsureShortcutAvailable(path, root);
            ShortcutFiles.Write(path, Path.Combine(root, "Codex-启动动画.exe"), root, "AI改变世界 · 全屏后台加载 Codex", Path.Combine(root, File.Exists(Path.Combine(root, "Codex.ico")) ? "Codex.ico" : "Companion.ico"));
        }

        static void RemoveShortcutIfOwned(string path, string root)
        {
            if (!File.Exists(path)) return;
            string target = ShortcutFiles.TargetOf(path);
            if (string.Equals(target, Path.Combine(root, "Codex-启动动画.exe"), StringComparison.OrdinalIgnoreCase)) File.Delete(path);
        }

        public static string Uninstall(InstallContext context)
        {
            Validate(context); EnsureLauncherClosed(context.Root);
            string manifest = Inside(context.Root, "install-manifest.json");
            if (!File.Exists(manifest)) throw new InvalidOperationException("没有找到本项目的安装记录，未删除任何文件。");
            var record = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifest));
            if (!record.ContainsKey("Product") || (string)record["Product"] != "CodexStartupAnimation" || !string.Equals((string)record["Root"], Path.GetFullPath(context.Root), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("安装记录与目录不匹配，未删除任何文件。");
            RemoveShortcutIfOwned(Path.Combine(context.Desktop, LinkName), context.Root);
            RemoveShortcutIfOwned(Path.Combine(context.Programs, LinkName), context.Root);
            using (var key = Registry.CurrentUser.OpenSubKey(context.RegistryPath))
                if (key != null && !string.Equals((string)key.GetValue("InstallLocation"), context.Root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("卸载记录不匹配，未删除程序文件。");
            Registry.CurrentUser.DeleteSubKeyTree(context.RegistryPath, false);
            string deferred = null;
            foreach (string name in OwnedFiles)
            {
                string path = Inside(context.Root, name);
                if (!File.Exists(path)) continue;
                if (string.Equals(path, Assembly.GetEntryAssembly().Location, StringComparison.OrdinalIgnoreCase)) deferred = path;
                else File.Delete(path);
            }
            if (deferred == null) { try { Directory.Delete(context.Root, false); } catch (IOException) { } }
            return deferred;
        }

        public static void DeleteSelfAfterExit(string file, string root)
        {
            if (!string.Equals(file, Path.GetFullPath(Path.Combine(root, "Uninstall.exe")), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("清理文件路径无效。");
            string quotedFile = "'" + file.Replace("'", "''") + "'", quotedRoot = "'" + root.Replace("'", "''") + "'";
            string script = "Wait-Process -Id " + Process.GetCurrentProcess().Id + " -ErrorAction SilentlyContinue; Remove-Item -LiteralPath " + quotedFile + " -Force -ErrorAction SilentlyContinue; if ((Test-Path -LiteralPath " + quotedRoot + ") -and (@(Get-ChildItem -LiteralPath " + quotedRoot + " -Force).Count -eq 0)) { Remove-Item -LiteralPath " + quotedRoot + " -Force -ErrorAction SilentlyContinue }";
            string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            Process.Start(new ProcessStartInfo(shell, "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        }
    }

    public class SetupForm : Form
    {
        readonly Label status;
        readonly ComboBox versions;
        readonly Button primary, refresh;
        readonly CheckBox launch;
        readonly bool uninstall;
        bool installed;
        public SetupForm(bool uninstall)
        {
            this.uninstall = uninstall;
            Text = uninstall ? "卸载 Codex 启动动画" : "安装 Codex 启动动画";
            ClientSize = new Size(630, 350); FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(5, 19, 23); ForeColor = Color.FromArgb(220, 245, 234); Font = new Font("Microsoft YaHei UI", 10);
            Controls.Add(new Label { Text = "Codex 启动动画", Font = new Font(Font.FontFamily, 22), ForeColor = Color.FromArgb(107, 242, 177), Location = new Point(28, 25), AutoSize = true });
            Controls.Add(new Label { Text = "AI改变世界", Location = new Point(32, 77), AutoSize = true });
            status = new Label { Location = new Point(32, 120), Size = new Size(564, 78) }; Controls.Add(status);
            status.Text = uninstall ? "卸载启动动画，保留 Codex 应用。" : "正在检测 Windows 版 Codex…";
            versions = new ComboBox { Location = new Point(32, 205), Size = new Size(564, 32), DropDownStyle = ComboBoxStyle.DropDownList }; Controls.Add(versions);
            launch = new CheckBox { Text = "安装完成后播放启动动画", Location = new Point(32, 257), AutoSize = true }; Controls.Add(launch);
            primary = new Button { Text = uninstall ? "卸载" : "安装", Location = new Point(478, 294), Size = new Size(118, 36), Enabled = uninstall }; Controls.Add(primary);
            refresh = new Button { Text = "重新检测", Location = new Point(345, 294), Size = new Size(118, 36) }; Controls.Add(refresh);
            primary.Click += delegate { if (installed) Close(); else if (uninstall) Remove(); else Install(); };
            refresh.Click += delegate { Detect(); };
            Shown += delegate
            {
                if (uninstall) { status.Text = "卸载本项目的动画、快捷方式和设置。\nCodex 应用和聊天数据会保留。"; versions.Visible = false; launch.Visible = false; refresh.Visible = false; }
                else Detect();
            };
        }
        void Work(string message, Action action, Action success)
        {
            status.Text = message; primary.Enabled = false; refresh.Enabled = false;
            var thread = new Thread(delegate()
            {
                Exception error = null; try { action(); } catch (Exception e) { error = e; }
                if (IsDisposed) return;
                BeginInvoke(new Action(delegate
                {
                    refresh.Enabled = true;
                    if (error != null) { status.Text = error.Message; primary.Enabled = uninstall || versions.Items.Count > 0; }
                    else success();
                }));
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
        }
        void Detect()
        {
            CodexInstall[] found = null;
            Work("正在检测 Windows 版 Codex…", delegate { found = Installer.DiscoverCodex(); }, delegate
            {
                versions.Items.Clear(); versions.Items.AddRange(found);
                if (found.Length > 0) { versions.SelectedIndex = 0; status.Text = "已检测到 Codex。\n安装后，使用桌面或开始菜单中的「Codex · 启动动画」。"; primary.Enabled = true; }
                else { status.Text = "需要先安装 Windows 版 Codex，再点击「重新检测」。\n这个安装程序提供启动动画，不包含 Codex 应用。"; primary.Enabled = false; }
            });
        }
        void Install()
        {
            var context = InstallContext.ForCurrentUser((CodexInstall)versions.SelectedItem);
            Work("正在安装启动动画…", delegate { Installer.Install(context); }, delegate
            {
                installed = true; primary.Text = "完成"; primary.Enabled = true; refresh.Visible = false;
                status.Text = "安装完成。\n请使用桌面或开始菜单中的「Codex · 启动动画」。";
                if (launch.Checked) Process.Start(new ProcessStartInfo(Path.Combine(context.Root, "Codex-启动动画.exe")) { UseShellExecute = true, WorkingDirectory = context.Root });
            });
        }
        void Remove()
        {
            var context = InstallContext.ForCurrentUser(null);
            string actualRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.Equals(context.Root, actualRoot, StringComparison.OrdinalIgnoreCase)) { status.Text = "请从 Windows 设置中的「已安装的应用」卸载，或运行安装目录内的 Uninstall.exe。"; return; }
            string deferred = null;
            Work("正在卸载启动动画…", delegate { deferred = Installer.Uninstall(context); }, delegate
            {
                installed = true; primary.Text = "完成"; primary.Enabled = true;
                status.Text = "启动动画已卸载。Codex 应用和聊天数据会保留。";
                if (deferred != null) Installer.DeleteSelfAfterExit(deferred, context.Root);
            });
        }
    }

    public static class SetupProgram
    {
        [STAThread] public static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--extract") { Installer.ExtractPayload(Path.GetFullPath(args[1])); return 0; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm(Array.IndexOf(args, "--uninstall") >= 0)); return 0;
        }
    }
}
