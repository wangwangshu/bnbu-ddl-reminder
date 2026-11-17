using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using BnbuReminder;

static class LayoutChecks {
    static IEnumerable<Control> Descendants(Control parent) {
        yield return parent;
        foreach (Control child in parent.Controls) foreach (var control in Descendants(child)) yield return control;
    }
    [STAThread]
    static int Main(string[] args) {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        var output = new List<string>();
        try {
            foreach (float textScale in new[] { 1f, 1.25f, 1.5f, 2f }) {
                foreach (var size in new[] { new Size(1000, 720), new Size(850, 580) }) {
                    using (var signal = new EventWaitHandle(false, EventResetMode.AutoReset))
                    using (var form = new MainForm(true, signal)) {
                        form.ClientSize = size; form.CreateControl();
                        var list = (FlowLayoutPanel)typeof(MainForm).GetField("cards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                        var task = new DdlTask { id = "layout-fixture", title = "示例长标题：Submit the group presentation and the mathematics problem set for the final course assignment", course = "示例课程：A long course name that must wrap without hiding the deadline", dueAt = DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeMilliseconds(), url = "https://ispace.bnbu.edu.cn/mod/assign/view.php?id=layout-fixture" };
                        var card = (Panel)typeof(MainForm).GetMethod("Card", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { task, new UserState(), Store.Now });
                        list.Controls.Add(card);
                        var fonts = Descendants(form).ToDictionary(c => c, c => new { c.Font.FontFamily, c.Font.Size, c.Font.Style });
                        foreach (var item in fonts) item.Key.Font = new Font(item.Value.FontFamily, item.Value.Size * textScale, item.Value.Style);
                        for (int pass = 0; pass < 5; pass++) {
                            form.PerformLayout();
                            foreach (var control in Descendants(form)) control.PerformLayout();
                            typeof(MainForm).GetMethod("ResizeCards", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
                        }
                        int checkedCount = 0;
                        foreach (var control in Descendants(form).Where(c => c is Label || c is Button)) {
                            if (String.IsNullOrWhiteSpace(control.Text)) continue;
                            var preferred = control.GetPreferredSize(new Size(control.Width, 0));
                            if (control.Height + 2 < preferred.Height) throw new Exception("Clipped height: " + control.Text + " actual=" + control.Height + " required=" + preferred.Height);
                            if (control is Button && control.Width + 2 < control.PreferredSize.Width) throw new Exception("Clipped button width: " + control.Text);
                            if (control.Parent != null && !(control.Parent is ScrollableControl && ((ScrollableControl)control.Parent).AutoScroll)) {
                                if (control.Right > control.Parent.ClientSize.Width + 2 || control.Bottom > control.Parent.ClientSize.Height + 2)
                                    throw new Exception("Control outside parent: " + control.Text);
                            }
                            checkedCount++;
                        }
                        var cardLabels = card.Controls.OfType<Label>().OrderBy(c => c.Top).ToArray();
                        for (int i = 1; i < cardLabels.Length; i++) if (cardLabels[i].Top < cardLabels[i - 1].Bottom) throw new Exception("Task text overlaps");
                        output.Add("PASS viewport " + size + ", text " + textScale.ToString("0%") + ": " + checkedCount + " labels/buttons fit, long task title/course wrap, deadline stays visible");
                    }
                }
            }
            output.Add("Text-size stress checks simulate larger text; physical 125/150/200 percent Windows display configurations and mixed-DPI monitor moves still need device verification.");
            File.WriteAllLines(args[0], output); return 0;
        } catch (Exception ex) { output.Add("FAIL " + ex); File.WriteAllLines(args[0], output); return 1; }
    }
}
