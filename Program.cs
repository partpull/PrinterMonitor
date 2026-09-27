using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PrinterMonitor
{
    internal static class Program
    {
        /// <summary>运行期绘制的应用图标，免去额外的 .ico 文件。</summary>
        public static readonly Icon AppIcon = CreateIcon();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [STAThread]
        private static void Main(string[] args)
        {
            // 出问题时留下线索，便于排查（写入 %LOCALAPPDATA%\PrinterMonitor\）
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Log("界面线程未处理异常: " + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Log("后台线程未处理异常: " + e.ExceptionObject);
            };
            Application.ApplicationExit += delegate(object s, EventArgs e)
            {
                Log("进程正常退出 ApplicationExit");
            };

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "PrinterMonitor.SingleInstance.v1", out createdNew))
            {
                if (!createdNew)
                {
                    Log("已有实例在运行，本次启动退出");
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 按系统 DPI 缩放整个界面。系统 DPI 感知模式下，桌面 DC 返回的就是系统 DPI，
// 与窗口所在显示器一致；显示器 DPI 与系统 DPI 不同时由 Windows 做位图拉伸（会略模糊，但尺寸正确）。
                try
                {
                    using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        Theme.InitScale(g.DpiX / 96f);
                    }
                }
                catch (Exception)
                {
                    Theme.InitScale(1f);
                }

                bool startHidden = false;
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "/tray", StringComparison.OrdinalIgnoreCase)) startHidden = true;
                }

                MainForm form = new MainForm();
                form.StartHidden = startHidden;
                Application.Run(form);
            }
        }

        /// <summary>把诊断信息追加到 %LOCALAPPDATA%\PrinterMonitor\log.txt，写不进去就算了。</summary>
        public static void Log(string message)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrinterMonitor");
                System.IO.Directory.CreateDirectory(dir);
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message
                    + Environment.NewLine;
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "log.txt"), line);
            }
            catch (Exception) { }
        }

        private static Icon CreateIcon()
        {
            const int size = 32;
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(255, 33, 33, 38)))
                        g.FillEllipse(bg, 0, 0, size - 1, size - 1);
                    using (SolidBrush ring = new SolidBrush(Color.FromArgb(255, 70, 70, 80)))
                    using (Pen ringPen = new Pen(ring, 1f))
                        g.DrawEllipse(ringPen, 0.5f, 0.5f, size - 2f, size - 2f);
                    using (SolidBrush fg = new SolidBrush(Color.FromArgb(255, 50, 240, 140)))
                        g.FillEllipse(fg, 9f, 9f, 14f, 14f);
                }
                IntPtr handle = bmp.GetHicon();
                Icon icon = (Icon)Icon.FromHandle(handle).Clone();
                DestroyIcon(handle);
                return icon;
            }
        }
    }

    /// <summary>Windows「自动管理默认打印机」策略。开启时默认打印机会随最近使用记录自动切换。</summary>
    internal static class DefaultPrinterPolicy
    {
        private const string KeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\Windows";
        private const string ValueName = "LegacyDefaultPrinterMode";

        /// <summary>true 表示 Windows 会按最近使用记录自动切换默认打印机。</summary>
        public static bool IsAutoManaged()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, false))
                {
                    if (key == null) return true;          // 值缺失等价于开启
                    object value = key.GetValue(ValueName);
                    if (value == null) return true;
                    return Convert.ToInt32(value) == 0;    // 0 = 自动管理，1 = 固定为用户所选
                }
            }
            catch (Exception) { return false; }
        }

        /// <summary>关闭自动管理，让默认打印机固定为用户所选（写 HKCU，不需要管理员权限）。</summary>
        public static bool DisableAutoManage()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, true))
                {
                    if (key == null) return false;
                    key.SetValue(ValueName, 1, RegistryValueKind.DWord);
                    return true;
                }
            }
            catch (Exception) { return false; }
        }
    }

    /// <summary>开机自启：写当前用户的 Run 项，不需要管理员权限。</summary>
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "PrinterMonitor";

        public static string ExePath()
        {
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetEntryAssembly();
                if (asm != null && asm.Location.Length > 0) return asm.Location;
            }
            catch (Exception) { }
            return Application.ExecutablePath;
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    object value = key.GetValue(ValueName);
                    if (value == null) return false;
                    string text = Convert.ToString(value);
                    if (text.Length == 0) return false;
                    // 指向的不是当前这份 exe，就认为未启用
                    return text.IndexOf(ExePath(), StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (Exception) { return false; }
        }

        public static bool Set(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return false;
                    if (enabled) key.SetValue(ValueName, "\"" + ExePath() + "\" /tray");
                    else key.DeleteValue(ValueName, false);
                    return true;
                }
            }
            catch (Exception) { return false; }
        }
    }
}
