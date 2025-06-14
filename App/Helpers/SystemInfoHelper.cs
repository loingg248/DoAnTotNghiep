using System;
using System.Management;

namespace SystemMonitor.Helpers
{
    public static class SystemInfoHelper
    {
        // Phương thức hỗ trợ lấy nhiệt độ CPU từ WMI nếu LibreHardwareMonitor không làm việc
        public static float GetCpuTemperatureFromWmi()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        float temperature = Convert.ToSingle(obj["CurrentTemperature"].ToString());
                        // WMI báo cáo nhiệt độ theo một phần mười độ Kelvin
                        return (temperature / 10) - 273.15f;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Không thể lấy nhiệt độ từ WMI: {ex.Message}");
            }

            return 0;
        }

        // Các phương thức hỗ trợ khác...
    }
}