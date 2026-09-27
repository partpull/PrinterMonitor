using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PrinterMonitor
{
    /// <summary>开启双缓冲的滚动容器，避免刷新时闪烁。</summary>
    internal sealed class BufferedFlowPanel : FlowLayoutPanel
    {
        public BufferedFlowPanel()
        {
            DoubleBuffered = true;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly HeroPanel _hero;
        private readonly BufferedFlowPanel _list;
        private readonly Panel _bottom;
        private readonly Button _btnSetDefault;
        private readonly Button _btnRefresh;
        private readonly CheckBox _chkAutoStart;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _trayMenu;
        private readonly ToolStripMenuItem _trayAutoItem;
        private readonly ContextMenuStrip _cardMenu;
        private readonly ToolStripMenuItem _cardMenuItem;

        private List<PrinterInfo> _printers = new List<PrinterInfo>();
        private readonly List<PrinterCard> _cards = new List<PrinterCard>();
        private readonly List<SectionHeader> _headers = new List<SectionHeader>();
        private string _listSignature = "";
        private string _selectedName = "";
        private bool _scanning;
        private bool _inRelayout;
        private bool _relayoutPending;
        private bool _reallyExit;
        private bool _trayHintShown;

        /// <summary>由 /tray 参数置位：启动后直接缩到托盘，不弹窗口。</summary>
        public bool StartHidden;

        public MainForm()
        {
            Text = "打印机状态助手";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.FontAt(12, FontStyle.Regular);
            ClientSize = new Size(Theme.Px(430), Theme.Px(560));
            MinimumSize = new Size(Theme.Px(380), Theme.Px(380));
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            Icon = Program.AppIcon;
            KeyPreview = true;

            _hero = new HeroPanel();
            _list = new BufferedFlowPanel();
            _list.FlowDirection = FlowDirection.TopDown;
            _list.WrapContents = false;
            _list.AutoScroll = true;
            _list.BackColor = Theme.Bg;
            _list.ClientSizeChanged += delegate(object s, EventArgs e) { RelayoutList(); };

            _bottom = new Panel();
            _bottom.BackColor = Theme.Bg;

            _btnSetDefault = MakeButton("设为默认");
            _btnSetDefault.Click += delegate(object s, EventArgs e) { ApplySelectedAsDefault(); };

            _btnRefresh = MakeButton("刷新");
            _btnRefresh.Click += delegate(object s, EventArgs e) { BeginScan(); };

            _chkAutoStart = new CheckBox();
            _chkAutoStart.Text = "开机自启";
            _chkAutoStart.FlatStyle = FlatStyle.Flat;
            _chkAutoStart.BackColor = Theme.Bg;
            _chkAutoStart.ForeColor = Theme.TextMuted;
            _chkAutoStart.Font = Theme.FontAt(12, FontStyle.Regular);
            _chkAutoStart.Cursor = Cursors.Hand;
            _chkAutoStart.AutoSize = false;
            _chkAutoStart.Checked = AutoStart.IsEnabled();
            _chkAutoStart.CheckedChanged += delegate(object s, EventArgs e)
            {
                if (AutoStart.Set(_chkAutoStart.Checked))
                {
                    if (_trayAutoItem.Checked != _chkAutoStart.Checked) _trayAutoItem.Checked = _chkAutoStart.Checked;
                }
                else
                {
                    MessageBox.Show(this, "写入开机启动项失败，请检查系统权限。", "打印机状态助手",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _chkAutoStart.Checked = AutoStart.IsEnabled();
                }
            };

            _bottom.Controls.Add(_btnSetDefault);
            _bottom.Controls.Add(_btnRefresh);
            _bottom.Controls.Add(_chkAutoStart);

            Controls.Add(_hero);
            Controls.Add(_list);
            Controls.Add(_bottom);

            // ---- 托盘与右键菜单
            _cardMenuItem = new ToolStripMenuItem("设为默认打印机");
            _cardMenuItem.Click += delegate(object s, EventArgs e)
            {
                ApplySelectedAsDefault();
            };
            _cardMenu = new ContextMenuStrip();
            _cardMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuTable());
            _cardMenu.ShowImageMargin = false;
            _cardMenu.Items.Add(_cardMenuItem);
            _cardMenu.Opening += delegate(object s, System.ComponentModel.CancelEventArgs e)
            {
                PrinterCard card = _cardMenu.SourceControl as PrinterCard;
                if (card == null || card.Info == null) { e.Cancel = true; return; }
                SelectPrinter(card.Info.Name);
                _cardMenuItem.Text = "将「" + Shorten(card.Info.Name, 18) + "」设为默认打印机";
                _cardMenuItem.Enabled = !card.Info.IsDefault;
            };

            ToolStripMenuItem showItem = new ToolStripMenuItem("显示主窗口");
            showItem.Font = Theme.FontAt(12, FontStyle.Bold);
            showItem.Click += delegate(object s, EventArgs e) { RestoreWindow(); };

            ToolStripMenuItem refreshItem = new ToolStripMenuItem("立即刷新");
            refreshItem.Click += delegate(object s, EventArgs e) { BeginScan(); };

            _trayAutoItem = new ToolStripMenuItem("开机自启");
            _trayAutoItem.CheckOnClick = true;
            _trayAutoItem.Checked = _chkAutoStart.Checked;
            _trayAutoItem.CheckedChanged += delegate(object s, EventArgs e)
            {
                if (_chkAutoStart.Checked != _trayAutoItem.Checked) _chkAutoStart.Checked = _trayAutoItem.Checked;
            };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate(object s, EventArgs e)
            {
                _reallyExit = true;
                Close();
            };

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuTable());
            _trayMenu.ShowImageMargin = false;
            _trayMenu.Items.Add(showItem);
            _trayMenu.Items.Add(refreshItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_trayAutoItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(exitItem);

            _tray = new NotifyIcon();
            _tray.Icon = Program.AppIcon;
            _tray.Text = "打印机状态助手";
            _tray.ContextMenuStrip = _trayMenu;
            _tray.Visible = true;
            _tray.DoubleClick += delegate(object s, EventArgs e) { RestoreWindow(); };

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 3000;
            _timer.Tick += delegate(object s, EventArgs e) { BeginScan(); };
            _timer.Start();

            // 窗口先显示，扫描放到后台，保证启动是瞬时的
            Shown += delegate(object s, EventArgs e)
            {
                if (StartHidden)
                {
                    StartHidden = false;
                    _trayHintShown = true;
                    ShowInTaskbar = false;
                    Hide();
                }
                BeginScan();
            };
            Resize += delegate(object s, EventArgs e) { LayoutChildren(); };
            VisibleChanged += delegate(object s, EventArgs e)
            {
                _timer.Interval = Visible ? 3000 : 10000;
            };
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.F5) { BeginScan(); e.Handled = true; }
                if (e.KeyCode == Keys.Escape) { Hide(); e.Handled = true; }
            };
        }

        private static Button MakeButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Theme.BorderStrong;
            b.FlatAppearance.MouseOverBackColor = Theme.CardHover;
            b.FlatAppearance.MouseDownBackColor = Theme.Card;
            b.BackColor = Theme.Card;
            b.ForeColor = Theme.Text;
            b.Font = Theme.FontAt(12, FontStyle.Regular);
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            return b;
        }

        private static string Shorten(string text, int max)
        {
            if (text == null) return "";
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

        // ------------------------------------------------------------------ 布局

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDarkTitleBar();
            LayoutChildren();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            CenterOnActiveScreen();
        }

        /// <summary>把窗口摆到鼠标所在屏幕的正中，并确保完整可见（避免跑到屏幕外）。</summary>
        private void CenterOnActiveScreen()
        {
            try
            {
                Screen screen = Screen.FromPoint(Cursor.Position);
                Rectangle wa = screen.WorkingArea;
                int x = wa.Left + (wa.Width - Width) / 2;
                int y = wa.Top + (wa.Height - Height) / 2;
                if (x < wa.Left) x = wa.Left;
                if (y < wa.Top) y = wa.Top;
                Location = new Point(x, y);
            }
            catch (Exception) { }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Win10/11 深色标题栏，避免深色界面配白色标题条。</summary>
        private void ApplyDarkTitleBar()
        {
            try
            {
                int on = 1;
                const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
                if (DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, 4) != 0)
                {
                    DwmSetWindowAttribute(Handle, 19, ref on, 4);   // 早期 Win10 的属性号
                }
            }
            catch (Exception) { }
        }

        private void LayoutChildren()
        {
            int pad = Theme.Px(14);
            int gap = Theme.Px(8);
            int heroH = Theme.Px(130);
            int bottomH = Theme.Px(52);

            int w = Math.Max(Theme.Px(120), ClientSize.Width - pad * 2);
            _hero.Bounds = new Rectangle(pad, pad, w, heroH);

            int bottomTop = ClientSize.Height - pad - bottomH;
            _bottom.Bounds = new Rectangle(pad, bottomTop, w, bottomH);

            int listTop = pad + heroH + gap;
            int listH = Math.Max(Theme.Px(80), bottomTop - gap - listTop);
            _list.Bounds = new Rectangle(pad, listTop, w, listH);

            // 底栏按钮
            int btnH = Theme.Px(34);
            int btnY = (bottomH - btnH) / 2;
            _btnSetDefault.Bounds = new Rectangle(0, btnY, Theme.Px(92), btnH);
            _btnRefresh.Bounds = new Rectangle(Theme.Px(102), btnY, Theme.Px(66), btnH);
            int chkW = Theme.Px(96);
            _chkAutoStart.Bounds = new Rectangle(_bottom.Width - chkW, btnY, chkW, btnH);

            RelayoutList();
        }

        private void RelayoutList()
        {
            if (_inRelayout) { _relayoutPending = true; return; }
            _inRelayout = true;
            try
            {
                // 滚动条出现会改变 ClientSize，需要收敛到稳定宽度
                do
                {
                    _relayoutPending = false;
                    int w = _list.ClientSize.Width;
                    for (int i = 0; i < _list.Controls.Count; i++)
                    {
                        Control c = _list.Controls[i];
                        int cw = Math.Max(Theme.Px(140), w - c.Margin.Horizontal - Theme.Px(2));
                        if (c.Width != cw) c.Width = cw;
                    }
                }
                while (_relayoutPending);
            }
            finally { _inRelayout = false; }
        }

        // ------------------------------------------------------------------ 扫描

        private void BeginScan()
        {
            if (_scanning) return;
            _scanning = true;
            _btnRefresh.Enabled = false;

            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                List<PrinterInfo> result = null;
                string error = null;
                try { result = PrinterService.Scan(); }
                catch (Exception ex) { error = ex.Message; }

                try
                {
                    if (IsDisposed || !IsHandleCreated) { _scanning = false; return; }
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _scanning = false;
                        _btnRefresh.Enabled = true;
                        if (error != null)
                        {
                            _hero.HasData = false;
                            _hero.ErrorText = Shorten(error, 40);
                            _hero.Invalidate();
                            UpdateTrayText();
                            return;
                        }
                        ApplyResult(result);
                    });
                }
                catch (Exception) { _scanning = false; }
            });
        }

        private void ApplyResult(List<PrinterInfo> result)
        {
            _printers = result;

            PrinterInfo active = null;
            bool hasDefault = false;
            int online = 0;
            for (int i = 0; i < _printers.Count; i++)
            {
                if (_printers[i].IsDefault)
                {
                    hasDefault = true;
                    if (active == null) active = _printers[i];
                }
                if (_printers[i].IsUsable) online++;
            }
            if (active == null)
            {
                for (int i = 0; i < _printers.Count; i++)
                {
                    if (_printers[i].IsUsable) { active = _printers[i]; break; }
                }
            }
            if (active == null && _printers.Count > 0) active = _printers[0];

            _hero.Current = active;
            _hero.HasData = true;
            _hero.HasDefault = hasDefault;
            _hero.OnlineCount = online;
            _hero.TotalCount = _printers.Count;
            _hero.LastUpdate = DateTime.Now;
            _hero.Invalidate();

            if (_selectedName.Length > 0 && !ContainsName(_selectedName)) _selectedName = "";
            UpdateList();
            UpdateButtons();
            UpdateTrayText();
        }

        private bool ContainsName(string name)
        {
            for (int i = 0; i < _printers.Count; i++)
            {
                if (string.Equals(_printers[i].Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private PrinterInfo FindByName(string name)
        {
            for (int i = 0; i < _printers.Count; i++)
            {
                if (string.Equals(_printers[i].Name, name, StringComparison.OrdinalIgnoreCase)) return _printers[i];
            }
            return null;
        }

        private void UpdateTrayText()
        {
            string text = "打印机状态助手";
            if (_hero.Current != null)
                text = _hero.Current.StateText + "：" + _hero.Current.Name;
            else if (_hero.HasData)
                text = "未检测到打印机";
            text = Shorten(text, 60);
            try { _tray.Text = text; }
            catch (Exception) { _tray.Text = "打印机状态助手"; }
        }

        // ------------------------------------------------------------------ 列表

        private string BuildSignature()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _printers.Count; i++)
            {
                sb.Append(_printers[i].Name).Append('|').Append(_printers[i].IsVirtual ? 'V' : 'P').Append('\n');
            }
            return sb.ToString();
        }

        private void UpdateList()
        {
            string signature = BuildSignature();
            if (signature == _listSignature && _cards.Count > 0)
            {
                // 结构没变，就地更新，避免重建造成闪动
                List<PrinterInfo> physical = new List<PrinterInfo>();
                List<PrinterInfo> virtuals = new List<PrinterInfo>();
                Split(_printers, physical, virtuals);
                int index = 0;
                for (int i = 0; i < _cards.Count; i++)
                {
                    _cards[i].Info = _printers[index];
                    _cards[i].Selected = string.Equals(_printers[index].Name, _selectedName, StringComparison.OrdinalIgnoreCase);
                    _cards[i].Invalidate();
                    index++;
                }
                UpdateHeaderHints(physical, virtuals);
                return;
            }

            _listSignature = signature;
            RebuildList();
        }

        private static void Split(List<PrinterInfo> all, List<PrinterInfo> physical, List<PrinterInfo> virtuals)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsVirtual) virtuals.Add(all[i]);
                else physical.Add(all[i]);
            }
        }

        private void UpdateHeaderHints(List<PrinterInfo> physical, List<PrinterInfo> virtuals)
        {
            int h = 0;
            if (physical.Count > 0 && h < _headers.Count)
            {
                _headers[h].Hint = physical.Count + " 台 · " + CountUsable(physical) + " 可用";
                _headers[h].Invalidate();
                h++;
            }
            if (virtuals.Count > 0 && h < _headers.Count)
            {
                _headers[h].Hint = virtuals.Count + " 台 · 不实际出纸";
                _headers[h].Invalidate();
            }
        }

        private static int CountUsable(List<PrinterInfo> list)
        {
            int n = 0;
            for (int i = 0; i < list.Count; i++) if (list[i].IsUsable) n++;
            return n;
        }

        private void RebuildList()
        {
            _list.SuspendLayout();
            try
            {
                while (_list.Controls.Count > 0)
                {
                    Control c = _list.Controls[0];
                    _list.Controls.RemoveAt(0);
                    c.Dispose();
                }
                _cards.Clear();
                _headers.Clear();

                List<PrinterInfo> physical = new List<PrinterInfo>();
                List<PrinterInfo> virtuals = new List<PrinterInfo>();
                Split(_printers, physical, virtuals);

                if (physical.Count > 0)
                {
                    AddHeader("物理打印机", physical.Count + " 台 · " + CountUsable(physical) + " 可用");
                    AddCards(physical);
                }
                if (virtuals.Count > 0)
                {
                    AddHeader("虚拟打印机", virtuals.Count + " 台 · 不实际出纸");
                    AddCards(virtuals);
                }
            }
            finally
            {
                _list.ResumeLayout();
            }
            RelayoutList();
        }

        private void AddHeader(string label, string hint)
        {
            SectionHeader header = new SectionHeader();
            header.Label = label;
            header.Hint = hint;
            header.Height = Theme.Px(32);
            header.Margin = new Padding(0, _headers.Count == 0 ? 0 : Theme.Px(10), 0, 0);
            header.Width = Math.Max(Theme.Px(140), _list.ClientSize.Width);
            _list.Controls.Add(header);
            _headers.Add(header);
        }

        private void AddCards(List<PrinterInfo> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                PrinterCard card = new PrinterCard();
                card.Info = items[i];
                card.Height = Theme.Px(76);
                card.Margin = new Padding(0, Theme.Px(6), 0, 0);
                card.Width = Math.Max(Theme.Px(140), _list.ClientSize.Width);
                card.Selected = string.Equals(items[i].Name, _selectedName, StringComparison.OrdinalIgnoreCase);
                card.ContextMenuStrip = _cardMenu;
                card.Click += delegate(object s, EventArgs e)
                {
                    PrinterCard source = s as PrinterCard;
                    if (source != null && source.Info != null) SelectPrinter(source.Info.Name);
                };
                card.DoubleClick += delegate(object s, EventArgs e)
                {
                    PrinterCard source = s as PrinterCard;
                    if (source == null || source.Info == null) return;
                    if (source.Info.IsDefault)
                    {
                        BeginScan();
                        return;
                    }
                    SelectPrinter(source.Info.Name);
                    ApplySelectedAsDefault();
                };
                _list.Controls.Add(card);
                _cards.Add(card);
            }
        }

        // ------------------------------------------------------------------ 交互

        private void SelectPrinter(string name)
        {
            if (string.Equals(_selectedName, name, StringComparison.OrdinalIgnoreCase)) return;
            _selectedName = name;
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Selected = _cards[i].Info != null &&
                    string.Equals(_cards[i].Info.Name, _selectedName, StringComparison.OrdinalIgnoreCase);
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            PrinterInfo p = _selectedName.Length > 0 ? FindByName(_selectedName) : null;
            bool canSet = p != null && !p.IsDefault;
            _btnSetDefault.Enabled = canSet;
            _btnSetDefault.ForeColor = canSet ? Theme.Text : Theme.TextDim;
            _btnSetDefault.Text = p == null ? "设为默认" : (p.IsDefault ? "已是默认" : "设为默认");
        }

        private void ApplySelectedAsDefault()
        {
            PrinterInfo p = _selectedName.Length > 0 ? FindByName(_selectedName) : null;
            if (p == null)
            {
                MessageBox.Show(this, "请先在下面点选一台打印机。", "打印机状态助手",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (p.IsDefault)
            {
                BeginScan();
                return;
            }
            if (PrinterService.SetDefault(p.Name))
            {
                _tray.Text = "已切换到：" + Shorten(p.Name, 40);
                BeginScan();
            }
            else
            {
                MessageBox.Show(this, "设置默认打印机失败，可能需要在 Windows 设置中确认。", "打印机状态助手",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RestoreWindow()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            BeginScan();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!_trayHintShown)
                {
                    _trayHintShown = true;
                    _tray.Visible = true;
                    _tray.ShowBalloonTip(3000, "打印机状态助手还在后台运行",
                        "双击托盘图标可以随时打开窗口。", ToolTipIcon.Info);
                }
                return;
            }
            _timer.Stop();
            _tray.Visible = false;
            _tray.Dispose();
            base.OnFormClosing(e);
        }
    }

    /// <summary>托盘 / 右键菜单的深色配色。</summary>
    internal sealed class DarkMenuTable : ProfessionalColorTable
    {
        private static readonly Color MenuBg = Color.FromArgb(40, 40, 46);
        private static readonly Color MenuHover = Color.FromArgb(58, 58, 66);
        private static readonly Color MenuLine = Color.FromArgb(62, 62, 70);

        public override Color ToolStripDropDownBackground { get { return MenuBg; } }
        public override Color ImageMarginGradientBegin { get { return MenuBg; } }
        public override Color ImageMarginGradientMiddle { get { return MenuBg; } }
        public override Color ImageMarginGradientEnd { get { return MenuBg; } }
        public override Color MenuBorder { get { return MenuLine; } }
        public override Color MenuItemBorder { get { return MenuLine; } }
        public override Color MenuItemSelected { get { return MenuHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return MenuHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return MenuHover; } }
        public override Color SeparatorDark { get { return MenuLine; } }
        public override Color SeparatorLight { get { return MenuLine; } }
    }
}
