using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BnbuReminder {
    public class DdlTask {
        public string id { get; set; }
        public string title { get; set; }
        public string course { get; set; }
        public long dueAt { get; set; }
        public string url { get; set; }
    }
    public class TaskFile {
        public int version { get; set; }
        public long exportedAt { get; set; }
        public List<DdlTask> tasks { get; set; }
    }
    public class UserState {
        public Dictionary<string, long> completed { get; set; }
        public Dictionary<string, long> notified { get; set; }
        public Dictionary<string, long> snoozed { get; set; }
        public UserState() {
            completed = new Dictionary<string, long>();
            notified = new Dictionary<string, long>();
            snoozed = new Dictionary<string, long>();
        }
    }
    public static class Store {
#if STORE_BUILD
        public const string DataFolder = "BNBU-DDL-Store";
        public const string InstanceName = "Local\\BnbuDdlStore";
        public const string ShowEventName = "Local\\BnbuDdlStoreShow";
#else
        public const string DataFolder = "BNBU-DDL";
        public const string InstanceName = "Local\\BnbuDdlDesktopIntegrated";
        public const string ShowEventName = "Local\\BnbuDdlIntegratedShow";
#endif
        public static string Root = Environment.GetEnvironmentVariable("BNBU_DDL_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataFolder);
        public static string DataFile { get { return Path.Combine(Root, "data", "tasks.json"); } }
        public static string StateFile { get { return Path.Combine(Root, "data", "state.json"); } }
        public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024 }; }
        public static long Now { get { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); } }
        public static void AtomicWrite(string path, string contents) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tmp, contents, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
        }
        public static T Read<T>(string path, T fallback) {
            try { return Json().Deserialize<T>(File.ReadAllText(path, Encoding.UTF8)); }
            catch { return fallback; }
        }
        public static List<DdlTask> Validate(IEnumerable<DdlTask> tasks) {
            return (tasks ?? new List<DdlTask>()).Where(t => t != null && !String.IsNullOrWhiteSpace(t.id)
                && !String.IsNullOrWhiteSpace(t.title) && t.title.Length <= 500 && t.id.Length <= 100
                && t.dueAt > 1577836800000L && t.dueAt < 4102444800000L)
                .GroupBy(t => t.id).Select(g => g.Last()).OrderBy(t => t.dueAt).Take(500).ToList();
        }
        public static TaskFile Load() {
            var file = Read<TaskFile>(DataFile, new TaskFile { tasks = new List<DdlTask>() });
            if (file == null) file = new TaskFile { tasks = new List<DdlTask>() };
            file.tasks = Validate(file.tasks); return file;
        }
        public static int Merge(TaskFile incoming) {
            if (incoming == null) throw new InvalidDataException("清单格式无法识别。");
            var valid = Validate(incoming.tasks);
            if (valid.Count == 0) return 0;
            using (var gate = new Mutex(false, "Local\\BnbuDdlDataWrite")) {
                bool held = false;
                try {
                    try { held = gate.WaitOne(10000); } catch (AbandonedMutexException) { held = true; }
                    if (!held) throw new IOException("数据正在更新，请稍后重试。");
                    var old = Load();
                    var map = old.tasks.ToDictionary(t => t.id);
                    foreach (var task in valid) map[task.id] = task;
                    var merged = new TaskFile { version = 1, exportedAt = Math.Max(old.exportedAt, incoming.exportedAt), tasks = Validate(map.Values) };
                    AtomicWrite(DataFile, Json().Serialize(merged)); return valid.Count;
                } finally { if (held) gate.ReleaseMutex(); }
            }
        }
        public static int ImportFile(string path) {
            var contents = File.ReadAllText(path, Encoding.UTF8);
            TaskFile data;
            if (contents.TrimStart().StartsWith("[")) data = new TaskFile { tasks = Json().Deserialize<List<DdlTask>>(contents), exportedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeMilliseconds() };
            else data = Json().Deserialize<TaskFile>(contents);
            return Merge(data);
        }
        public static void ImportLocal() {
#if !STORE_BUILD
            string oldFile = Path.Combine(Root, "tasks.json");
            if (!File.Exists(DataFile) && !File.Exists(oldFile) && Environment.GetEnvironmentVariable("BNBU_DDL_DATA_DIR") == null) {
                string legacy = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "Documents", "Codex", "BNBU-DDL-Reminder", "data", "tasks.json");
                if (File.Exists(legacy)) ImportFile(legacy);
            }
            if (!File.Exists(DataFile) && File.Exists(oldFile)) ImportFile(oldFile);
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            try {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders")) {
                    string value = key == null ? null : key.GetValue("{374DE290-123F-4565-9164-39C4925E467B}") as string;
                    if (!String.IsNullOrWhiteSpace(value)) downloads = Environment.ExpandEnvironmentVariables(value);
                }
                if (Directory.Exists(downloads)) foreach (var file in new DirectoryInfo(downloads).GetFiles("BNBU-DDL-Reminder-tasks*.json").OrderBy(f => f.LastWriteTimeUtc)) {
                    if (!File.Exists(DataFile) || file.LastWriteTimeUtc > File.GetLastWriteTimeUtc(DataFile)) {
                        try { ImportFile(file.FullName); } catch { }
                    }
                }
            } catch { }
