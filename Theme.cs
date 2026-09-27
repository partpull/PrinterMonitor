using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace PrinterMonitor
{
    /// <summary>深色配色 + 高 DPI 缩放。所有界面尺寸按 96 DPI 的设计值书写，绘制时统一乘以 Scale。</summary>
    internal static class Theme
    {
        // 背景与容器
        public static readonly Color Bg = Color.FromArgb(27, 27, 31);
        public static readonly Color HeroCard = Color.FromArgb(33, 33, 38);
        public static readonly Color Card = Color.FromArgb(38, 38, 43);
        public static readonly Color CardHover = Color.FromArgb(47, 47, 53);
        public static readonly Color Border = Color.FromArgb(54, 54, 61);
        public static readonly Color BorderStrong = Color.FromArgb(78, 78, 88);

        // 文字
        public static readonly Color Text = Color.FromArgb(240, 240, 244);
        public static readonly Color TextMuted = Color.FromArgb(152, 152, 164);
        public static readonly Color TextDim = Color.FromArgb(108, 108, 118);

        // 状态色
        public static readonly Color Green = Color.FromArgb(50, 240, 140);
        public static readonly Color Yellow = Color.FromArgb(239, 170, 23);
        public static readonly Color Blue = Color.FromArgb(100, 180, 255);
        public static readonly Color Gray = Color.FromArgb(108, 108, 118);
        public static readonly Color Red = Color.FromArgb(232, 70, 58);

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
            get { if (_fCardName == null) _fCardName = FontAt(13, FontStyle.Bold); return _fCardName; }
        }

        public static Font Pill
        {
            get { if (_fPill == null) _fPill = FontAt(11, FontStyle.Regular); return _fPill; }
        }

        public static Font Section
        {
            get { if (_fSection == null) _fSection = FontAt(11, FontStyle.Regular); return _fSection; }
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
            using (GraphicsPath path = RoundedRect(r, radius))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
            }
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
    }
}
