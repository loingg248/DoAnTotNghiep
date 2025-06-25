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
        private readonly Dictionary<int, ProcessThrottleInfo> _throttleInfo;
        private Timer _monitoringTimer;
        private readonly object _lockObject = new object();

        public ResourceLimitService()
        {
            _processLimits = new Dictionary<int, ProcessResourceLimit>();
            _jobHandles = new Dictionary<int, IntPtr>();
            _throttleInfo = new Dictionary<int, ProcessThrottleInfo>();
        }

        // Thêm giới hạn tài nguyên với các tùy chọn mới
        public bool SetProcessResourceLimit(int processId, ProcessResourceLimit limit, ResourceLimitOptions options = null)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process == null || process.HasExited)
                    return false;

                lock (_lockObject)
                {
                    _processLimits[processId] = limit;
                    _throttleInfo[processId] = new ProcessThrottleInfo
                    {
                        Options = options ?? new ResourceLimitOptions(),
                        LastMemoryCheck = DateTime.Now,
                        ViolationCount = 0
                    };
                }

                return CreateAdvancedJobForProcess(processId, limit, options ?? new ResourceLimitOptions());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thiết lập giới hạn tài nguyên: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Tạo Job Object với các tính năng nâng cao
        private bool CreateAdvancedJobForProcess(int processId, ProcessResourceLimit limit, ResourceLimitOptions options)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process == null || process.HasExited)
                    return false;

                string jobName = $"AdvancedResourceLimit_{processId}";
                IntPtr jobHandle = CreateJobObject(IntPtr.Zero, jobName);

                if (jobHandle == IntPtr.Zero)
                    return false;

                if (!AssignProcessToJobObject(jobHandle, process.Handle))
                {
                    CloseHandle(jobHandle);
                    return false;
                }

                // 1. Thiết lập giới hạn memory cứng (Hard Limit)
                if (limit.MemoryLimitMB > 0)
                {
                    SetMemoryLimits(jobHandle, limit.MemoryLimitMB, options);
                }

                // 2. Thiết lập giới hạn CPU với throttling
                if (limit.CpuLimitPercent > 0 && limit.CpuLimitPercent < 100)
                {
                    SetCpuLimits(jobHandle, limit.CpuLimitPercent, options);
                }

                // 3. Thiết lập giới hạn I/O nếu được chỉ định
                if (options.EnableIoThrottling)
                {
                    SetIoLimits(jobHandle, options);
                }

                // 4. Thiết lập Process Priority
                if (options.EnablePriorityAdjustment)
                {
                    SetProcessPriority(process, options.ThrottledPriority);
                }

                _jobHandles[processId] = jobHandle;
                StartMonitoring();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Thiết lập giới hạn memory với các tùy chọn
        private void SetMemoryLimits(IntPtr jobHandle, int memoryLimitMB, ResourceLimitOptions options)
        {
            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();

            // Hard limit - process sẽ bị terminate nếu vượt quá
            if (options.UseHardMemoryLimit)
            {
                extendedInfo.BasicLimitInformation.LimitFlags |= JOB_OBJECT_LIMIT_PROCESS_MEMORY;
                extendedInfo.ProcessMemoryLimit = (UIntPtr)(memoryLimitMB * 1024 * 1024);
            }

            // Working set limit - Windows sẽ tự động trim memory
            if (options.UseWorkingSetLimit)
            {
                extendedInfo.BasicLimitInformation.LimitFlags |= JOB_OBJECT_LIMIT_WORKINGSET;
                extendedInfo.BasicLimitInformation.MinimumWorkingSetSize = (UIntPtr)(memoryLimitMB * 0.5 * 1024 * 1024); // 50% minimum
                extendedInfo.BasicLimitInformation.MaximumWorkingSetSize = (UIntPtr)(memoryLimitMB * 1024 * 1024);
            }

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

            SetInformationJobObject(jobHandle, JobObjectInfoType.ExtendedLimitInformation,
                extendedInfoPtr, (uint)length);

            Marshal.FreeHGlobal(extendedInfoPtr);
        }

        // Thiết lập giới hạn CPU với throttling
        private void SetCpuLimits(IntPtr jobHandle, int cpuLimitPercent, ResourceLimitOptions options)
        {
            var cpuInfo = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION();

            if (options.UseHardCpuLimit)
            {
                // Hard cap - CPU sẽ bị giới hạn cứng
                cpuInfo.ControlFlags = JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP;
                cpuInfo.CpuRate = (uint)(cpuLimitPercent * 100);
            }
            else
            {
                // Weight-based throttling - linh hoạt hơn
                cpuInfo.ControlFlags = JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_WEIGHT_BASED;
                cpuInfo.Weight = (uint)Math.Max(1, cpuLimitPercent * 10); // Weight từ 1-1000
            }

            int length = Marshal.SizeOf(typeof(JOBOBJECT_CPU_RATE_CONTROL_INFORMATION));
            IntPtr cpuInfoPtr = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(cpuInfo, cpuInfoPtr, false);

            SetInformationJobObject(jobHandle, JobObjectInfoType.CpuRateControlInformation,
                cpuInfoPtr, (uint)length);

            Marshal.FreeHGlobal(cpuInfoPtr);
        }

        // Thiết lập giới hạn I/O
        private void SetIoLimits(IntPtr jobHandle, ResourceLimitOptions options)
        {
            // Sử dụng JOBOBJECT_IO_RATE_CONTROL_INFORMATION nếu Windows hỗ trợ
            // Hoặc điều chỉnh process priority để giảm I/O priority
        }

        // Điều chỉnh Process Priority
        private void SetProcessPriority(Process process, ProcessPriorityClass priority)
        {
            try
            {
                process.PriorityClass = priority;
            }
            catch (Exception)
            {
                // Không thể thay đổi priority
            }
        }

        // Monitor nâng cao với các hành động khác nhau
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

                            var throttleInfo = _throttleInfo[kvp.Key];
                            var limit = kvp.Value;

                            // Kiểm tra memory
                            var memoryUsageMB = process.WorkingSet64 / (1024 * 1024);

                            if (limit.MemoryLimitMB > 0)
                            {
                                HandleMemoryViolation(process, limit, throttleInfo, memoryUsageMB);
                            }

                            // Kiểm tra CPU (cần implement CPU usage tracking)
                            if (limit.CpuLimitPercent > 0)
                            {
                                HandleCpuViolation(process, limit, throttleInfo);
                            }

                        }
                        catch (ArgumentException)
                        {
                            expiredProcesses.Add(kvp.Key);
                        }
                    }

                    foreach (var processId in expiredProcesses)
                    {
                        RemoveProcessResourceLimit(processId);
                    }
                }
            }
            catch (Exception)
            {
                // Log error
            }
        }

        // Xử lý vi phạm memory
        private void HandleMemoryViolation(Process process, ProcessResourceLimit limit,
            ProcessThrottleInfo throttleInfo, long memoryUsageMB)
        {
            var options = throttleInfo.Options;
            var violationThreshold = limit.MemoryLimitMB * (1.0 + options.MemoryViolationThreshold);

            if (memoryUsageMB > violationThreshold)
            {
                throttleInfo.ViolationCount++;

                switch (options.MemoryViolationAction)
                {
                    case ViolationAction.Warning:
                        ShowMemoryWarning(process.ProcessName, process.Id, limit.MemoryLimitMB, memoryUsageMB);
                        break;

                    case ViolationAction.Throttle:
                        ThrottleProcess(process, throttleInfo);
                        break;

                    case ViolationAction.TrimWorkingSet:
                        TrimProcessWorkingSet(process);
                        break;

                    case ViolationAction.SuspendResume:
                        if (throttleInfo.ViolationCount % 3 == 0) // Suspend mỗi 3 lần vi phạm
                        {
                            SuspendResumeProcess(process, throttleInfo);
                        }
                        break;
                }
            }
            else if (memoryUsageMB < limit.MemoryLimitMB * 0.8) // Dưới 80% limit
            {
                // Reset violation count và restore priority nếu cần
                if (throttleInfo.ViolationCount > 0)
                {
                    throttleInfo.ViolationCount = Math.Max(0, throttleInfo.ViolationCount - 1);
                    if (throttleInfo.IsThrottled)
                    {
                        RestoreProcessPriority(process, throttleInfo);
                    }
                }
            }
        }

        // Xử lý vi phạm CPU
        private void HandleCpuViolation(Process process, ProcessResourceLimit limit, ProcessThrottleInfo throttleInfo)
        {
            // Implementation for CPU monitoring and throttling
            // Cần thêm CPU usage tracking
        }

        // Throttle process bằng cách giảm priority
        private void ThrottleProcess(Process process, ProcessThrottleInfo throttleInfo)
        {
            if (!throttleInfo.IsThrottled)
            {
                try
                {
                    throttleInfo.OriginalPriority = process.PriorityClass;
                    process.PriorityClass = throttleInfo.Options.ThrottledPriority;
                    throttleInfo.IsThrottled = true;
                    throttleInfo.ThrottleStartTime = DateTime.Now;
                }
                catch (Exception)
                {
                    // Không thể thay đổi priority
                }
            }
        }

        // Trim working set của process
        private void TrimProcessWorkingSet(Process process)
        {
            try
            {
                // Gọi Windows API để trim working set
                EmptyWorkingSet(process.Handle);
            }
            catch (Exception)
            {
                // Không thể trim working set
            }
        }

        // Suspend/Resume process tạm thời
        private void SuspendResumeProcess(Process process, ProcessThrottleInfo throttleInfo)
        {
            try
            {
                if (!throttleInfo.IsSuspended)
                {
                    SuspendProcess(process.Id);
                    throttleInfo.IsSuspended = true;
                    throttleInfo.SuspendStartTime = DateTime.Now;

                    // Tự động resume sau một khoảng thời gian
                    Task.Delay(throttleInfo.Options.SuspendDurationMs).ContinueWith(_ =>
                    {
                        if (throttleInfo.IsSuspended)
                        {
                            ResumeProcess(process.Id);
                            throttleInfo.IsSuspended = false;
                        }
                    });
                }
            }
            catch (Exception)
            {
                // Không thể suspend process
            }
        }

        // Restore process priority
        private void RestoreProcessPriority(Process process, ProcessThrottleInfo throttleInfo)
        {
            if (throttleInfo.IsThrottled)
            {
                try
                {
                    process.PriorityClass = throttleInfo.OriginalPriority;
                    throttleInfo.IsThrottled = false;
                }
                catch (Exception)
                {
                    // Không thể restore priority
                }
            }
        }

        // Show memory warning
        private void ShowMemoryWarning(string processName, int processId, int limitMB, long actualMB)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show($"Process {processName} (ID: {processId}) đã vượt giới hạn memory!\n" +
                              $"Giới hạn: {limitMB}MB, Thực tế: {actualMB}MB",
                              "Cảnh báo giới hạn tài nguyên", MessageBoxButton.OK, MessageBoxImage.Warning);
            }));
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

                    if (_throttleInfo.ContainsKey(processId))
                    {
                        var throttleInfo = _throttleInfo[processId];

                        // Restore process state nếu cần
                        if (throttleInfo.IsThrottled || throttleInfo.IsSuspended)
                        {
                            try
                            {
                                var process = Process.GetProcessById(processId);
                                if (throttleInfo.IsThrottled)
                                {
                                    process.PriorityClass = throttleInfo.OriginalPriority;
                                }
                                if (throttleInfo.IsSuspended)
                                {
                                    ResumeProcess(processId);
                                }
                            }
                            catch (Exception)
                            {
                                // Process có thể đã kết thúc
                            }
                        }

                        _throttleInfo.Remove(processId);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Bắt đầu monitoring
        private void StartMonitoring()
        {
            if (_monitoringTimer == null)
            {
                _monitoringTimer = new Timer(MonitorProcesses, null,
                    TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)); // Tăng tần suất check
            }
        }

        public void Stop()
        {
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;

            lock (_lockObject)
            {
                // Restore tất cả processes về trạng thái ban đầu
                foreach (var kvp in _throttleInfo.ToList())
                {
                    RemoveProcessResourceLimit(kvp.Key);
                }

                foreach (var handle in _jobHandles.Values)
                {
                    CloseHandle(handle);
                }

                _jobHandles.Clear();
                _processLimits.Clear();
                _throttleInfo.Clear();
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

        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        private static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll")]
        private static extern int NtResumeProcess(IntPtr processHandle);

        private void SuspendProcess(int processId)
        {
            var process = Process.GetProcessById(processId);
            NtSuspendProcess(process.Handle);
        }

        private void ResumeProcess(int processId)
        {
            var process = Process.GetProcessById(processId);
            NtResumeProcess(process.Handle);
        }
        public List<ProcessResourceLimit> GetProcessLimits()
        {
            lock (_lockObject)
            {
                return _processLimits.Values.ToList();
            }
        }

        private enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9,
            CpuRateControlInformation = 15
        }

        // Existing structs...
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
        private const uint JOB_OBJECT_LIMIT_WORKINGSET = 0x00000001;
        private const uint JOB_OBJECT_CPU_RATE_CONTROL_ENABLE = 0x1;
        private const uint JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP = 0x4;
        private const uint JOB_OBJECT_CPU_RATE_CONTROL_WEIGHT_BASED = 0x2;

        #endregion
    }

    // Các class hỗ trợ
    public class ResourceLimitOptions
    {
        public bool UseHardMemoryLimit { get; set; } = false; // Hard limit sẽ kill process
        public bool UseWorkingSetLimit { get; set; } = true;  // Soft limit, Windows tự trim
        public bool UseHardCpuLimit { get; set; } = false;    // Hard CPU cap
        public bool EnablePriorityAdjustment { get; set; } = true;
        public bool EnableIoThrottling { get; set; } = false;

        public double MemoryViolationThreshold { get; set; } = 0.1; // 10% over limit
        public ViolationAction MemoryViolationAction { get; set; } = ViolationAction.TrimWorkingSet;
        public ViolationAction CpuViolationAction { get; set; } = ViolationAction.Throttle;

        public ProcessPriorityClass ThrottledPriority { get; set; } = ProcessPriorityClass.BelowNormal;
        public int SuspendDurationMs { get; set; } = 1000; // 1 giây
    }

    public enum ViolationAction
    {
        Warning,        // Chỉ cảnh báo
        Throttle,       // Giảm priority
        TrimWorkingSet, // Trim memory
        SuspendResume   // Suspend tạm thời
    }

    public class ProcessThrottleInfo
    {
        public ResourceLimitOptions Options { get; set; }
        public DateTime LastMemoryCheck { get; set; }
        public int ViolationCount { get; set; }
        public bool IsThrottled { get; set; }
        public bool IsSuspended { get; set; }
        public DateTime ThrottleStartTime { get; set; }
        public DateTime SuspendStartTime { get; set; }
        public ProcessPriorityClass OriginalPriority { get; set; } = ProcessPriorityClass.Normal;
    }

    // Existing ProcessResourceLimit class remains the same...
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