#endif
        }
        public static UserState LoadState() {
            var s = Read<UserState>(StateFile, new UserState()) ?? new UserState();
            if (s.completed == null) s.completed = new Dictionary<string, long>();
            if (s.notified == null) s.notified = new Dictionary<string, long>();
            if (s.snoozed == null) s.snoozed = new Dictionary<string, long>();
            return s;
        }
        public static void SaveState(UserState s) { AtomicWrite(StateFile, Json().Serialize(s)); }
        public static string Key(DdlTask task) { return task.id + ":" + task.dueAt; }
        public static bool Done(DdlTask t, UserState s) { return s.completed.ContainsKey(t.id) && s.completed[t.id] == t.dueAt; }
        public static bool ShouldRemind(DdlTask task, UserState s, long now) {
            if (Done(task, s) || now < task.dueAt - 86400000L || now > task.dueAt + 86400000L) return false;
            string key = Key(task);
            if (s.snoozed.ContainsKey(key)) return s.snoozed[key] <= now;
            return !s.notified.ContainsKey(key);
        }
        public static DateTime LocalTime(long ms) { return DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime; }
        public static void OpenAssignment(DdlTask task) {
            Uri uri;
            if (Uri.TryCreate(task.url, UriKind.Absolute, out uri) && uri.Scheme == "https" && uri.Host == "ispace.bnbu.edu.cn") Process.Start(uri.AbsoluteUri);
        }
        public static void Log(Exception ex) {
            try { Directory.CreateDirectory(Root); File.AppendAllText(Path.Combine(Root, "app.log"), DateTimeOffset.Now.ToString("o") + " " + ex.Message + Environment.NewLine); } catch { }
        }
    }
    public static class NativeHost {
        static byte[] Exact(Stream input, int length) {
            byte[] bytes = new byte[length]; int read = 0;
            while (read < length) { int n = input.Read(bytes, read, length - read); if (n == 0) throw new EndOfStreamException(); read += n; }
            return bytes;
        }
        public static int Run() {
            try {
                Stream input = Console.OpenStandardInput(), output = Console.OpenStandardOutput();
                int length = BitConverter.ToInt32(Exact(input, 4), 0);
                if (length <= 0 || length > 1024 * 1024) return 2;
                string json = Encoding.UTF8.GetString(Exact(input, length));
                var data = Store.Json().Deserialize<TaskFile>(json);
                int count = Store.Merge(data);
                byte[] response = Encoding.UTF8.GetBytes(Store.Json().Serialize(new { ok = true, count = count, savedAt = Store.Now }));
                output.Write(BitConverter.GetBytes(response.Length), 0, 4);
                output.Write(response, 0, response.Length); output.Flush(); return 0;
            } catch (Exception ex) { Store.Log(ex); return 1; }
        }
    }
    static class Program {
        [STAThread]
        static int Main(string[] args) {
            if (args.Any(a => a == "--native" || a.StartsWith("chrome-extension://"))) return NativeHost.Run();
            if (args.Length > 1 && args[0] == "--self-test") return SelfTest.Run(args[1]);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 2 && args[0] == "--webview-test") { Application.Run(new PortalForm(null, args[1], args[2])); return Environment.ExitCode; }
            bool created;
            using (var single = new Mutex(true, Store.InstanceName, out created)) {
                using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, Store.ShowEventName)) {
                    if (!created) { if (!args.Contains("--background")) show.Set(); return 0; }
                    try { Store.ImportLocal(); Application.Run(new MainForm(args.Contains("--background"), show)); }
                    catch (Exception ex) { Store.Log(ex); if (!args.Contains("--background")) MessageBox.Show(ex.Message, "BNBU DDL 提醒"); return 1; }
                }
            }
            return 0;
        }
    }
    public static class Theme {
        public static readonly Icon BrandIcon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.ico"), 64, 64);
        public static readonly Image BrandImage = Image.FromStream(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.png"));
        public static Color Ink = Color.FromArgb(27, 38, 58), Muted = Color.FromArgb(99, 112, 132), Blue = Color.FromArgb(45, 95, 229), Canvas = Color.FromArgb(244, 247, 252);
        public static Font Font(float size, FontStyle style = FontStyle.Regular) { return new Font("Microsoft YaHei UI", size, style); }
        public static Label Label(string text, int x, int y, int w, int h, float size = 10, bool bold = false) {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), Font = Font(size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = Ink, BackColor = Color.Transparent, AutoEllipsis = true };
        }
        public static Button Button(string text, int x, int y, int w, bool primary = false) {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 38), MinimumSize = new Size(w, 38), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 8, 10, 8), Font = Font(9), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, BackColor = primary ? Blue : Color.White, ForeColor = primary ? Color.White : Ink };
            b.FlatAppearance.BorderColor = Color.FromArgb(221, 228, 239); return b;
        }
        public static Label Text(string text, float size = 10, bool bold = false) {
            return new Label { Text = text, AutoSize = true, Font = Font(size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = Ink, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 8) };
        }
        public static void Configure(Form form) {
            form.SuspendLayout();
            form.AutoScaleDimensions = new SizeF(96, 96);
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.Font = Font(10);
        }
        public static void Finish(Form form) {
            form.ResumeLayout(true);
            form.Shown += (s, e) => {
                var work = Screen.FromControl(form).WorkingArea;
                form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, work.Width), Math.Min(form.MinimumSize.Height, work.Height));
                form.Size = new Size(Math.Min(form.Width, work.Width), Math.Min(form.Height, work.Height));
            };
        }
    }
    public class MainForm : Form {
        NotifyIcon tray;
        FlowLayoutPanel cards;
        Label summary, status, empty;
        System.Windows.Forms.Timer clock;
        bool quitting, background;
        EventWaitHandle showEvent;
        string lastSignature = "";
        HashSet<string> openReminders = new HashSet<string>();
        PortalForm portalForm;
        public MainForm(bool startBackground, EventWaitHandle show) {
            Theme.Configure(this);
            background = startBackground; showEvent = show;
            Text = "BNBU DDL 提醒"; Size = new Size(1000, 720); MinimumSize = new Size(850, 580);
#if STORE_BUILD
            Text = "BNBU DDL 提醒（商店版）";
#endif
            StartPosition = FormStartPosition.CenterScreen; BackColor = Theme.Canvas; Font = Theme.Font(10);
            Icon = Theme.BrandIcon;
            var frame = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var root = new TableLayoutPanel { Dock = DockStyle.Top, Height = 660, ColumnCount = 1, RowCount = 7, Padding = new Padding(24, 20, 24, 16), Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < 7; row++) root.RowStyles.Add(new RowStyle(row == 4 ? SizeType.Percent : SizeType.AutoSize, row == 4 ? 100 : 0));
            frame.Controls.Add(root); Controls.Add(frame);
            var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 10) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var heading = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = true };
            heading.Controls.Add(new PictureBox { Image = Theme.BrandImage, Size = new Size(48, 48), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 14, 0) });
            heading.Controls.Add(Theme.Text("BNBU DDL 提醒", 24, true));
            var badge = Theme.Text("●  后台提醒中", 10, true); badge.ForeColor = Color.FromArgb(30, 128, 94); badge.Margin = new Padding(12, 12, 0, 0);
            header.Controls.Add(heading, 0, 0); header.Controls.Add(badge, 1, 0); root.Controls.Add(header, 0, 0);
            var intro = Theme.Text("提前 1 天提醒  ·  读取本机清单  ·  Edge 关闭后也能提醒"); intro.ForeColor = Theme.Muted; root.Controls.Add(intro, 0, 1);
            var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0, 4, 0, 16) };
            root.Controls.Add(toolbar, 0, 2);
            var add = Theme.Button("＋ 添加作业", 0, 0, 135, true); add.Click += (s, e) => AddTask(); toolbar.Controls.Add(add);
            var refresh = Theme.Button("读取本机数据", 0, 0, 135); refresh.Click += (s, e) => RefreshTasks(true); toolbar.Controls.Add(refresh);
            var import = Theme.Button("导入清单", 0, 0, 110); import.Click += (s, e) => Import(); toolbar.Controls.Add(import);
            var portal = Theme.Button("登录并同步", 0, 0, 125); portal.Click += (s, e) => OpenPortal(); toolbar.Controls.Add(portal);
            var demo = Theme.Button("试一下提醒", 0, 0, 125); demo.Click += (s, e) => ShowDemo(); toolbar.Controls.Add(demo);
