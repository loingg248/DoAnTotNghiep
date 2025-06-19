using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;

namespace SystemMonitor.Helpers
{
    public static class StartupHelper
    {
        private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "SystemMonitor";

        /// <summary>
        /// Thêm ứng dụng vào danh sách khởi động cùng Windows
        /// </summary>
        /// <param name="startMinimized">Có khởi động ở chế độ ngầm không</param>
        public static bool AddToStartup(bool startMinimized = true)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (key != null)
                    {
                        string exePath = Assembly.GetExecutingAssembly().Location;
                        string arguments = startMinimized ? " --minimized" : "";

                        key.SetValue(APP_NAME, $"\"{exePath}\"{arguments}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding to startup: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Xóa ứng dụng khỏi danh sách khởi động
        /// </summary>
        public static bool RemoveFromStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(APP_NAME, false);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing from startup: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Kiểm tra ứng dụng có trong danh sách khởi động không
        /// </summary>
        public static bool IsInStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(APP_NAME);
                        return value != null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking startup status: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Tạo shortcut để khởi động ở chế độ ngầm
        /// </summary>
        public static void CreateTrayShortcut()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, "System Monitor (Tray).lnk");
                string exePath = Assembly.GetExecutingAssembly().Location;

                // Sử dụng COM để tạo shortcut
                var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                var shortcut = shell.GetType().InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });

                shortcut.GetType().InvokeMember("TargetPath",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                shortcut.GetType().InvokeMember("Arguments",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "--minimized" });
                shortcut.GetType().InvokeMember("Description",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "System Monitor - Chạy ngầm" });
                shortcut.GetType().InvokeMember("Save",
                    System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating shortcut: {ex.Message}");
            }
        }
    }
}