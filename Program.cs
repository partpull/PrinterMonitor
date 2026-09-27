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
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "PrinterMonitor.SingleInstance.v1", out createdNew))
            {
                if (!createdNew) return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 按系统 DPI 缩放整个界面（配合 app.manifest 的 dpiAware 声明）
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
                        g.DrawEllipse(new Pen(ring, 1f), 0.5f, 0.5f, size - 2f, size - 2f);
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