#if STORE_BUILD
            refresh.Text = "刷新本机清单";
            var startup = Theme.Button("开机提醒设置", 0, 0, 135);
            startup.Click += (s, e) => {
                try { Process.Start("ms-settings:startupapps"); }
                catch { MessageBox.Show(this, "请打开 Windows 设置 → 应用 → 启动，开启 BNBU DDL Reminder。", "开机提醒"); }
            }; toolbar.Controls.Add(startup);
            intro.Text = "提前 1 天提醒  ·  作业保存在本机  ·  可在开机提醒设置中开启自动启动";
#endif
            summary = Theme.Text("正在读取…", 13, true); summary.Margin = new Padding(0, 4, 0, 12); root.Controls.Add(summary, 0, 3);
            cards = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.TopDown, BackColor = Theme.Canvas, Margin = Padding.Empty };
            root.Controls.Add(cards, 0, 4);
            empty = Theme.Text("还没有作业。点击“登录并同步”，在程序里登录自己的 iSpace。", 13); empty.Margin = new Padding(8, 16, 8, 16);
            status = Theme.Text("", 9); status.ForeColor = Theme.Muted; status.Margin = new Padding(0, 12, 0, 8); root.Controls.Add(status, 0, 5);
            var hint = Theme.Text("关闭窗口会留在右下角托盘继续提醒。电脑需开机并登录；睡眠或关机时不弹窗。", 9); hint.ForeColor = Theme.Muted; root.Controls.Add(hint, 0, 6);
