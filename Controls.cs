using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PrinterMonitor
{
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
            Theme.FillRounded(g, card, Theme.Px(12), Theme.HeroCard);
            Theme.DrawRoundedBorder(g, card, Theme.Px(12), Theme.Border);

            Color accent = Current == null ? Theme.Gray : Theme.StateColor(Current.State);
            Rectangle bar = new Rectangle(Theme.Px(14), Theme.Px(18), Theme.Px(4), Math.Max(1, Height - Theme.Px(36)));
            Theme.FillRounded(g, bar, Theme.Px(2), accent);

            int left = Theme.Px(28);
            int textWidth = Math.Max(Theme.Px(20), Width - left - Theme.Px(20));

            // 标题行：当前活跃打印机 + 最后刷新时间
            Rectangle labelRect = new Rectangle(left, Theme.Px(15), Math.Max(Theme.Px(20), textWidth - Theme.Px(80)), Theme.Px(18));
            Theme.DrawText(g, "当前活跃打印机", _fLabel, labelRect, Theme.TextMuted, TextFormatFlags.Left);
            string stamp = LastUpdate == DateTime.MinValue ? "" : LastUpdate.ToString("HH:mm:ss");
            Rectangle stampRect = new Rectangle(left + textWidth - Theme.Px(70), Theme.Px(15), Theme.Px(70), Theme.Px(18));
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
                    warn = "虚拟打印机，只会生成文件，不会实际出纸";
                    warnColor = Theme.Yellow;
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

            Theme.DrawText(g, name, _fName, new Rectangle(left, Theme.Px(34), textWidth, Theme.Px(32)),
                Theme.Text, TextFormatFlags.Left);
            Theme.DrawText(g, sub, _fSub, new Rectangle(left, Theme.Px(70), textWidth, Theme.Px(18)),
                Theme.TextMuted, TextFormatFlags.Left);
            if (warn.Length > 0)
            {
                Theme.DrawText(g, warn, _fSub, new Rectangle(left, Theme.Px(92), textWidth, Theme.Px(18)),
                    warnColor, TextFormatFlags.Left);
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
        private bool _selected;

        private readonly Font _fName;
        private readonly Font _fSub;
        private readonly Font _fPill;

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
            }
        }

        public PrinterCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
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

            Rectangle card = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            Color fill = _hover ? Theme.CardHover : Theme.Card;
            Theme.FillRounded(g, card, Theme.Px(10), fill);
            Color border = _selected ? Color.FromArgb(190, 190, 200) : Theme.Border;
            Theme.DrawRoundedBorder(g, card, Theme.Px(10), border);

            if (Info == null) return;

            Color stateColor = Theme.StateColor(Info.State);
            int left = Theme.Px(18);
            int right = Width - Theme.Px(18);
            int textRight = right;

            // ---- 第一行：状态灯 + 名称 + 默认标签
            int row1Center = Theme.Px(25);
            Theme.DrawDot(g, left + Theme.Px(5), row1Center, Theme.Px(5), stateColor, !Info.IsUsable);

            int nameRight = right;
            if (Info.IsDefault)
            {
                int pillW = Theme.DrawPill(g, right, row1Center, "默认", _fPill,
                    Theme.Green, Color.FromArgb(90, Theme.Green), Color.FromArgb(22, Theme.Green));
                nameRight = right - pillW - Theme.Px(10);
            }

            int nameLeft = left + Theme.Px(18);
            Rectangle nameRect = new Rectangle(nameLeft, Theme.Px(12),
                Math.Max(Theme.Px(20), nameRight - nameLeft), Theme.Px(26));
            Theme.DrawText(g, Info.Name, _fName, nameRect, Info.IsUsable ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.Left);

            // ---- 第二行：连接方式 + 细节 + 状态文案
            int row2Center = Theme.Px(53);
            int detailLeft = left + Theme.Px(18);

            if (Info.IsVirtual)
            {
                int pillW = Theme.DrawPillAt(g, detailLeft, row2Center, "虚拟打印机", _fPill,
                    Theme.Yellow, Color.FromArgb(90, Theme.Yellow), Color.FromArgb(20, Theme.Yellow));
                detailLeft = detailLeft + pillW + Theme.Px(10);
            }
            else
            {
                string kind = Info.KindText;
                int kindW = Theme.MeasureWidth(g, kind, _fSub);
                Rectangle kindRect = new Rectangle(detailLeft, Theme.Px(43), kindW + Theme.Px(2), Theme.Px(20));
                Theme.DrawText(g, kind, _fSub, kindRect, Theme.TextMuted, TextFormatFlags.Left);
                detailLeft = detailLeft + kindW + Theme.Px(10);
            }

            string stateText = Info.StateText;
            int stateW = Theme.MeasureWidth(g, stateText, _fSub);
            Rectangle stateRect = new Rectangle(textRight - stateW - Theme.Px(2), Theme.Px(43), stateW + Theme.Px(2), Theme.Px(20));
            Theme.DrawText(g, stateText, _fSub, stateRect, stateColor, TextFormatFlags.Right);

            Rectangle detailRect = new Rectangle(detailLeft, Theme.Px(43),
                Math.Max(Theme.Px(20), stateRect.X - detailLeft - Theme.Px(10)), Theme.Px(20));
            Theme.DrawText(g, Info.DetailText, _fSub, detailRect, Theme.TextDim, TextFormatFlags.Left);
        }
    }
}
