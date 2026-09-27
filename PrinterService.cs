using System;
using System.Collections.Generic;
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
        public string PortName = "";
        public string DriverName = "";
        public string ServerName = "";
        public bool Network;
        public int JobCount;
        public bool IsVirtual;
        public uint StatusBits;
        public PrinterState State = PrinterState.Offline;

        public bool IsUsable
        {
            get { return State == PrinterState.Active || State == PrinterState.Available; }
        }

        /// <summary>Windows 侧标记为脱机，或驱动报告离线/不可用。</summary>
        public bool Offline
        {
            get
            {
                return (StatusBits & (Spool.PRINTER_STATUS_OFFLINE |
                                      Spool.PRINTER_STATUS_NOT_AVAILABLE |
                                      Spool.PRINTER_STATUS_SERVER_UNKNOWN)) != 0;
            }
        }

        /// <summary>影响出纸的故障：缺纸、卡纸、开盖、需人工干预等。</summary>
        public bool HasError
        {
            get
            {
                return (StatusBits & (Spool.PRINTER_STATUS_ERROR |
                                      Spool.PRINTER_STATUS_PAPER_JAM |
                                      Spool.PRINTER_STATUS_PAPER_OUT |
                                      Spool.PRINTER_STATUS_MANUAL_FEED |
                                      Spool.PRINTER_STATUS_PAPER_PROBLEM |
                                      Spool.PRINTER_STATUS_OUTPUT_BIN_FULL |
                                      Spool.PRINTER_STATUS_NO_TONER |
                                      Spool.PRINTER_STATUS_USER_INTERVENTION |
                                      Spool.PRINTER_STATUS_DOOR_OPEN |
                                      Spool.PRINTER_STATUS_OUT_OF_MEMORY)) != 0;
            }
        }

        /// <summary>正在出纸。注意很多驱动根本不报这个位。</summary>
        public bool Printing
        {
            get
            {
                return (StatusBits & (Spool.PRINTER_STATUS_PRINTING |
                                      Spool.PRINTER_STATUS_PROCESSING)) != 0;
            }
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

        /// <summary>列表里名称过长会截断，悬浮提示给出完整信息。</summary>
        public string TooltipText
        {
            get
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append(Name).Append('\n');
                sb.Append(IsDefault ? "系统默认" : "非默认").Append(" · ").Append(KindText).Append('\n');
                sb.Append("端口：").Append(string.IsNullOrEmpty(PortName) ? "未知" : PortName).Append('\n');
                sb.Append("驱动：").Append(string.IsNullOrEmpty(DriverName) ? "未知" : DriverName.Trim()).Append('\n');
                sb.Append("状态：").Append(StateText);
                if (IsVirtual) sb.Append('\n').Append("虚拟打印机，不会实际出纸");
                return sb.ToString();
            }
        }

        /// <summary>状态文案，有打印任务时优先显示任务数。</summary>
        public string StateText
        {
            get
            {
                if (JobCount > 0)
                {
                    if (Printing) return "打印中 " + JobCount;
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
    }

    /// <summary>立刻可读的 spooler 状态位（winspool.h）。</summary>
    internal static class Spool
    {
        public const uint PRINTER_STATUS_PAUSED = 0x00000001;
        public const uint PRINTER_STATUS_ERROR = 0x00000002;
        public const uint PRINTER_STATUS_PAPER_JAM = 0x00000008;
        public const uint PRINTER_STATUS_PAPER_OUT = 0x00000010;
        public const uint PRINTER_STATUS_MANUAL_FEED = 0x00000020;
        public const uint PRINTER_STATUS_PAPER_PROBLEM = 0x00000040;
        public const uint PRINTER_STATUS_OFFLINE = 0x00000080;
        public const uint PRINTER_STATUS_BUSY = 0x00000200;
        public const uint PRINTER_STATUS_PRINTING = 0x00000400;
        public const uint PRINTER_STATUS_OUTPUT_BIN_FULL = 0x00000800;
        public const uint PRINTER_STATUS_NOT_AVAILABLE = 0x00001000;
        public const uint PRINTER_STATUS_PROCESSING = 0x00004000;
        public const uint PRINTER_STATUS_NO_TONER = 0x00040000;
        public const uint PRINTER_STATUS_USER_INTERVENTION = 0x00100000;
        public const uint PRINTER_STATUS_OUT_OF_MEMORY = 0x00200000;
        public const uint PRINTER_STATUS_DOOR_OPEN = 0x00400000;
        public const uint PRINTER_STATUS_SERVER_UNKNOWN = 0x00800000;

        public const uint PRINTER_ATTRIBUTE_NETWORK = 0x00000010;
        public const uint PRINTER_ATTRIBUTE_LOCAL = 0x00000040;
        public const uint PRINTER_ATTRIBUTE_WORK_OFFLINE = 0x00000400;

        public const int PRINTER_ENUM_LOCAL = 0x00000002;
        public const int PRINTER_ENUM_CONNECTIONS = 0x00000004;
    }

    /// <summary>
    /// 打印机扫描。走 spooler API（EnumPrinters / GetDefaultPrinter / EnumJobs），
    /// 不依赖 WMI —— 实测比 WMI 快约两个数量级，且不受 WMI 服务被管控影响。
    /// </summary>
    public static class PrinterService
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PRINTER_INFO_2
        {
            public string pServerName;
            public string pPrinterName;
            public string pShareName;
            public string pPortName;
            public string pDriverName;
            public string pComment;
            public string pLocation;
            public IntPtr pDevMode;
            public string pSepFile;
            public string pPrintProcessor;
            public string pDatatype;
            public string pParameters;
            public IntPtr pSecurityDescriptor;
            public uint Attributes;
            public uint Priority;
            public uint DefaultPriority;
            public uint StartTime;
            public uint UntilTime;
            public uint Status;
            public uint cJobs;
            public uint AveragePPM;
        }

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool EnumPrinters(int Flags, string Name, int Level, IntPtr pPrinterEnum,
                                                int cbBuf, out int pcbNeeded, out int pcReturned);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetDefaultPrinter(System.Text.StringBuilder pszBuffer, ref int pcchBuffer);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EnumJobs(IntPtr hPrinter, int FirstJob, int NoJobs, int Level,
                                            IntPtr pJob, int cbBuf, out int pcbNeeded, out int pcReturned);

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
            List<PrinterInfo> list = Enumerate();
            string defaultName = QueryDefaultPrinter(list);

            for (int i = 0; i < list.Count; i++)
            {
                PrinterInfo p = list[i];
                p.IsDefault = string.Equals(p.Name, defaultName, StringComparison.OrdinalIgnoreCase);
                p.JobCount = QueryJobCount(p.Name);
                p.IsVirtual = IsVirtualPrinter(p);
                p.State = Resolve(p);
            }

            list.Sort(Compare);
            return list;
        }

        /// <summary>枚举本机与已连接网络打印机。</summary>
        private static List<PrinterInfo> Enumerate()
        {
            List<PrinterInfo> list = new List<PrinterInfo>();
            int flags = Spool.PRINTER_ENUM_LOCAL | Spool.PRINTER_ENUM_CONNECTIONS;

            int needed, returned;
            EnumPrinters(flags, null, 2, IntPtr.Zero, 0, out needed, out returned);
            if (needed == 0) return list;      // 没有安装任何打印机

            IntPtr buffer = Marshal.AllocHGlobal(needed);
            try
            {
                if (!EnumPrinters(flags, null, 2, buffer, needed, out needed, out returned))
                    throw new Exception("读取打印机列表失败（错误码 " + Marshal.GetLastWin32Error() + "）");

                int size = Marshal.SizeOf(typeof(PRINTER_INFO_2));
                for (int i = 0; i < returned; i++)
                {
                    IntPtr item = (IntPtr)((long)buffer + i * size);
                    PRINTER_INFO_2 raw = (PRINTER_INFO_2)Marshal.PtrToStructure(item, typeof(PRINTER_INFO_2));
                    if (raw.pPrinterName == null || raw.pPrinterName.Length == 0) continue;

                    PrinterInfo p = new PrinterInfo();
                    p.Name = raw.pPrinterName;
                    p.PortName = raw.pPortName == null ? "" : raw.pPortName;
                    p.DriverName = raw.pDriverName == null ? "" : raw.pDriverName;
                    p.ServerName = raw.pServerName == null ? "" : raw.pServerName;
                    p.Network = (raw.Attributes & Spool.PRINTER_ATTRIBUTE_NETWORK) != 0;
                    p.StatusBits = raw.Status;
                    // PRINTER_ATTRIBUTE_WORK_OFFLINE 是「脱机使用打印机」，等同于离线
                    if ((raw.Attributes & Spool.PRINTER_ATTRIBUTE_WORK_OFFLINE) != 0)
                        p.StatusBits |= Spool.PRINTER_STATUS_OFFLINE;
                    list.Add(p);
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }

            return list;
        }

        /// <summary>
        /// 取默认打印机。
        /// 注意：不能用 PRINTER_INFO_2.Attributes 的 DEFAULT 位 —— 实测 Windows 经常不设这一位。
        /// 优先读注册表 Device 值（实测与 API 完全同步，快 400 倍），
        /// 取不到或指向已卸载设备时再退回 spooler API。
        /// </summary>
        private static string QueryDefaultPrinter(List<PrinterInfo> installed)
        {
            string name = ReadRegistryDefault(installed);
            if (name.Length > 0) return name;
            return QueryDefaultPrinterViaApi();
        }

        /// <summary>
        /// 注册表里 Device 的格式是「打印机名,驱动名,端口名」。打印机名本身可能含逗号，
        /// 所以从最长的前缀开始试，取第一个能在已安装列表里匹配上的。
        /// </summary>
        private static string ReadRegistryDefault(List<PrinterInfo> installed)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows NT\CurrentVersion\Windows", false))
                {
                    if (key == null) return "";
                    object value = key.GetValue("Device");
                    if (value == null) return "";
                    string text = Convert.ToString(value);
                    if (text.Length == 0) return "";

                    for (int cut = text.Length - 1; cut > 0; cut--)
                    {
                        if (text[cut] != ',') continue;
                        string candidate = text.Substring(0, cut);
                        if (ContainsName(installed, candidate)) return candidate;
                    }
                    return "";
                }
            }
            catch (Exception) { return ""; }
        }

        private static bool ContainsName(List<PrinterInfo> list, string name)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>兜底路径：单次调用即可，固定缓冲避免「先探长度」多花一倍时间。</summary>
        private static string QueryDefaultPrinterViaApi()
        {
            try
            {
                const int capacity = 512;      // 打印机名长度上限远小于此
                System.Text.StringBuilder sb = new System.Text.StringBuilder(capacity);
                int length = capacity;
                if (!GetDefaultPrinter(sb, ref length)) return "";
                return sb.ToString();
            }
            catch (Exception) { return ""; }
        }

        /// <summary>取某台打印机的排队任务数。拿不到时按 0 处理，不影响其它信息展示。</summary>
        private static int QueryJobCount(string printerName)
        {
            IntPtr handle;
            if (!OpenPrinter(printerName, out handle, IntPtr.Zero)) return 0;
            try
            {
                int needed, returned;
                EnumJobs(handle, 0, 255, 1, IntPtr.Zero, 0, out needed, out returned);
                if (needed == 0) return 0;               // 队列为空
                IntPtr buffer = Marshal.AllocHGlobal(needed);
                try
                {
                    if (!EnumJobs(handle, 0, 255, 1, buffer, needed, out needed, out returned)) return 0;
                    return returned;
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            catch (Exception) { return 0; }
            finally { ClosePrinter(handle); }
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
            if (p.HasError) return PrinterState.Error;
            if (p.Offline) return p.IsDefault ? PrinterState.Warning : PrinterState.Offline;
            return p.IsDefault ? PrinterState.Active : PrinterState.Available;
        }

        /// <summary>排序：实体设备优先 → 可用优先 → 默认优先 → 任务少优先 → 名称。</summary>
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
    }
}