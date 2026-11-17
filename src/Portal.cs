using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BnbuReminder {
    public class PortalForm : Form {
        WebView2 web;
        Label message, address;
        System.Windows.Forms.Timer scan;
        Action updated;
        bool busy, ready;
        int pages;
        string signature = "", fixture, report;
        public PortalForm(Action onUpdated, string fixturePath, string reportPath) {
            Theme.Configure(this);
            Icon = Theme.BrandIcon;
            updated = onUpdated; fixture = fixturePath; report = reportPath;
            Text = "登录并同步 · BNBU DDL 提醒"; Size = new Size(1100, 800); MinimumSize = new Size(850, 600); StartPosition = FormStartPosition.CenterScreen; BackColor = Theme.Canvas;
            var top = new Panel { Dock = DockStyle.Top, Height = 105 };
            var home = Theme.Button("返回仪表盘", 16, 12, 128, true); home.Click += (s, e) => { if (ready) { pages = 0; web.CoreWebView2.Navigate("https://ispace.bnbu.edu.cn/my/"); } }; top.Controls.Add(home);
            var reload = Theme.Button("刷新作业", 156, 12, 110); reload.Click += (s, e) => BackgroundRefresh(); top.Controls.Add(reload);
            var done = Theme.Button("返回作业清单", 279, 12, 140); done.Click += (s, e) => Hide(); top.Controls.Add(done);
            message = Theme.Label("请在下方学校页面登录。程序会自动保存仪表盘中的作业。", 435, 18, 630, 40, 10); message.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; top.Controls.Add(message);
            address = Theme.Label("正在打开学校门户…", 18, 68, 1040, 26, 9); address.ForeColor = Theme.Muted; address.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; top.Controls.Add(address);
            web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.White };
            Controls.Add(web); Controls.Add(top);
            scan = new System.Windows.Forms.Timer { Interval = 1800 }; scan.Tick += async (s, e) => await Scan();
            Shown += async (s, e) => { if (!ready) await Initialize(); };
            FormClosing += (s, e) => { if (fixture == null && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            Disposed += (s, e) => { scan.Dispose(); web.Dispose(); };
            Theme.Finish(this);
        }
        async Task Initialize() {
            try {
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Store.Root, "portal-profile"));
                await web.EnsureCoreWebView2Async(env); ready = true;
                web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                web.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                web.CoreWebView2.NavigationStarting += (s, e) => {
                    if (fixture != null) { File.AppendAllText(report + ".events", "navigate " + e.Uri + Environment.NewLine); return; }
                    Uri uri;
                    if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out uri) || (uri.Scheme != "https" && e.Uri != "about:blank")) e.Cancel = true;
                };
                web.CoreWebView2.NewWindowRequested += (s, e) => { e.Handled = true; Uri uri; if (Uri.TryCreate(e.Uri, UriKind.Absolute, out uri) && uri.Scheme == "https") web.CoreWebView2.Navigate(uri.AbsoluteUri); };
                web.CoreWebView2.NavigationCompleted += async (s, e) => {
                    if (fixture != null) File.AppendAllText(report + ".events", "complete " + e.IsSuccess + " " + e.WebErrorStatus + Environment.NewLine);
                    address.Text = web.Source == null ? "" : web.Source.ToString();
                    if (!IsDashboard() && fixture == null) message.Text = "请在学校页面完成登录，然后进入 Dashboard。";
                    await Scan();
                };
                if (fixture != null) web.CoreWebView2.NavigateToString(File.ReadAllText(fixture));
                else web.CoreWebView2.Navigate("https://ispace.bnbu.edu.cn/my/");
                scan.Start();
                if (fixture != null) {
                    await Task.Delay(11000);
                    var tasks = Store.Load().tasks;
                    bool ok = tasks.Count == 2 && tasks.Exists(t => t.title == "集成测试作业一") && tasks.Exists(t => t.title == "集成测试作业二") && tasks.TrueForAll(t => t.dueAt > 2000000000000L);
                    File.WriteAllText(report, (ok ? "PASS" : "FAIL") + " embedded WebView2 initialization, Timeline extraction, pagination, UTF8 persistence; tasks=" + tasks.Count);
                    Environment.ExitCode = ok ? 0 : 1; Close();
                }
            } catch (Exception ex) {
                Store.Log(ex); message.Text = "门户未能打开，请检查网络或重新运行安装包修复组件。";
#if STORE_BUILD
                message.Text = "门户未能打开，请检查网络和 Microsoft Edge WebView2 Runtime。";
                if (fixture == null && ex is WebView2RuntimeNotFoundException) {
                    if (MessageBox.Show(this, "学校登录需要 Microsoft Edge WebView2 Runtime。是否打开微软官方下载页面？本机已有作业仍可提醒。", "需要 WebView2 Runtime", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        Process.Start("https://developer.microsoft.com/microsoft-edge/webview2/#download");
                }
#endif
                if (fixture != null) { File.WriteAllText(report, "FAIL " + ex); Environment.ExitCode = 1; Close(); }
            }
        }
        bool IsDashboard() {
            Uri uri = web.Source;
            return uri != null && uri.Scheme == "https" && uri.Host == "ispace.bnbu.edu.cn" && uri.AbsolutePath.StartsWith("/my/");
        }
        public void BackgroundRefresh() {
            if (!ready) return;
            pages = 0;
            if (IsDashboard()) web.Reload();
            else if (Visible) web.CoreWebView2.Navigate("https://ispace.bnbu.edu.cn/my/");
        }
        async Task Scan() {
            if (!ready || busy || (!IsDashboard() && fixture == null)) return;
            busy = true;
            try {
                string script;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PortalExtractor.js"))
                using (var reader = new StreamReader(stream)) script = reader.ReadToEnd();
                string raw = await web.ExecuteScriptAsync(script);
                if (fixture != null) {
                    File.WriteAllText(report + ".raw", raw);
                    File.WriteAllText(report + ".html", await web.ExecuteScriptAsync("document.documentElement.outerHTML"));
                }
                var data = Store.Json().Deserialize<TaskFile>(raw);
                string next = Store.Json().Serialize(data.tasks);
                if (data.tasks.Count > 0 && signature != next) { Store.Merge(data); signature = next; if (updated != null) updated(); }
                message.Text = data.tasks.Count > 0 ? "已同步本页 " + data.tasks.Count + " 项作业。可以返回作业清单。" : "尚未读到作业，正在等 Timeline 加载；请确认已经登录。";
                if (pages < 50) {
                    string clicked = await web.ExecuteScriptAsync("(() => { const b = [...document.querySelectorAll('button')].find(b => /^(show more activities|显示更多活动|更多活动)$/i.test(b.textContent.trim()) && !b.disabled && b.getClientRects().length); if (b) { b.click(); return true; } return false; })()");
                    if (clicked == "true") pages++;
                }
            } catch (Exception ex) { Store.Log(ex); message.Text = "本次读取未完成，已保存的作业仍会提醒。可稍后刷新。"; }
            finally { busy = false; }
        }
    }
}
