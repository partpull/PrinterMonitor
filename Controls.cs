using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PrinterMonitor
{
    /// <summary>自绘圆角按钮。</summary>
    internal sealed class SoftButton : Control
    {
        /// <summary>文字左侧是否绘制刷新图标。</summary>
        public bool RefreshIcon;

        private bool _hover;
        private bool _pressed;
        private readonly Font _font;

        public SoftButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
            _font = Theme.Sub;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_pressed) { _pressed = false; Invalidate(); }
            base.OnMouseUp(e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            int radius = Theme.Px(10);

            Color fill, border, text;
            if (!Enabled)
            {
                fill = Theme.BtnDisabledFill;
                border = Theme.BtnDisabledBorder;
                text = Theme.BtnDisabledText;
            }
            else
            {
                fill = _pressed ? Theme.BtnDown : (_hover ? Theme.BtnHover : Theme.BtnFill);
                border = Theme.BtnBorder;
                text = Theme.Text;
            }

            Theme.FillRounded(g, rect, radius, fill);
            Theme.DrawRoundedBorder(g, rect, radius, border);

            string label = Text == null ? "" : Text;
            if (RefreshIcon)
            {
                int iconSize = Theme.Px(14);
                int gap = Theme.Px(6);
                int textW = Theme.MeasureWidth(g, label, _font);
                int groupW = iconSize + gap + textW;
                int startX = (Width - groupW) / 2;
                Theme.DrawRefreshGlyph(g, startX, (Height - iconSize) / 2, iconSize, text, 1.5f);
                Rectangle labelRect = new Rectangle(startX + iconSize + gap, 0, textW + Theme.Px(2), Height);
                TextRenderer.DrawText(g, label, _font, labelRect, text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
            else
            {
                Rectangle textRect = new Rectangle(Theme.Px(4), 0, Math.Max(Theme.Px(10), Width - Theme.Px(8)), Height);
                TextRenderer.DrawText(g, label, _font, textRect, text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }
        }
    }

    /// <summary>自绘开关（文字 + 滑动圆钮），用于「开机自启」。</summary>
    internal sealed class ToggleSwitch : Control
    {
        private const int AnimInterval = 16;
        private const double AnimStep = 0.16;

        private bool _checked;
        private double _pos;          // 0 = 关，1 = 开（用于滑动动画）
        private readonly System.Windows.Forms.Timer _anim;
        private readonly Font _font;

        /// <summary>勾选状态变化时触发。</summary>
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                _anim.Start();
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
            _font = Theme.Sub;

            _anim = new System.Windows.Forms.Timer();
            _anim.Interval = AnimInterval;
            _anim.Tick += delegate(object s, EventArgs e)
            {
                double target = _checked ? 1.0 : 0.0;
                if (Math.Abs(target - _pos) <= AnimStep)
                {
                    _pos = target;
                    _anim.Stop();
                }
                else
                {
                    _pos += _checked ? AnimStep : -AnimStep;
                }
                Invalidate();
            };
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !_checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackW = Theme.Px(42);
            int trackH = Theme.Px(24);
            int trackX = Math.Max(0, Width - trackW);
            int trackY = (Height - trackH) / 2;

            Rectangle textRect = new Rectangle(0, 0, Math.Max(Theme.Px(10), trackX - Theme.Px(8)), Height);
            TextRenderer.DrawText(g, Text == null ? "" : Text, _font, textRect,
                Enabled ? Theme.TextMuted : Theme.TextDim,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            Rectangle track = new Rectangle(trackX, trackY, trackW, trackH);
            // 开启过程中轨道从灰向绿过渡，避免动画期间出现跳色
            int blend = (int)Math.Round(100.0 * (_pos <= 0 ? 0 : (_pos >= 1 ? 1 : _pos)));
            Color trackColor = Blend(Theme.SwitchOff, Theme.Green, blend);
            Theme.FillRounded(g, track, trackH / 2, trackColor);

            int pad = Theme.Px(3);
            int knob = trackH - pad * 2;
            int travel = Math.Max(1, trackW - pad * 2 - knob);
            int knobX = track.X + pad + (int)Math.Round(travel * _pos);
            Color knobColor = Blend(Theme.SwitchKnobOff, Theme.SwitchKnobOn, blend);
            Theme.FillRounded(g, new Rectangle(knobX, track.Y + pad, knob, knob), knob / 2, knobColor);
        }

        private static Color Blend(Color from, Color to, int percent)
        {
            if (percent <= 0) return from;
            if (percent >= 100) return to;
            return Color.FromArgb(
                from.A + (to.A - from.A) * percent / 100,
                from.R + (to.R - from.R) * percent / 100,
                from.G + (to.G - from.G) * percent / 100,
                from.B + (to.B - from.B) * percent / 100);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _anim != null) _anim.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>顶部「当前活跃打印机」大卡片。</summary>
    internal sealed class HeroPanel : Control
    {
        public PrinterInfo Current;
        public bool HasData;
        public bool HasDefault = true;
        public string ErrorText = "";
        public int OnlineCount;
        public int TotalCount;
        public DateTime LastUpdate = DateTime.MinValue;

        private readonly Font _fLabel;
        private readonly Font _fName;
        private readonly Font _fSub;

        public HeroPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            _fLabel = Theme.HeroLabel;
            _fName = Theme.HeroName;
            _fSub = Theme.Sub;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle card = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            int radius = Theme.Px(14);
            Theme.FillRoundedGradient(g, card, radius, Theme.HeroCard, Theme.HeroCardEnd);
            Theme.DrawTopSheen(g, card, radius, Theme.Px(14));
            Theme.DrawRoundedBorder(g, card, radius, Theme.HeroBorder);

            Color accent = Current == null ? Theme.Gray : Theme.StateColor(Current.State);

            // 状态图标块：颜色随当前状态变化
            Rectangle iconBox = new Rectangle(Theme.Px(18), Theme.Px(24), Theme.Px(42), Theme.Px(42));
            Theme.FillRounded(g, iconBox, Theme.Px(13), Color.FromArgb(30, accent));
            Theme.DrawRoundedBorder(g, iconBox, Theme.Px(13), Color.FromArgb(92, accent));
            Theme.DrawPrinterGlyph(g, iconBox.X + Theme.Px(9), iconBox.Y + Theme.Px(9), Theme.Px(24), accent, 1.8f);

            int left = Theme.Px(74);
            int right = Width - Theme.Px(18);
            int textWidth = Math.Max(Theme.Px(20), right - left);
            int subLeft = Theme.Px(18);
            int subWidth = Math.Max(Theme.Px(20), Width - Theme.Px(36));

            // 标题行：当前活跃打印机 + 最后刷新时间
            Rectangle labelRect = new Rectangle(left, Theme.Px(13), Math.Max(Theme.Px(20), textWidth - Theme.Px(90)), Theme.Px(18));
            Theme.DrawText(g, "当前活跃打印机", _fLabel, labelRect, Theme.TextMuted, TextFormatFlags.Left);
            string stamp = LastUpdate == DateTime.MinValue ? "" : "更新于 " + LastUpdate.ToString("HH:mm:ss");
            Rectangle stampRect = new Rectangle(Math.Max(0, right - Theme.Px(96)), Theme.Px(13), Theme.Px(96), Theme.Px(18));
            Theme.DrawText(g, stamp, _fLabel, stampRect, Theme.TextDim, TextFormatFlags.Right);

            string name;
            string sub;
            string warn = "";
            Color warnColor = Theme.Yellow;

            if (!HasData)
            {
                name = ErrorText.Length > 0 ? "扫描失败" : "正在扫描…";
                sub = ErrorText.Length > 0 ? ErrorText : "正在读取本机打印机列表";
            }
            else if (Current == null)
            {
                name = "未检测到打印机";
                sub = "系统里没有任何打印机，请先添加设备";
                if (TotalCount > 0) sub = "共 " + TotalCount + " 台设备，但都已离线";
            }
            else
            {
                name = Current.Name;
                string role = Current.IsDefault ? "系统默认" : "非默认";
                sub = role + "  ·  " + Current.KindText;
                if (Current.JobCount > 0) sub = sub + "  ·  队列 " + Current.JobCount + " 个任务";
                else sub = sub + "  ·  无排队任务";

                if (Current.State == PrinterState.Warning)
                {
                    warn = "默认打印机离线 —— 请检查电源与数据线，或改用下方的可用设备";
                    warnColor = Theme.Yellow;
                }
                else if (!HasDefault)
                {
                    warn = "系统未设置默认打印机，WPS 打印时会弹窗让你选设备";
                    warnColor = Theme.Yellow;
                }
                else if (Current.IsVirtual)
                {
                    warn = "只会生成文件，不会实际出纸";
                    warnColor = Theme.Info;
                }
                else if (Current.State == PrinterState.Error)
                {
                    warn = "打印机报错（缺纸 / 卡纸 / 需要人工处理）";
                    warnColor = Theme.Red;
                }
                else if (OnlineCount <= 1 && TotalCount > 1)
                {
                    warn = "当前只有这一台可用，其余设备处于离线状态";
                    warnColor = Theme.TextMuted;
                }
            }

            Theme.DrawText(g, name, _fName, new Rectangle(left, Theme.Px(31), textWidth, Theme.Px(32)),
                Theme.Text, TextFormatFlags.Left);
            Theme.DrawText(g, sub, _fSub, new Rectangle(subLeft, Theme.Px(70), subWidth, Theme.Px(18)),
                Theme.TextMuted, TextFormatFlags.Left);

            if (warn.Length > 0)
            {
                Rectangle warnRect = new Rectangle(subLeft, Theme.Px(89), subWidth, Theme.Px(27));
                Theme.DrawText(g, warn, _fSub, warnRect, warnColor, TextFormatFlags.Left);
            }
        }
    }

    /// <summary>列表分组标题。</summary>
    internal sealed class SectionHeader : Control
    {
        public string Label = "";
        public string Hint = "";

        private readonly Font _font;

        public SectionHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            _font = Theme.Section;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(Theme.Px(2), Theme.Px(6), Math.Max(Theme.Px(20), Width - Theme.Px(4)), Theme.Px(20));
            Theme.DrawText(g, Label, _font, rect, Theme.TextMuted, TextFormatFlags.Left);
            if (Hint.Length > 0)
            {
                Rectangle hintRect = new Rectangle(Theme.Px(2), Theme.Px(6), Math.Max(Theme.Px(20), Width - Theme.Px(4)), Theme.Px(20));
                Theme.DrawText(g, Hint, _font, hintRect, Theme.TextDim, TextFormatFlags.Right);
            }
        }
    }

    /// <summary>单台打印机的卡片行。</summary>
    internal sealed class PrinterCard : Control
    {
        public PrinterInfo Info;

        private bool _hover;

        private readonly Font _fName;
        private readonly Font _fSub;
        private readonly Font _fPill;

        public PrinterCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            _fName = Theme.CardName;
            _fSub = Theme.Pill;
            _fPill = Theme.Pill;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int radius = Theme.Px(12);
            // 控件底部预留 4px 用于绘制投影
            Rectangle card = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - Theme.Px(4) - 1));

            // 卡片投影：卡片下沿外侧的一片渐隐暗影，制造浮起感
            Rectangle shadow = new Rectangle(card.X + Theme.Px(3), card.Bottom - Theme.Px(2),
                Math.Max(1, card.Width - Theme.Px(6)), Math.Max(2, Theme.Px(6)));
            using (GraphicsPath shadowPath = Theme.RoundedRect(shadow, Theme.Px(3)))
            using (LinearGradientBrush shadowBrush = new LinearGradientBrush(
                new Rectangle(shadow.X, shadow.Y, shadow.Width, Math.Max(1, shadow.Height)),
                Color.FromArgb(_hover ? 135 : 100, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), LinearGradientMode.Vertical))
            {
                g.FillPath(shadowBrush, shadowPath);
            }

            // 卡片本体
            if (_hover)
                Theme.FillRoundedGradient(g, card, radius, Theme.CardHover, Theme.CardHoverEnd);
            else
                Theme.FillRoundedGradient(g, card, radius, Theme.Card, Theme.CardEnd);
            Theme.DrawTopSheen(g, card, radius, Theme.Px(13));

            // 左侧状态色竖条：上下内缩、右端圆角（左端超出卡片被裁掉，因此紧贴左边缘）
            Color stateColor = Info == null ? Theme.Gray : Theme.StateColor(Info.State);
            GraphicsState clipState = g.Save();
            try
            {
                using (GraphicsPath clip = Theme.RoundedRect(card, radius))
                {
                    g.SetClip(clip);
                    int barInset = Theme.Px(12);
                    int barRadius = Theme.Px(4);
                    Rectangle bar = new Rectangle(card.X - barRadius, card.Y + barInset,
                        Theme.Px(8) + barRadius, Math.Max(Theme.Px(8), card.Height - barInset * 2));
                    using (GraphicsPath barPath = Theme.RoundedRect(bar, barRadius))
                    using (SolidBrush barBrush = new SolidBrush(stateColor))
                        g.FillPath(barBrush, barPath);
                }
            }
            finally { g.Restore(clipState); }

            Theme.DrawRoundedBorder(g, card, radius, _hover ? Theme.BorderStrong : Theme.Border);

            if (Info == null) return;

            int left = Theme.Px(18);
            int right = Width - Theme.Px(18);
            int textRight = right;

            // ---- 第一行：状态灯 + 名称 + 默认标签
            int row1Center = Theme.Px(25);
            Theme.DrawStatusDot(g, left + Theme.Px(9), row1Center, Theme.Px(4.5), stateColor, !Info.IsUsable);

            int nameRight = right;
            if (Info.IsDefault)
            {
                int pillW = Theme.DrawPill(g, right, row1Center, "默认", _fPill,
                    Theme.Green, Color.FromArgb(97, Theme.Green), Color.FromArgb(33, Theme.Green));
                nameRight = right - pillW - Theme.Px(10);
            }

            int nameLeft = Theme.Px(40);
            Rectangle nameRect = new Rectangle(nameLeft, Theme.Px(12),
                Math.Max(Theme.Px(20), nameRight - nameLeft), Theme.Px(26));
            Theme.DrawText(g, Info.Name, _fName, nameRect, Info.IsUsable ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.Left);

            // ---- 第二行：端口 + 状态文案（驱动信息保留在 ToolTip 中）
            string stateText = Info.StateText;
            int stateW = Theme.MeasureWidth(g, stateText, _fSub);
            Rectangle stateRect = new Rectangle(textRight - stateW - Theme.Px(2), Theme.Px(43), stateW + Theme.Px(2), Theme.Px(20));
            Theme.DrawText(g, stateText, _fSub, stateRect, stateColor, TextFormatFlags.Right);

            string port = string.IsNullOrEmpty(Info.PortName) ? "未知端口" : Info.PortName;
            Rectangle portRect = new Rectangle(Theme.Px(40), Theme.Px(43),
                Math.Max(Theme.Px(20), stateRect.X - Theme.Px(40) - Theme.Px(10)), Theme.Px(20));
            Theme.DrawText(g, port, _fSub, portRect, Theme.TextMuted, TextFormatFlags.Left);
        }
    }
}