#if STORE_BUILD
            hint.Text = "关闭窗口后在托盘提醒；请在 Windows 启动设置中开启本应用。电脑需开机并登录。";
#endif
            bool sizing = false;
            Action fit = () => {
                if (sizing || root.ClientSize.Width < 1) return;
                sizing = true;
                try {
                    int width = Math.Max(160, root.ClientSize.Width - root.Padding.Horizontal);
                    foreach (var label in new[] { intro, summary, status, hint }) label.MaximumSize = new Size(width, 0);
                    int fixedHeight = root.Padding.Vertical;
                    foreach (Control c in new Control[] { header, intro, toolbar, summary, status, hint }) fixedHeight += c.GetPreferredSize(new Size(width, 0)).Height + c.Margin.Vertical;
                    int wanted = Math.Max(frame.ClientSize.Height, fixedHeight + (int)Math.Ceiling(160 * DeviceDpi / 96f));
                    if (root.Height != wanted) root.Height = wanted;
                } finally { sizing = false; }
            };
            root.Layout += (s, e) => fit(); frame.SizeChanged += (s, e) => fit();
            var menu = new ContextMenuStrip(); menu.Items.Add("打开作业清单", null, (s, e) => BringUp()); menu.Items.Add("登录并同步", null, (s, e) => OpenPortal()); menu.Items.Add("退出程序", null, (s, e) => { quitting = true; Close(); });
            tray = new NotifyIcon { Text = "BNBU DDL 提醒 · 提前 1 天", Icon = Theme.BrandIcon, Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += (s, e) => BringUp();
            FormClosing += (s, e) => { if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            FormClosed += (s, e) => { tray.Visible = false; tray.Dispose(); clock.Dispose(); if (portalForm != null) portalForm.Dispose(); };
            cards.SizeChanged += (s, e) => ResizeCards();
            clock = new System.Windows.Forms.Timer { Interval = 1000 }; int ticks = 0;
            clock.Tick += (s, e) => {
                if (showEvent.WaitOne(0)) BringUp();
                if (++ticks % 30 == 0) { try { RefreshTasks(false); CheckReminders(); } catch (Exception ex) { Store.Log(ex); } }
                if (ticks % 1800 == 0 && portalForm != null && !portalForm.IsDisposed) portalForm.BackgroundRefresh();
            }; clock.Start();
            Shown += (s, e) => {
                RefreshTasks(false); if (background) Hide(); CheckReminders();
#if !STORE_BUILD
                if (!background && Store.Load().tasks.Count == 0) BeginInvoke(new Action(OpenPortal));
                else
#endif
                if (Directory.Exists(Path.Combine(Store.Root, "portal-profile"))) {
                    portalForm = new PortalForm(() => RefreshTasks(true), null, null); portalForm.Show(); portalForm.Hide();
                }
            };
            Theme.Finish(this);
        }
        void ResizeCards() {
            int width = Math.Max(200, cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 12);
            empty.MaximumSize = new Size(Math.Max(100, width - 16), 0);
            foreach (Control card in cards.Controls) if (card != empty) card.Width = width;
        }
        void OpenPortal() {
            if (portalForm == null || portalForm.IsDisposed) portalForm = new PortalForm(() => RefreshTasks(true), null, null);
            portalForm.Show(); portalForm.WindowState = FormWindowState.Normal; portalForm.Activate();
        }
        void BringUp() { Show(); WindowState = FormWindowState.Normal; Activate(); RefreshTasks(false); }
        void AddTask() { using (var editor = new TaskEditor()) if (editor.ShowDialog(this) == DialogResult.OK) { Store.Merge(new TaskFile { tasks = new List<DdlTask> { editor.Result }, exportedAt = Store.Now }); RefreshTasks(true); CheckReminders(); } }
        void Import() {
            using (var dialog = new OpenFileDialog { Filter = "作业清单 (*.json)|*.json", Title = "选择本机 DDL 清单" }) if (dialog.ShowDialog(this) == DialogResult.OK) {
                try { int count = Store.ImportFile(dialog.FileName); RefreshTasks(true); MessageBox.Show(this, "已读取 " + count + " 项作业。", "读取完成"); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "清单未能读取"); }
            }
        }
        public void RefreshTasks(bool force) {
            Store.ImportLocal(); var file = Store.Load(); var state = Store.LoadState(); long now = Store.Now;
            int outstanding = file.tasks.Count(t => t.dueAt > now && !Store.Done(t, state));
            summary.Text = outstanding + " 项待办" + "     /     " + file.tasks.Count + " 项已保存在本机";
            string syncTime = file.exportedAt > 0 ? Store.LocalTime(file.exportedAt).ToString("M月d日 HH:mm") : "尚未同步";
            status.Text = "最近更新：" + syncTime + "   ·   点击“登录并同步”更新作业；提醒由本机程序完成";
            string sig = Store.Json().Serialize(file) + Store.Json().Serialize(state) + DateTime.Now.ToString("yyyyMMddHHmm");
            if (!force && sig == lastSignature) return; lastSignature = sig;
            cards.SuspendLayout();
            foreach (Control c in cards.Controls.Cast<Control>().ToArray()) { cards.Controls.Remove(c); if (c != empty) c.Dispose(); }
            if (file.tasks.Count == 0) cards.Controls.Add(empty);
            foreach (var task in file.tasks.OrderBy(t => Store.Done(t, state) ? 1 : 0).ThenBy(t => t.dueAt)) cards.Controls.Add(Card(task, state, now));
            cards.ResumeLayout(); ResizeCards();
        }
        bool BridgeReady() { try { using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Edge\NativeMessagingHosts\cn.bnbu.ddl_reminder")) return k != null; } catch { return false; } }
        Panel Card(DdlTask t, UserState state, long now) {
            bool done = Store.Done(t, state), late = t.dueAt <= now;
            var card = new Panel { Width = Math.Max(200, cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 12), Margin = new Padding(4, 0, 4, 12), BackColor = Color.White };
            Color accent = done ? Color.FromArgb(174, 184, 199) : late ? Color.FromArgb(228, 145, 66) : Theme.Blue;
            card.Paint += (s, e) => { using (var brush = new SolidBrush(accent)) e.Graphics.FillRectangle(brush, 0, 0, Math.Max(3, card.DeviceDpi / 24), card.Height); };
            var title = Theme.Text(t.title, 12, true); card.Controls.Add(title);
            var course = Theme.Text(t.course ?? "个人作业", 9); course.ForeColor = Theme.Muted; card.Controls.Add(course);
            string until = done ? "已完成" : late ? "已过截止时间" : "剩余 " + Math.Max(1, Math.Ceiling((t.dueAt - now) / 3600000.0)) + " 小时";
            var date = Theme.Text("截止  " + Store.LocalTime(t.dueAt).ToString("M月d日 ddd HH:mm") + "     ·     " + until, 10);
            date.ForeColor = late && !done ? Color.FromArgb(176, 102, 34) : Theme.Blue; card.Controls.Add(date);
            var mark = Theme.Button(done ? "恢复待办" : "标记完成", 0, 0, 115);
            mark.Click += (s, e) => { var u = Store.LoadState(); if (Store.Done(t, u)) u.completed.Remove(t.id); else u.completed[t.id] = t.dueAt; Store.SaveState(u); RefreshTasks(true); }; card.Controls.Add(mark);
            Button open = null;
            if (!String.IsNullOrEmpty(t.url)) { open = Theme.Button("打开作业", 0, 0, 115); open.Click += (s, e) => Store.OpenAssignment(t); card.Controls.Add(open); }
            bool arranging = false;
            card.Layout += (s, e) => {
                if (arranging) return; arranging = true;
                try {
                    Func<int, int> px = value => (int)Math.Ceiling(value * card.DeviceDpi / 96f);
                    int actionsWidth = 0, actionBottom = px(14);
                    foreach (var button in new[] { mark, open }.Where(b => b != null)) {
                        button.MinimumSize = new Size(px(115), px(38)); button.Padding = new Padding(px(10), px(8), px(10), px(8));
                        button.Size = button.GetPreferredSize(Size.Empty); actionsWidth = Math.Max(actionsWidth, button.Width);
                    }
                    int textWidth = Math.Max(px(50), card.ClientSize.Width - actionsWidth - px(58));
                    int y = px(14);
                    foreach (var label in new[] { title, course, date }) {
                        label.MaximumSize = new Size(textWidth, 0); label.Size = label.GetPreferredSize(new Size(textWidth, 0)); label.Location = new Point(px(20), y); y += label.Height + px(8);
                    }
                    foreach (var button in new[] { mark, open }.Where(b => b != null)) {
                        button.Location = new Point(card.ClientSize.Width - px(16) - actionsWidth, actionBottom); actionBottom += button.Height + px(10);
                    }
                    int height = Math.Max(y, actionBottom) + px(8);
                    if (card.Height != height) card.Height = height;
                } finally { arranging = false; }
            };
            card.DpiChangedAfterParent += (s, e) => card.PerformLayout();
            card.PerformLayout(); return card;
        }
        void CheckReminders() {
            var state = Store.LoadState(); long now = Store.Now;
            foreach (var task in Store.Load().tasks) {
                string key = Store.Key(task);
                if (!Store.ShouldRemind(task, state, now) || openReminders.Contains(key)) continue;
                var popup = new ReminderForm(task, false);
                popup.Snooze += () => { var u = Store.LoadState(); u.snoozed[key] = Store.Now + 1800000L; Store.SaveState(u); };
                popup.MarkDone += () => { var u = Store.LoadState(); u.completed[task.id] = task.dueAt; Store.SaveState(u); RefreshTasks(true); };
                popup.Shown += (s, e) => { var u = Store.LoadState(); u.notified[key] = Store.Now; u.snoozed.Remove(key); Store.SaveState(u); };
                popup.FormClosed += (s, e) => openReminders.Remove(key);
                openReminders.Add(key); popup.Show();
                tray.ShowBalloonTip(10000, "BNBU 作业提醒", task.title + " · " + Store.LocalTime(task.dueAt).ToString("M月d日 HH:mm") + " 截止", ToolTipIcon.Info);
            }
        }
        void ShowDemo() { new ReminderForm(new DdlTask { id = "demo", title = "这是一条测试提醒", course = "正式提醒也会这样弹出，不需要打开 Edge。", dueAt = Store.Now + 86400000L }, true).Show(); }
    }
    public class ReminderForm : Form {
        public event Action Snooze;
        public event Action MarkDone;
        public ReminderForm(DdlTask task, bool demo) {
            Theme.Configure(this);
            Icon = Theme.BrandIcon;
            Text = demo ? "BNBU 测试提醒" : "BNBU 作业提醒"; ClientSize = new Size(510, 290); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; TopMost = true; BackColor = Color.White; StartPosition = FormStartPosition.CenterScreen; Font = Theme.Font(10);
            Controls.Add(Theme.Label(demo ? "提醒已经送达" : task.dueAt < Store.Now ? "这项作业已截止" : "这项作业将在 1 天内截止", 25, 24, 460, 35, 18, true));
            Controls.Add(Theme.Label(task.title, 25, 80, 460, 52, 12, true));
            var course = Theme.Label(task.course, 25, 140, 460, 34, 9); course.ForeColor = Theme.Muted; Controls.Add(course);
            Controls.Add(Theme.Label("截止时间：" + Store.LocalTime(task.dueAt).ToString("M月d日 HH:mm"), 25, 183, 460, 25, 11));
            var open = Theme.Button(demo ? "知道了" : "打开作业", 25, 230, 145, true); open.Click += (s, e) => { if (!demo) Store.OpenAssignment(task); Close(); }; Controls.Add(open);
            if (!demo) {
                var later = Theme.Button("30 分钟后提醒", 183, 230, 145); later.Click += (s, e) => { if (Snooze != null) Snooze(); Close(); }; Controls.Add(later);
                var done = Theme.Button("标记完成", 341, 230, 140); done.Click += (s, e) => { if (MarkDone != null) MarkDone(); Close(); }; Controls.Add(done);
            }
            Shown += (s, e) => System.Media.SystemSounds.Asterisk.Play();
            Theme.Finish(this);
        }
    }
    public class TaskEditor : Form {
        public DdlTask Result;
        public TaskEditor() {
            Theme.Configure(this);
            Icon = Theme.BrandIcon;
            Text = "添加作业"; ClientSize = new Size(470, 300); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; BackColor = Color.White; Font = Theme.Font(10);
            Controls.Add(Theme.Label("作业名称", 24, 25, 110, 25)); var title = new TextBox { Location = new Point(135, 23), Width = 305, MaxLength = 500 }; Controls.Add(title);
            Controls.Add(Theme.Label("课程", 24, 80, 110, 25)); var course = new TextBox { Location = new Point(135, 78), Width = 305, MaxLength = 300 }; Controls.Add(course);
            Controls.Add(Theme.Label("截止时间", 24, 135, 110, 25)); var date = new DateTimePicker { Location = new Point(135, 131), Width = 305, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm", Value = DateTime.Now.AddDays(2) }; Controls.Add(date);
            Controls.Add(Theme.Label("默认提前 1 天提醒；已在 1 天内的作业会尽快提醒。", 24, 187, 420, 35, 9));
            var save = Theme.Button("保存并提醒", 285, 239, 155, true); save.Click += (s, e) => {
                if (String.IsNullOrWhiteSpace(title.Text)) { title.Focus(); return; }
                Result = new DdlTask { id = "manual-" + Guid.NewGuid().ToString("N"), title = title.Text.Trim(), course = course.Text.Trim(), dueAt = new DateTimeOffset(date.Value).ToUnixTimeMilliseconds() }; DialogResult = DialogResult.OK;
            }; Controls.Add(save); AcceptButton = save; Theme.Finish(this);
        }
    }
    public static class SelfTest {
        public static int Run(string output) {
            string original = Store.Root, isolated = Path.Combine(Path.GetTempPath(), "bnbu-selftest-" + Guid.NewGuid().ToString("N"));
            var lines = new List<string>();
            try {
                Store.Root = isolated;
                long now = 1794902400000L;
                var task = new DdlTask { id = "1", title = "测试中文 DDL", dueAt = now + 86400000L, url = "https://ispace.bnbu.edu.cn/mod/assign/view.php?id=1" };
#if STORE_BUILD
                Directory.CreateDirectory(isolated);
                string selectedFile = Path.Combine(isolated, "tasks.json");
                File.WriteAllText(selectedFile, Store.Json().Serialize(new TaskFile { tasks = new List<DdlTask> { task } }), Encoding.UTF8);
                Store.ImportLocal();
                Assert(!File.Exists(Store.DataFile), "Store build does not automatically import legacy files", lines);
                Assert(Store.ImportFile(selectedFile) == 1 && Store.Load().tasks.Count == 1, "explicit JSON import works in Store build", lines);
#endif
                var state = new UserState();
                Assert(!Store.ShouldRemind(task, state, now - 1), "before 24h boundary", lines);
                Assert(Store.ShouldRemind(task, state, now), "exact 24h boundary", lines);
                state.notified[Store.Key(task)] = now;
                Assert(!Store.ShouldRemind(task, state, now + 1), "no duplicate notification", lines);
                state.snoozed[Store.Key(task)] = now + 1800000L;
                Assert(!Store.ShouldRemind(task, state, now + 1000L) && Store.ShouldRemind(task, state, now + 1800000L), "snooze and resume", lines);
                state.completed[task.id] = task.dueAt;
                Assert(!Store.ShouldRemind(task, state, now + 1800000L), "completed task is quiet", lines);
                state = new UserState();
                Assert(Store.ShouldRemind(task, state, task.dueAt + 1000) && !Store.ShouldRemind(task, state, task.dueAt + 86400001L), "offline catch-up window", lines);
                Store.Merge(new TaskFile { exportedAt = now, tasks = new List<DdlTask> { task } });
                task.dueAt += 3600000L;
                Store.Merge(new TaskFile { exportedAt = now + 1, tasks = new List<DdlTask> { task } });
                var loaded = Store.Load();
                Assert(loaded.tasks.Count == 1 && loaded.tasks[0].title == "测试中文 DDL" && loaded.tasks[0].dueAt == task.dueAt, "UTF8 persistence and deadline update", lines);
                Assert(Store.Validate(new[] { new DdlTask { id = "bad", title = "bad", dueAt = 1 } }).Count == 0, "reject invalid input", lines);
                File.WriteAllLines(output, lines, Encoding.UTF8); return 0;
            } catch (Exception ex) { lines.Add("FAIL " + ex); File.WriteAllLines(output, lines, Encoding.UTF8); return 1; }
            finally { Store.Root = original; try { Directory.Delete(isolated, true); } catch { } }
        }
        static void Assert(bool condition, string label, List<string> lines) { if (!condition) throw new Exception(label); lines.Add("PASS " + label); }
    }
}
