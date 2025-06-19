using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SystemMonitor.Services
{
    public class ResourceLimitService
    {
        private readonly Dictionary<int, ProcessResourceLimit> _processLimits;
        private readonly Dictionary<int, IntPtr> _jobHandles;
        private Timer _monitoringTimer;
        private readonly object _lockObject = new object();

        public ResourceLimitService()
        {
            _processLimits = new Dictionary<int, ProcessResourceLimit>();
            _jobHandles = new Dictionary<int, IntPtr>();
        }

        // Thêm giới hạn tài nguyên cho process
        public bool SetProcessResourceLimit(int processId, ProcessResourceLimit limit)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process == null || process.HasExited)
                    return false;

                lock (_lockObject)
                {
                    _processLimits[processId] = limit;
                }

                // Tạo Job Object để giới hạn tài nguyên
                return CreateJobForProcess(processId, limit);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thiết lập giới hạn tài nguyên: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Tạo Job Object cho process
        private bool CreateJobForProcess(int processId, ProcessResourceLimit limit)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process == null || process.HasExited)
                    return false;

                // Tạo Job Object
                string jobName = $"ResourceLimit_{processId}";
                IntPtr jobHandle = CreateJobObject(IntPtr.Zero, jobName);

                if (jobHandle == IntPtr.Zero)
                    return false;

                // Thêm process vào Job
                if (!AssignProcessToJobObject(jobHandle, process.Handle))
                {
                    CloseHandle(jobHandle);
                    return false;
                }

                // Thiết lập giới hạn memory
                if (limit.MemoryLimitMB > 0)
                {
                    JOBOBJECT_EXTENDED_LIMIT_INFORMATION extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                    extendedInfo.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_PROCESS_MEMORY;
                    extendedInfo.ProcessMemoryLimit = (UIntPtr)(limit.MemoryLimitMB * 1024 * 1024);

                    int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                    IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                    Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

                    SetInformationJobObject(jobHandle, JobObjectInfoType.ExtendedLimitInformation,
                        extendedInfoPtr, (uint)length);

                    Marshal.FreeHGlobal(extendedInfoPtr);
                }

                // Thiết lập giới hạn CPU
                if (limit.CpuLimitPercent > 0 && limit.CpuLimitPercent < 100)
                {
                    JOBOBJECT_CPU_RATE_CONTROL_INFORMATION cpuInfo = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION();
                    cpuInfo.ControlFlags = JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP;
                    cpuInfo.CpuRate = (uint)(limit.CpuLimitPercent * 100); // Tính theo 1/100 của 1%

                    int length = Marshal.SizeOf(typeof(JOBOBJECT_CPU_RATE_CONTROL_INFORMATION));
                    IntPtr cpuInfoPtr = Marshal.AllocHGlobal(length);
                    Marshal.StructureToPtr(cpuInfo, cpuInfoPtr, false);

                    SetInformationJobObject(jobHandle, JobObjectInfoType.CpuRateControlInformation,
                        cpuInfoPtr, (uint)length);

                    Marshal.FreeHGlobal(cpuInfoPtr);
                }

                _jobHandles[processId] = jobHandle;

                // Bắt đầu monitoring nếu chưa có
                StartMonitoring();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Bỏ giới hạn tài nguyên cho process
        public bool RemoveProcessResourceLimit(int processId)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_jobHandles.ContainsKey(processId))
                    {
                        CloseHandle(_jobHandles[processId]);
                        _jobHandles.Remove(processId);
                    }

                    if (_processLimits.ContainsKey(processId))
                    {
                        _processLimits.Remove(processId);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Lấy danh sách process có giới hạn tài nguyên
        public List<ProcessResourceLimit> GetProcessLimits()
        {
            lock (_lockObject)
            {
                return _processLimits.Values.ToList();
            }
        }

        // Bắt đầu monitoring
        private void StartMonitoring()
        {
            if (_monitoringTimer == null)
            {
                _monitoringTimer = new Timer(MonitorProcesses, null,
                    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            }
        }

        // Monitor các process có giới hạn
        private void MonitorProcesses(object state)
        {
            try
            {
                lock (_lockObject)
                {
                    var expiredProcesses = new List<int>();

                    foreach (var kvp in _processLimits)
                    {
                        try
                        {
                            var process = Process.GetProcessById(kvp.Key);
                            if (process == null || process.HasExited)
                            {
                                expiredProcesses.Add(kvp.Key);
                                continue;
                            }

                            // Kiểm tra memory usage
                            var memoryUsageMB = process.WorkingSet64 / (1024 * 1024);
                            if (kvp.Value.MemoryLimitMB > 0 && memoryUsageMB > kvp.Value.MemoryLimitMB * 1.1) // 10% buffer
                            {
                                // Thông báo vượt giới hạn memory
                                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    MessageBox.Show($"Process {process.ProcessName} (ID: {kvp.Key}) đã vượt giới hạn memory!\n" +
                                                  $"Giới hạn: {kvp.Value.MemoryLimitMB}MB, Thực tế: {memoryUsageMB}MB",
                                                  "Cảnh báo giới hạn tài nguyên", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }));
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Process không tồn tại
                            expiredProcesses.Add(kvp.Key);
                        }
                    }

                    // Xóa các process đã kết thúc
                    foreach (var processId in expiredProcesses)
                    {
                        RemoveProcessResourceLimit(processId);
                    }
                }
            }
            catch (Exception)
            {
                // Bỏ qua lỗi monitoring
            }
        }

        // Dừng service
        public void Stop()
        {
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;

            lock (_lockObject)
            {
                foreach (var handle in _jobHandles.Values)
                {
                    CloseHandle(handle);
                }
                _jobHandles.Clear();
                _processLimits.Clear();
            }
        }

        #region Windows API

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9,
            CpuRateControlInformation = 15
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
        {
            public uint ControlFlags;
            public uint CpuRate;
            public uint Weight;
            public ushort MinRate;
            public ushort MaxRate;
        }

        private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100;
        private const uint JOB_OBJECT_CPU_RATE_CONTROL_ENABLE = 0x1;
        private const uint JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP = 0x4;

        #endregion
    }

    public class ProcessResourceLimit
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public int MemoryLimitMB { get; set; }
        public int CpuLimitPercent { get; set; }
        public DateTime CreatedTime { get; set; }
        public bool IsActive { get; set; }

        public ProcessResourceLimit()
        {
            CreatedTime = DateTime.Now;
            IsActive = true;
        }

        public override string ToString()
        {
            return $"{ProcessName} (ID: {ProcessId}) - RAM: {MemoryLimitMB}MB, CPU: {CpuLimitPercent}%";
        }
    }
}