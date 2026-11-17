using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

static class MakeIcon {
    static GraphicsPath Round(float x, float y, float w, float h, float r) {
        var p = new GraphicsPath(); float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    static void FillRound(Graphics g, Brush b, float x, float y, float w, float h, float r) { using (var p = Round(x, y, w, h, r)) g.FillPath(b, p); }
    static Bitmap Draw(int size) {
        var b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b)) {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.CompositingQuality = CompositingQuality.HighQuality;
            g.ScaleTransform(size / 1024f, size / 1024f);
            using (var blue = new LinearGradientBrush(new Point(96, 60), new Point(930, 975), Color.FromArgb(65, 111, 248), Color.FromArgb(31, 57, 172))) FillRound(g, blue, 40, 40, 944, 944, 211);
            using (var shadow = new SolidBrush(Color.FromArgb(35, 8, 30, 104))) FillRound(g, shadow, 204, 246, 570, 565, 72);
            FillRound(g, Brushes.White, 190, 224, 570, 564, 72);
            using (var pale = new SolidBrush(Color.FromArgb(230, 237, 255))) {
                using (var pen = new Pen(pale, 13)) g.DrawLine(pen, 228, 372, 722, 372);
                FillRound(g, pale, 273, 440, 76, 76, 19); FillRound(g, pale, 397, 440, 76, 76, 19); FillRound(g, pale, 521, 440, 76, 76, 19);
            }
            using (var rings = new SolidBrush(Color.FromArgb(220, 232, 255))) { FillRound(g, rings, 302, 170, 54, 135, 27); FillRound(g, rings, 592, 170, 54, 135, 27); }
            using (var pen = new Pen(Color.FromArgb(57, 100, 231), 44)) { pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round; g.DrawLines(pen, new[] { new Point(280, 634), new Point(344, 697), new Point(460, 571) }); }
            g.FillEllipse(Brushes.White, 514, 506, 402, 402);
            using (var orange = new LinearGradientBrush(new Point(560, 525), new Point(855, 888), Color.FromArgb(255, 198, 85), Color.FromArgb(255, 139, 43))) g.FillEllipse(orange, 534, 526, 362, 362);
            using (var bell = new GraphicsPath()) {
                bell.StartFigure(); bell.AddBezier(606, 741, 617, 728, 638, 706, 638, 680); bell.AddLine(638, 680, 638, 637);
                bell.AddBezier(638, 637, 638, 543, 792, 543, 792, 637); bell.AddLine(792, 637, 792, 680);
                bell.AddBezier(792, 680, 792, 706, 813, 728, 824, 741); bell.AddBezier(824, 741, 832, 751, 826, 763, 812, 763);
                bell.AddLine(812, 763, 618, 763); bell.AddBezier(618, 763, 604, 763, 598, 751, 606, 741); bell.CloseFigure(); g.FillPath(Brushes.White, bell);
            }
            g.FillEllipse(Brushes.White, 687, 778, 56, 41);
            FillRound(g, Brushes.White, 703, 556, 24, 41, 12);
        }
        return b;
    }
    static int Main(string[] args) {
        Directory.CreateDirectory(args[0]);
        using (var large = Draw(1024)) {
            using (var preview = new Bitmap(512, 512, PixelFormat.Format32bppArgb)) {
                using (var g = Graphics.FromImage(preview)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(large, 0, 0, 512, 512); }
                preview.Save(Path.Combine(args[0], "app-icon.png"), ImageFormat.Png);
            }
            var frames = new List<byte[]>(); int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };
            foreach (int size in sizes) {
                using (var image = new Bitmap(size, size, PixelFormat.Format32bppArgb)) {
                    using (var g = Graphics.FromImage(image)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.CompositingQuality = CompositingQuality.HighQuality; g.DrawImage(large, 0, 0, size, size); }
                    using (var m = new MemoryStream()) {
                        if (size == 256) image.Save(m, ImageFormat.Png);
                        else {
                            using (var dib = new BinaryWriter(m, System.Text.Encoding.UTF8, true)) {
                                int maskStride = ((size + 31) / 32) * 4;
                                dib.Write(40); dib.Write(size); dib.Write(size * 2); dib.Write((ushort)1); dib.Write((ushort)32); dib.Write(0);
                                dib.Write(size * size * 4 + maskStride * size); dib.Write(0); dib.Write(0); dib.Write(0); dib.Write(0);
                                for (int y = size - 1; y >= 0; y--) for (int x = 0; x < size; x++) { var c = image.GetPixel(x, y); dib.Write(c.B); dib.Write(c.G); dib.Write(c.R); dib.Write(c.A); }
                                for (int y = size - 1; y >= 0; y--) {
                                    byte[] mask = new byte[maskStride];
                                    for (int x = 0; x < size; x++) if (image.GetPixel(x, y).A == 0) mask[x / 8] |= (byte)(128 >> (x % 8));
                                    dib.Write(mask);
                                }
                            }
                        }
                        frames.Add(m.ToArray());
                    }
                }
            }
            using (var writer = new BinaryWriter(File.Create(Path.Combine(args[0], "app.ico")))) {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++) { writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length; }
                foreach (var frame in frames) writer.Write(frame);
            }
        }
        Console.WriteLine("Generated calendar + bell icon, 10 sizes (16 to 256 px)."); return 0;
    }
}
