using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BnbuSetup {
    static class Program {
        public static string Root { get { return Environment.GetEnvironmentVariable("BNBU_INSTALL_TEST_ROOT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BNBU-DDL"); } }
        public static string AppDir { get { return Path.Combine(Root, "app"); } }
        public static string AppPath { get { return Path.Combine(AppDir, "BNBU-DDL.exe"); } }
        public static readonly Icon BrandIcon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.ico"), 64, 64);
        public static readonly Image BrandImage = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.png"));
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BNBU-DDL";
        static bool TestMode { get { return Environment.GetEnvironmentVariable("BNBU_INSTALL_TEST_ROOT") != null; } }
        static string RunKey { get { return TestMode ? @"Software\BNBU-DDL-InstallTest\Run" : @"Software\Microsoft\Windows\CurrentVersion\Run"; } }
        static string EffectiveUninstallKey { get { return TestMode ? @"Software\BNBU-DDL-InstallTest\Uninstall" : UninstallKey; } }
        [STAThread] static int Main(string[] args) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--render-preview") {
                using (var preview = new SetupForm()) {
                    preview.Show(); Application.DoEvents();
                    using (var bitmap = new Bitmap(preview.Width, preview.Height)) { preview.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height)); bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png); }
                    preview.Hide();
                }
                return 0;
            }
            if (args.Length == 2 && args[0] == "--verify-package") {
                try { Verify(args[1]); return 0; } catch (Exception ex) { File.WriteAllText(args[1], "FAIL " + ex); return 1; }
            }
            if (args.Length == 2 && args[0] == "--install-test") {
                try {
                    if (!TestMode) throw new Exception("An isolated install root is required.");
                    Task.Run(() => Install(true, true, message => { })).GetAwaiter().GetResult();
                    if (!File.Exists(AppPath) || !File.Exists(Path.Combine(Root, "test-desktop", "BNBU DDL 提醒.lnk"))) throw new Exception("Installed program or shortcut is missing.");
                    using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) if (key == null || !(key.GetValue("BNBU-DDL") as string ?? "").Contains(AppPath)) throw new Exception("Autostart target mismatch.");
                    File.WriteAllText(args[1], "PASS isolated install engine, extracted app, COM shortcuts, autostart target, uninstall registration; runtime=" + HasWebView());
                    return 0;
                } catch (Exception ex) { File.WriteAllText(args[1], "FAIL " + ex); return 1; }
                finally { if (TestMode) Registry.CurrentUser.DeleteSubKeyTree(@"Software\BNBU-DDL-InstallTest", false); }
            }
            if (args.Contains("--uninstall")) { Uninstall(); return 0; }
            Application.Run(new SetupForm()); return 0;
        }
        public static void Extract(string destination) {
            Directory.CreateDirectory(destination);
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            using (var zip = new ZipArchive(resource, ZipArchiveMode.Read)) {
                foreach (var entry in zip.Entries) {
                    var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                    if (!target.StartsWith(Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid package path.");
                    if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target, true);
                }
            }
        }
        public static bool HasWebView() {
            const string relative = @"Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 }) {
                try { using (var root = RegistryKey.OpenBaseKey(hive, view)) using (var key = root.OpenSubKey(relative)) {
                    string pv = key == null ? null : key.GetValue("pv") as string;
                    if (!String.IsNullOrEmpty(pv) && pv != "0.0.0.0") return true;
                } } catch { }
            }
            return false;
        }
        public static void Install(bool desktop, bool startup, Action<string> progress) {
            progress("正在解包程序…");
            // Fail clearly on an upgrade while the installed program is running.
            foreach (var p in Process.GetProcessesByName("BNBU-DDL")) {
                try { if (String.Equals(p.MainModule.FileName, AppPath, StringComparison.OrdinalIgnoreCase)) throw new IOException("请先从右下角托盘退出 BNBU DDL 提醒，再重新安装。"); }
                finally { p.Dispose(); }
            }
            Extract(AppDir);
            if (!HasWebView()) {
                progress("正在安装 Microsoft 网页组件，首次安装需要联网…");
                using (var p = Process.Start(new ProcessStartInfo(Path.Combine(AppDir, "MicrosoftEdgeWebview2Setup.exe"), "/silent /install") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })) {
                    if (!p.WaitForExit(180000)) throw new IOException("网页组件安装还未完成，请稍后重新运行安装包。");
                    if (!HasWebView()) throw new IOException("网页组件尚未安装完成。请连接网络后重新运行安装包。");
                }
            }
            progress("正在创建快捷方式和开机提醒…");
            if (desktop) Shortcut(TestMode ? Path.Combine(Root, "test-desktop") : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "BNBU DDL 提醒.lnk");
            string startMenu = TestMode ? Path.Combine(Root, "test-start-menu") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "BNBU DDL 提醒");
            Directory.CreateDirectory(startMenu); Shortcut(startMenu, "BNBU DDL 提醒.lnk");
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey)) {
                if (startup) run.SetValue("BNBU-DDL", "\"" + AppPath + "\" --background"); else run.DeleteValue("BNBU-DDL", false);
            }
            string uninstallPath = Path.Combine(AppDir, "Uninstall.exe");
            string setupPath = Assembly.GetExecutingAssembly().Location;
            if (!String.Equals(setupPath, uninstallPath, StringComparison.OrdinalIgnoreCase)) File.Copy(setupPath, uninstallPath, true);
            using (var key = Registry.CurrentUser.CreateSubKey(EffectiveUninstallKey)) {
                key.SetValue("DisplayName", "BNBU DDL 提醒"); key.SetValue("DisplayVersion", "3.0.1"); key.SetValue("Publisher", "BNBU DDL Reminder");
                key.SetValue("InstallLocation", AppDir); key.SetValue("DisplayIcon", AppPath);
                key.SetValue("UninstallString", "\"" + uninstallPath + "\" --uninstall"); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
            }
            progress("安装完成。首次在程序内登录自己的 iSpace 即可同步作业。");
        }
        static void Shortcut(string folder, string name) {
            Directory.CreateDirectory(folder); object shell = null, link = null;
            try {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { Path.Combine(folder, name) });
                var type = link.GetType();
                type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { AppPath });
                type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { AppPath + ",0" });
                type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { AppDir });
                type.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "BNBU DDL 提醒" });
                type.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            } finally { if (link != null) Marshal.FinalReleaseComObject(link); if (shell != null) Marshal.FinalReleaseComObject(shell); }
        }
        static void Uninstall() {
            if (MessageBox.Show("卸载 BNBU DDL 提醒？本机作业和登录会话会保留。请先从托盘退出程序。", "卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try {
                using (var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) if (run != null) run.DeleteValue("BNBU-DDL", false);
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
                string link = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "BNBU DDL 提醒.lnk");
                if (File.Exists(link)) {
                    // Only remove a shortcut that points to this installation.
                    object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                    object shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
                    string target = (string)shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
                    Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell);
                    if (String.Equals(target, AppPath, StringComparison.OrdinalIgnoreCase)) File.Delete(link);
                }
                string start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "BNBU DDL 提醒", "BNBU DDL 提醒.lnk");
                if (File.Exists(start)) File.Delete(start);
                foreach (var file in Directory.GetFiles(AppDir, "*", SearchOption.AllDirectories)) {
                    if (!String.Equals(file, Assembly.GetExecutingAssembly().Location, StringComparison.OrdinalIgnoreCase)) { try { File.Delete(file); } catch { } }
                }
                MessageBox.Show("已取消开机启动并移除程序文件。卸载器和本机数据保留在安装目录。", "卸载完成");
            } catch (Exception ex) { MessageBox.Show(ex.Message, "卸载未完成"); }
        }
        static void Verify(string report) {
            string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)), "package-check-" + Guid.NewGuid().ToString("N"));
            Extract(temp);
            string[] required = { "BNBU-DDL.exe", "BNBU-DDL.exe.config", "app.ico", "Microsoft.Web.WebView2.Core.dll", "Microsoft.Web.WebView2.WinForms.dll", "WebView2Loader.dll", "MicrosoftEdgeWebview2Setup.exe", "使用说明.txt", "ThirdPartyNotices.txt" };
            foreach (var file in required) if (!File.Exists(Path.Combine(temp, file))) throw new Exception("Missing " + file);
            if (Directory.GetFiles(temp, "*.json", SearchOption.AllDirectories).Any() || Directory.Exists(Path.Combine(temp, "data")) || Directory.Exists(Path.Combine(temp, "portal-profile"))) throw new Exception("Package includes user data.");
            File.WriteAllText(report, "PASS package extraction, required components, no coursework or account data; extracted=" + temp);
        }
    }
    class SetupForm : Form {
        Button install, launch; Label status; CheckBox desktop, startup;
        public SetupForm() {
            Icon = Program.BrandIcon;
            Text = "安装 BNBU DDL 提醒"; ClientSize = new Size(590, 420); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; BackColor = Color.White; Font = new Font("Microsoft YaHei UI", 10);
            Controls.Add(new PictureBox { Image = Program.BrandImage, Location = new Point(30, 22), Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom });
            Controls.Add(new Label { Text = "BNBU DDL 提醒", Font = new Font(Font.FontFamily, 23, FontStyle.Bold), Location = new Point(112, 26), Size = new Size(440, 50) });
            Controls.Add(new Label { Text = "登录学校门户、同步作业、后台提醒，都在一个程序里。\n首次安装后，用自己的 iSpace 账号登录一次。", Location = new Point(32, 93), Size = new Size(520, 65) });
            desktop = new CheckBox { Text = "创建桌面快捷方式", Checked = true, Location = new Point(32, 180), Size = new Size(400, 30) }; Controls.Add(desktop);
            startup = new CheckBox { Text = "登录 Windows 后自动提醒", Checked = true, Location = new Point(32, 219), Size = new Size(400, 30) }; Controls.Add(startup);
            status = new Label { Text = "Windows 10 / 11 64 位 · 安装到当前用户 · 无需浏览器扩展\n缺少 Microsoft 网页组件时，安装过程会联网补装。", ForeColor = Color.DimGray, Location = new Point(32, 272), Size = new Size(520, 61) }; Controls.Add(status);
            install = new Button { Text = "安装", BackColor = Color.RoyalBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(350, 351), Size = new Size(200, 42) }; Controls.Add(install);
            launch = new Button { Text = "打开并登录 iSpace", BackColor = Color.RoyalBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = install.Location, Size = install.Size, Visible = false }; Controls.Add(launch);
            install.Click += async (s, e) => {
                install.Enabled = false; desktop.Enabled = startup.Enabled = false;
                bool desk = desktop.Checked, run = startup.Checked;
                try { await Task.Run(() => Program.Install(desk, run, text => BeginInvoke(new Action(() => status.Text = text)))); install.Visible = false; launch.Visible = true; }
                catch (Exception ex) { status.Text = ex.Message; install.Enabled = true; desktop.Enabled = startup.Enabled = true; }
            };
            launch.Click += (s, e) => { Process.Start(Program.AppPath); Close(); };
            FormClosing += (s, e) => { if (!install.Enabled && install.Visible) e.Cancel = true; };
        }
    }
}
