using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;

namespace PrinterMonitor
{
    /// <summary>打印机可用状态（决定状态灯颜色与措辞）。</summary>
    public enum PrinterState
    {
        Active = 0,     // 系统默认 且 在线 —— 绿色，这就是 WPS 会用的那台
        Warning = 1,    // 系统默认 但 离线 —— 黄色，重点提醒
        Available = 2,  // 非默认 但 在线 —— 蓝色
        Error = 3,      // 报错/缺纸/卡纸等
        Offline = 4     // 离线或不可用 —— 灰色
    }

    /// <summary>单台打印机的一次扫描结果。</summary>
    public class PrinterInfo
    {
        public string Name = "";
        public bool IsDefault;
        public bool WorkOffline;
        public int PrinterStatus = -1;
        public int DetectedErrorState = -1;
        public string PortName = "";
        public string DriverName = "";
        public string ServerName = "";
        public bool Network;
        public int JobCount;
        public bool IsVirtual;
        public PrinterState State = PrinterState.Offline;

        public bool IsUsable
        {
            get { return State == PrinterState.Active || State == PrinterState.Available; }
        }

        /// <summary>连接方式描述。</summary>
        public string KindText
        {
            get
            {
                if (IsVirtual) return "虚拟打印机";
                string port = PortName == null ? "" : PortName.ToUpperInvariant();
                if (port.StartsWith("USB") || port.StartsWith("DOT4")) return "USB 直连";
                if (port.StartsWith("IP_") || port.StartsWith("WSD-") || port.StartsWith("TCP")) return "网络打印机";
                if (port.StartsWith("COM") || port.StartsWith("LPT")) return "本地端口";
                if (Network) return "网络打印机";
                return "本地打印机";
            }
        }

        /// <summary>第二行左侧的连接细节。</summary>
        public string DetailText
        {
            get
            {
                string port = string.IsNullOrEmpty(PortName) ? "未知端口" : PortName;
                string driver = DriverName == null ? "" : DriverName.Trim();
                if (driver.Length == 0) return port;
                return port + "  ·  " + driver;
            }
        }

        /// <summary>状态文案，有打印任务时优先显示任务数。</summary>
        public string StateText
        {
            get
            {
                if (JobCount > 0)
                {
                    if (PrinterStatus == 4) return "打印中 " + JobCount;
                    return "排队 " + JobCount;
                }
                switch (State)
                {
                    case PrinterState.Active: return "活跃";
                    case PrinterState.Warning: return "默认已离线";
                    case PrinterState.Available: return "可用";
                    case PrinterState.Error: return "异常";
                    default: return "离线";
                }
            }
        }

        /// <summary>虚拟打印机的额外提示，实体打印机返回空串。</summary>
        public string NoticeText
        {
            get
            {
                if (!IsVirtual) return "";
                return "这是虚拟打印机，只会生成文件，不会实际出纸";
            }
        }
    }

    public static class PrinterService
    {
        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDefaultPrinter(string pszPrinter);

        /// <summary>把指定打印机设为 Windows 默认打印机（WPS 等软件随即可见）。</summary>
        public static bool SetDefault(string printerName)
        {
            if (string.IsNullOrEmpty(printerName)) return false;
            try { return SetDefaultPrinter(printerName); }
            catch (Exception) { return false; }
        }

        /// <summary>扫描全部已安装打印机并判定状态。调用方应在后台线程执行。</summary>
        public static List<PrinterInfo> Scan()
        {
            List<PrinterInfo> list = new List<PrinterInfo>();

            // 1) 打印机主列表
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Printer"))
            using (ManagementObjectCollection rows = searcher.Get())
            {
                foreach (ManagementBaseObject row in rows)
                {
                    PrinterInfo p = new PrinterInfo();
                    p.Name = GetString(row, "Name");
                    if (p.Name.Length == 0) continue;
                    p.IsDefault = GetBool(row, "Default");
                    p.WorkOffline = GetBool(row, "WorkOffline");
                    p.PrinterStatus = GetInt(row, "PrinterStatus", -1);
                    p.DetectedErrorState = GetInt(row, "DetectedErrorState", -1);
                    p.PortName = GetString(row, "PortName");
                    p.DriverName = GetString(row, "DriverName");
                    p.ServerName = GetString(row, "ServerName");
                    p.Network = GetBool(row, "Network");
                    list.Add(p);
                }
            }

            // 2) 打印队列任务数
            Dictionary<string, int> jobs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PrintJob"))
            using (ManagementObjectCollection rows = searcher.Get())
            {
                foreach (ManagementBaseObject row in rows)
                {
                    // Win32_PrintJob.Name 形如 "打印机名,任务号"，按最后一个逗号切分
                    string full = GetString(row, "Name");
                    if (full.Length == 0) continue;
                    int cut = full.LastIndexOf(',');
                    string owner = cut > 0 ? full.Substring(0, cut) : full;
                    if (jobs.ContainsKey(owner)) jobs[owner] = jobs[owner] + 1;
                    else jobs[owner] = 1;
                }
            }

            // 3) 逐台判定
            for (int i = 0; i < list.Count; i++)
            {
                PrinterInfo p = list[i];
                int n;
                if (jobs.TryGetValue(p.Name, out n)) p.JobCount = n;
                p.IsVirtual = IsVirtualPrinter(p);
                p.State = Resolve(p);
            }

            list.Sort(Compare);
            return list;
        }

