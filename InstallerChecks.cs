using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using CompanionInstaller;

class InstallerChecks
{
    static void Assert(bool value, string description) { if (!value) throw new Exception(description); Console.WriteLine("PASS " + description); }
    static void Reject(Action action, string description) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Assert(rejected, description); }
    static string TargetOf(string path)
    {
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")), link = null;
        try { link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path }); return (string)link.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null); }
        finally { if (link != null) Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
    }
    static void OtherShortcut(string path, string target)
    {
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")), link = null;
        try {
            link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            link.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
            link.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        } finally { if (link != null) Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
    }
    [STAThread] static int Main(string[] args)
    {
        string projectRoot = Path.GetFullPath(args[0]);
        string suiteRoot = Path.Combine(projectRoot, "test-output", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(suiteRoot);
        var context = new InstallContext {
            AllowedParent = suiteRoot, Root = Path.Combine(suiteRoot, "installed"),
            Desktop = Path.Combine(suiteRoot, "desktop"), Programs = Path.Combine(suiteRoot, "start-menu"),
            RegistryPath = @"Software\CodexStartupAnimation\Tests\" + Guid.NewGuid().ToString("N"),
            SetupExecutable = Path.Combine(projectRoot, "dist", "CodexStartupSetup.exe")
        };
        try
        {
            CodexInstall[] real = Installer.DiscoverCodex();
            Console.WriteLine("Actual Codex discovery: " + real.Length + " installation(s)");
            foreach (var app in real) Assert(app.AppId.Contains("!") && Directory.Exists(app.PackageRoot), "real application id and installation path are valid");
            string packageRoot = Path.Combine(suiteRoot, "codex-fixture");
            Directory.CreateDirectory(Path.Combine(packageRoot, "assets"));
            Directory.CreateDirectory(Path.Combine(packageRoot, "app", "resources"));
            using (var logo = new Bitmap(16,16)) { logo.SetPixel(8,8,Color.White); logo.Save(Path.Combine(packageRoot, "assets", "Square44x44Logo.targetsize-256_altform-unplated.png")); }
            File.Copy(Path.Combine(projectRoot, "Companion.ico"), Path.Combine(packageRoot, "app", "resources", "icon-chatgpt.ico"));
            context.Codex = new CodexInstall { AppId = "OpenAI.Codex_Test!App", PackageRoot = packageRoot, Version = "test" };
            Directory.CreateDirectory(context.Desktop);
            string original = Path.Combine(context.Desktop, "Codex.lnk"); File.WriteAllText(original, "original shortcut fixture");

            string collision = Path.Combine(context.Desktop, Installer.LinkName);
            string foreignTarget = Path.Combine(suiteRoot,"different-program.exe");
            OtherShortcut(collision, foreignTarget);
            Reject(delegate { Installer.Install(context); }, "unrelated shortcut collision is rejected before installing files");
            Assert(!Directory.Exists(context.Root) && TargetOf(collision)==foreignTarget, "collision keeps original shortcut and leaves installation directory untouched");
            File.Delete(collision);

            Installer.Install(context);
            Assert(File.Exists(Path.Combine(context.Root,"Codex-启动动画.exe")) && File.Exists(Path.Combine(context.Root,"Uninstall.exe")), "launcher and uninstaller are installed");
            Assert(File.ReadAllText(Path.Combine(context.Root,"启动设置.ini")).Contains("AppUserModelId=OpenAI.Codex_Test!App"), "detected target is written to settings");
            Assert(TargetOf(Path.Combine(context.Desktop,Installer.LinkName)) == Path.Combine(context.Root,"Codex-启动动画.exe"), "desktop shortcut targets installed launcher");
            Assert(TargetOf(Path.Combine(context.Programs,Installer.LinkName)) == Path.Combine(context.Root,"Codex-启动动画.exe"), "start menu shortcut targets installed launcher");
            Assert(File.ReadAllText(original)=="original shortcut fixture", "official shortcut fixture remains unchanged");
            using(var key=Registry.CurrentUser.OpenSubKey(context.RegistryPath)) Assert(key!=null && (string)key.GetValue("InstallLocation")==context.Root, "uninstaller is registered under current user");
            Assert(File.Exists(Path.Combine(context.Root,"OpenAI-logo.png")) && File.Exists(Path.Combine(context.Root,"Codex.ico")), "branding assets are read from the local Codex installation");

            string settings=Path.Combine(context.Root,"启动设置.ini");
            File.WriteAllText(settings,"Duration=9\nSound=false\nAppUserModelId=old!App\n", new UTF8Encoding(true));
            Installer.Install(context);
            string upgraded=File.ReadAllText(settings);
            Assert(upgraded.Contains("Duration=9") && upgraded.Contains("Sound=false") && !upgraded.Contains("old!App"), "upgrade preserves preferences and refreshes application identity");
            Assert(upgraded.Split(new[]{"AppUserModelId="},StringSplitOptions.None).Length==2, "upgrade writes one target setting");

            var escaped = new InstallContext { Root=Path.Combine(suiteRoot,"..","escaped"), AllowedParent=suiteRoot, RegistryPath=context.RegistryPath };
            Reject(delegate { Installer.Validate(escaped); }, "path outside approved install parent is rejected");
            File.WriteAllText(Path.Combine(context.Root,"user-note.txt"),"keep this user file");
            Assert(Installer.Uninstall(context)==null, "isolated uninstall completes without deferred files");
            Assert(!File.Exists(Path.Combine(context.Root,"Codex-启动动画.exe")) && !File.Exists(Path.Combine(context.Desktop,Installer.LinkName)) && !File.Exists(Path.Combine(context.Programs,Installer.LinkName)), "uninstall removes owned programs and shortcuts");
            Assert(File.Exists(original) && File.ReadAllText(Path.Combine(context.Root,"user-note.txt"))=="keep this user file", "uninstall preserves unrelated shortcut and user files");
            using(var key=Registry.CurrentUser.OpenSubKey(context.RegistryPath)) Assert(key==null,"uninstall removes only its registration");
            using(var form=new SetupForm(false))
            {
                form.StartPosition=System.Windows.Forms.FormStartPosition.Manual; form.Location=new Point(-5000,-5000); form.ShowInTaskbar=false;
                form.Show();
                var renderWatch=Stopwatch.StartNew();
                while(renderWatch.ElapsedMilliseconds<1500) { System.Windows.Forms.Application.DoEvents(); System.Threading.Thread.Sleep(15); }
                using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height)); image.Save(Path.Combine(projectRoot,"test-output","installer-ui.png")); }
                form.Close();
            }
            Console.WriteLine("ALL INSTALLER CHECKS PASSED");
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { Registry.CurrentUser.DeleteSubKeyTree(context.RegistryPath,false); }
    }
}
