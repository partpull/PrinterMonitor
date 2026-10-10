using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace PrinterMonitor
{
    /// <summary>提亮深色配色 + 高 DPI 缩放。所有界面尺寸按 96 DPI 的设计值书写，绘制时统一乘以 Scale。</summary>
    internal static class Theme
    {
        // 背景与容器
        public static readonly Color Bg = Color.FromArgb(31, 35, 43);
        public static readonly Color HeroCard = Color.FromArgb(62, 71, 89);
        public static readonly Color HeroCardEnd = Color.FromArgb(46, 52, 64);
        public static readonly Color Card = Color.FromArgb(74, 84, 102);
        public static readonly Color CardEnd = Color.FromArgb(55, 62, 77);
        public static readonly Color CardHover = Color.FromArgb(90, 101, 121);
        public static readonly Color CardHoverEnd = Color.FromArgb(68, 76, 92);
        public static readonly Color Border = Color.FromArgb(86, 97, 118);
        public static readonly Color HeroBorder = Color.FromArgb(78, 88, 108);
        public static readonly Color BorderStrong = Color.FromArgb(116, 128, 148);
        public static readonly Color Divider = Color.FromArgb(39, 44, 53);
        public static readonly Color SelectedBorder = Color.FromArgb(198, 202, 212);

        // 文字
        public static readonly Color Text = Color.FromArgb(243, 245, 249);
        public static readonly Color TextMuted = Color.FromArgb(176, 185, 202);
        public static readonly Color TextDim = Color.FromArgb(150, 160, 177);

        // 状态色
        public static readonly Color Green = Color.FromArgb(62, 224, 143);
        public static readonly Color Yellow = Color.FromArgb(240, 176, 46);
        public static readonly Color Blue = Color.FromArgb(107, 182, 255);
        public static readonly Color Gray = Color.FromArgb(139, 148, 166);
        public static readonly Color Red = Color.FromArgb(240, 96, 79);

        // 自绘按钮与开关
        public static readonly Color BtnFill = Color.FromArgb(48, 53, 63);
        public static readonly Color BtnBorder = Color.FromArgb(68, 75, 90);
        public static readonly Color BtnHover = Color.FromArgb(58, 64, 76);
        public static readonly Color BtnDown = Color.FromArgb(43, 48, 58);
        public static readonly Color BtnDisabledFill = Color.FromArgb(43, 48, 58);
        public static readonly Color BtnDisabledBorder = Color.FromArgb(58, 64, 77);
        public static readonly Color BtnDisabledText = Color.FromArgb(113, 121, 138);
        public static readonly Color SwitchOff = Color.FromArgb(60, 66, 78);
        public static readonly Color SwitchKnobOff = Color.FromArgb(147, 155, 171);
        public static readonly Color SwitchKnobOn = Color.FromArgb(255, 255, 255);

        /// <summary>卡片顶部内高光的透明度（约 12.5%）。</summary>
        private const int SheenAlpha = 32;

        private static float _scale = 1f;

        public static float Scale { get { return _scale; } }

        public static void InitScale(float scale)
        {
            _scale = scale <= 0f ? 1f : scale;
        }

        /// <summary>把设计像素换算成当前 DPI 下的像素。</summary>
        public static int Px(double designPx)
        {
            return (int)Math.Round(designPx * _scale);
        }

        public static float Pf(double designPx)
        {
            return (float)(designPx * _scale);
        }

        public static readonly string Family = ResolveFamily();

        private static string ResolveFamily()
        {
            string[] wanted = new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" };
            try
            {
                using (InstalledFontCollection installed = new InstalledFontCollection())
                {
                    FontFamily[] families = installed.Families;
                    for (int w = 0; w < wanted.Length; w++)
                    {
                        for (int i = 0; i < families.Length; i++)
                        {
                            if (string.Equals(families[i].Name, wanted[w], StringComparison.OrdinalIgnoreCase))
                                return families[i].Name;
                        }
                    }
                }
            }
            catch (Exception) { }
            return "Segoe UI";
        }

        /// <summary>按像素字号（设计值）创建字体。</summary>
        public static Font FontAt(double designPx, FontStyle style)
        {
            return new Font(Family, Pf(designPx), style, GraphicsUnit.Pixel);
        }

        // 共享字体：定时刷新会反复重绘，字体必须全局复用，否则会耗尽 GDI 句柄。
        private static Font _fHeroLabel;
        private static Font _fHeroName;
        private static Font _fSub;
        private static Font _fCardName;
        private static Font _fPill;
        private static Font _fSection;

        public static Font HeroLabel
        {
            get { if (_fHeroLabel == null) _fHeroLabel = FontAt(11, FontStyle.Regular); return _fHeroLabel; }
        }

        public static Font HeroName
        {
            get { if (_fHeroName == null) _fHeroName = FontAt(20, FontStyle.Bold); return _fHeroName; }
        }

        public static Font Sub
        {
            get { if (_fSub == null) _fSub = FontAt(12, FontStyle.Regular); return _fSub; }
        }

        public static Font CardName
        {
            get { if (_fCardName == null) _fCardName = FontAt(14, FontStyle.Bold); return _fCardName; }
        }

        public static Font Pill
        {
            get { if (_fPill == null) _fPill = FontAt(11, FontStyle.Regular); return _fPill; }
        }

        public static Font Section
        {
            get { if (_fSection == null) _fSection = FontAt(12, FontStyle.Regular); return _fSection; }
        }

        public static Color StateColor(PrinterState state)
        {
            switch (state)
            {
                case PrinterState.Active: return Green;
                case PrinterState.Warning: return Yellow;
                case PrinterState.Available: return Blue;
                case PrinterState.Error: return Red;
                default: return Gray;
            }
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int h = bounds.Height;
            int w = bounds.Width;
            if (radius * 2 > h) radius = h / 2;
            if (radius * 2 > w) radius = w / 2;
            if (radius < 1)
            {
                path.AddRectangle(bounds);
                return path;
            }
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180f, 90f);
            path.AddArc(bounds.X + w - d, bounds.Y, d, d, 270f, 90f);
            path.AddArc(bounds.X + w - d, bounds.Y + h - d, d, d, 0f, 90f);
            path.AddArc(bounds.X, bounds.Y + h - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color fill)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath path = RoundedRect(r, radius))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
            }
        }

        /// <summary>从上到下的竖直渐变圆角填充。</summary>
        public static void FillRoundedGradient(Graphics g, Rectangle r, int radius, Color top, Color bottom)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath path = RoundedRect(r, radius))
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)), top, bottom, LinearGradientMode.Vertical))
            {
                g.FillPath(brush, path);
            }
        }

        /// <summary>沿圆角卡片顶部画一条内高光，制造"上沿受光"的立体感。</summary>
        public static void DrawTopSheen(Graphics g, Rectangle r, int radius, int inset)
        {
            if (r.Width <= 0) return;
            GraphicsState state = g.Save();
            try
            {
                using (GraphicsPath path = RoundedRect(r, radius))
                {
                    g.SetClip(path);
                    using (Pen pen = new Pen(Color.FromArgb(SheenAlpha, 255, 255, 255), 1f))
                        g.DrawLine(pen, r.X + inset, r.Y + 1, r.Right - inset, r.Y + 1);
                }
            }
            finally { g.Restore(state); }
        }

        public static void DrawRoundedBorder(Graphics g, Rectangle r, int radius, Color color)
        {
            using (GraphicsPath path = RoundedRect(r, radius))
            using (Pen pen = new Pen(color, 1f))
            {
                g.DrawPath(pen, path);
            }
        }

        /// <summary>量出标签的宽度（不含左右内边距之外的东西）。</summary>
        public static int MeasurePill(Graphics g, string text, Font font)
        {
            Size size = TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            return size.Width + Px(7) * 2;
        }

        /// <summary>以左边界为基准绘制圆角标签，返回其宽度。</summary>
        public static int DrawPillAt(Graphics g, int left, int centerY, string text, Font font,
                                     Color textColor, Color borderColor, Color fillColor)
        {
            int w = MeasurePill(g, text, font);
            int h = Px(20);
            Rectangle rect = new Rectangle(left, centerY - h / 2, w, h);
            using (GraphicsPath path = RoundedRect(rect, h / 2))
            {
                if (fillColor.A > 0)
                {
                    using (SolidBrush brush = new SolidBrush(fillColor)) g.FillPath(brush, path);
                }
                if (borderColor.A > 0)
                {
                    using (Pen pen = new Pen(borderColor, 1f)) g.DrawPath(pen, path);
                }
            }
            TextRenderer.DrawText(g, text, font, rect, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return w;
        }

        /// <summary>以右边界为基准绘制圆角标签（右对齐用），返回其宽度。</summary>
        public static int DrawPill(Graphics g, int right, int centerY, string text, Font font,
                                   Color textColor, Color borderColor, Color fillColor)
        {
            int w = MeasurePill(g, text, font);
            DrawPillAt(g, right - w, centerY, text, font, textColor, borderColor, fillColor);
            return w;
        }

        /// <summary>测量一段文本宽度。</summary>
        public static int MeasureWidth(Graphics g, string text, Font font)
        {
            return TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }

        /// <summary>单行截断文本绘制。extra 传 TextFormatFlags.Left 或 TextFormatFlags.Right。</summary>
        public static void DrawText(Graphics g, string text, Font font, Rectangle rect, Color color, TextFormatFlags extra)
        {
            TextRenderer.DrawText(g, text, font, rect, color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | extra);
        }

        public static void DrawDot(Graphics g, int centerX, int centerY, int radius, Color color, bool hollow)
        {
            Rectangle r = new Rectangle(centerX - radius, centerY - radius, radius * 2, radius * 2);
            if (hollow)
            {
                using (Pen pen = new Pen(Color.FromArgb(150, color), Pf(1.6)))
                    g.DrawEllipse(pen, r);
            }
            else
            {
                using (SolidBrush brush = new SolidBrush(color))
                    g.FillEllipse(brush, r);
            }
        }

        /// <summary>状态灯：在线为「柔光外圈 + 实心点」，离线保持空心环。</summary>
        public static void DrawStatusDot(Graphics g, int centerX, int centerY, int dotRadius, Color color, bool hollow)
        {
            if (hollow)
            {
                DrawDot(g, centerX, centerY, dotRadius, color, true);
                return;
            }
            int glow = dotRadius * 2;
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(48, color)))
                g.FillEllipse(brush, new Rectangle(centerX - glow, centerY - glow, glow * 2, glow * 2));
            using (SolidBrush brush = new SolidBrush(color))
                g.FillEllipse(brush, new Rectangle(centerX - dotRadius, centerY - dotRadius, dotRadius * 2, dotRadius * 2));
        }

        /// <summary>在 24×24 的设计坐标系里绘制打印机线条图标。x、y 为图标左上角，size 为边长（物理像素）。</summary>
        public static void DrawPrinterGlyph(Graphics g, int x, int y, int size, Color color, float stroke)
        {
            if (size <= 0) return;
            GraphicsState state = g.Save();
            try
            {
                float s = size / 24f;
                g.TranslateTransform(x, y);
                g.ScaleTransform(s, s);
                using (Pen pen = new Pen(color, stroke))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    // 上方纸盒
                    g.DrawLines(pen, new PointF[] {
                        new PointF(7f, 9.5f), new PointF(7f, 4.5f),
                        new PointF(17f, 4.5f), new PointF(17f, 9.5f) });

                    // 机身：底部中间留出出纸口
                    using (GraphicsPath body = new GraphicsPath())
                    {
                        body.AddLine(6.5f, 18.5f, 5.2f, 18.5f);
                        body.AddArc(2.7f, 13.5f, 5f, 5f, 90f, 90f);
                        body.AddArc(2.7f, 9.7f, 5f, 5f, 180f, 90f);
                        body.AddLine(5.2f, 9.7f, 18.8f, 9.7f);
                        body.AddArc(16.3f, 9.7f, 5f, 5f, 270f, 90f);
                        body.AddArc(16.3f, 13.5f, 5f, 5f, 0f, 90f);
                        body.AddLine(18.8f, 18.5f, 17.3f, 18.5f);
                        g.DrawPath(pen, body);
                    }

                    // 出纸
                    using (GraphicsPath tray = RoundedRect(new Rectangle(7, 14, 10, 7), 1))
                        g.DrawPath(pen, tray);
                }
            }
            finally { g.Restore(state); }
        }
    }
}