        /// <summary>判定虚拟打印机：只能生成文件、不会实际出纸的那一类。</summary>
        private static bool IsVirtualPrinter(PrinterInfo p)
        {
            string port = (p.PortName == null ? "" : p.PortName.Trim()).ToUpperInvariant();
            string driver = (p.DriverName == null ? "" : p.DriverName.Trim()).ToUpperInvariant();
            string name = (p.Name == null ? "" : p.Name.Trim()).ToUpperInvariant();

            // 端口是最可靠的信号
            if (port == "PORTPROMPT:" || port == "FILE:" || port == "NUL:") return true;
            if (port.StartsWith("XPSPORT:")) return true;
            if (port.StartsWith("SHRFAX:")) return true;
            if (port.Contains("VIRTUAL")) return true;
            if (port.StartsWith("RPCPORT:")) return true;

            // 驱动名
            if (driver.Contains("VIRTUAL")) return true;
            if (driver.Contains("PRINT TO PDF")) return true;
            if (driver.Contains("MICROSOFT XPS")) return true;
            if (driver.Contains("SHARED FAX")) return true;

            // 名称（仅针对系统内置的几款，避免误伤真实设备）
            if (name == "MICROSOFT PRINT TO PDF") return true;
            if (name == "MICROSOFT XPS DOCUMENT WRITER") return true;
            if (name == "FAX") return true;
            if (name.Contains("虚拟")) return true;

            return false;
        }

        /// <summary>综合系统默认标记与在线状态，得出可用状态。</summary>
        private static PrinterState Resolve(PrinterInfo p)
        {
            // 报错状态：缺纸 3/4、缺墨 5/6、开门 7、卡纸 8、需要服务 10
            if (p.DetectedErrorState >= 3 && p.DetectedErrorState <= 8) return PrinterState.Error;
            if (p.PrinterStatus == 5 || p.PrinterStatus == 6) return PrinterState.Error;

            // 离线判定
            bool offline = p.WorkOffline;
            if (p.PrinterStatus == 7) offline = true;       // 7 = Offline
            if (p.DetectedErrorState == 9) offline = true;  // 9 = Offline
            if (offline) return p.IsDefault ? PrinterState.Warning : PrinterState.Offline;

            return p.IsDefault ? PrinterState.Active : PrinterState.Available;
        }

        /// <summary>排序：实体设备优先 → 可用优先 → 默认优先 → 名称。</summary>
        private static int Compare(PrinterInfo a, PrinterInfo b)
        {
            int c = a.IsVirtual.CompareTo(b.IsVirtual);
            if (c != 0) return c;
            c = ((int)a.State).CompareTo((int)b.State);
            if (c != 0) return c;
            c = b.IsDefault.CompareTo(a.IsDefault);
            if (c != 0) return c;
            c = a.JobCount.CompareTo(b.JobCount);
            if (c != 0) return c;
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        private static string GetString(ManagementBaseObject row, string key)
        {
            try
            {
                object v = row[key];
                return v == null ? "" : Convert.ToString(v);
            }
            catch (Exception) { return ""; }
        }

        private static bool GetBool(ManagementBaseObject row, string key)
        {
            try
            {
                object v = row[key];
                if (v == null) return false;
                return Convert.ToBoolean(v);
            }
            catch (Exception) { return false; }
        }

        private static int GetInt(ManagementBaseObject row, string key, int fallback)
        {
            try
            {
                object v = row[key];
                if (v == null) return fallback;
                return Convert.ToInt32(v);
            }
            catch (Exception) { return fallback; }
        }
    }
}
