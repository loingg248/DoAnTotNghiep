using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Management;

namespace SystemMonitor.Services
{
    public class StartupService
    {
        private ListView? _startupListView;
        private TextBlock? _startupCountLabel;

        // Registry key để lưu trữ các disabled items
        private const string DISABLED_ITEMS_KEY = @"SOFTWARE\SystemMonitor\DisabledStartupItems";

        public ObservableCollection<StartupItem> StartupItems { get; private set; }

        public StartupService()
        {
            StartupItems = new ObservableCollection<StartupItem>();
        }

        public void SetUIControls(ListView startupListView, TextBlock startupCountLabel)
        {
            _startupListView = startupListView;
            _startupCountLabel = startupCountLabel;
            _startupListView.ItemsSource = StartupItems;
        }

        public void LoadStartupItems()
        {
            try
            {
                StartupItems.Clear();

                // Load từ Registry - Current User
                LoadFromRegistry(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "User");

                // Load từ Registry - Local Machine
                LoadFromRegistry(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "System");

                // Load từ Startup folder - Current User
                string userStartupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                LoadFromFolder(userStartupPath, "User Folder");

                // Load từ Startup folder - All Users
                string allUsersStartupPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                LoadFromFolder(allUsersStartupPath, "System Folder");

                // Load disabled items
                LoadDisabledItems();

                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách startup: {ex.Message}", "Lỗi",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RefreshStartupItems()
        {
            LoadStartupItems();
        }

        private void LoadFromRegistry(RegistryKey baseKey, string subKeyPath, string location)
        {
            try
            {
                using (RegistryKey? key = baseKey.OpenSubKey(subKeyPath))
                {
                    if (key != null)
                    {
                        foreach (string valueName in key.GetValueNames())
                        {
                            string? command = key.GetValue(valueName)?.ToString();
                            if (!string.IsNullOrEmpty(command))
                            {
                                var startupItem = new StartupItem
                                {
                                    Name = valueName,
                                    Command = command,
                                    Location = location,
                                    Type = "Registry",
                                    Status = "Enabled",
                                    RegistryKey = subKeyPath,
                                    RegistryHive = baseKey == Registry.CurrentUser ? "HKCU" : "HKLM"
                                };

                                // Lấy thông tin file nếu có thể
                                ExtractFileInfo(startupItem);
                                StartupItems.Add(startupItem);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue loading other items
                Debug.WriteLine($"Error loading from registry {subKeyPath}: {ex.Message}");
            }
        }

        private void LoadFromFolder(string folderPath, string location)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    // Load enabled files
                    var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                                        .Where(f => IsExecutableFile(f) && !f.EndsWith(".disabled"));

                    foreach (string filePath in files)
                    {
                        var startupItem = new StartupItem
                        {
                            Name = Path.GetFileNameWithoutExtension(filePath),
                            Command = filePath,
                            Location = location,
                            Type = "Folder",
                            Status = "Enabled",
                            FilePath = filePath
                        };

                        ExtractFileInfo(startupItem);
                        StartupItems.Add(startupItem);
                    }

                    // Load disabled files (files with .disabled extension)
                    var disabledFiles = Directory.GetFiles(folderPath, "*.disabled", SearchOption.TopDirectoryOnly);
                    foreach (string disabledFilePath in disabledFiles)
                    {
                        string originalName = Path.GetFileNameWithoutExtension(disabledFilePath);
                        if (IsExecutableFile(originalName))
                        {
                            var startupItem = new StartupItem
                            {
                                Name = Path.GetFileNameWithoutExtension(originalName),
                                Command = disabledFilePath.Replace(".disabled", ""),
                                Location = location,
                                Type = "Folder",
                                Status = "Disabled",
                                FilePath = disabledFilePath
                            };

                            ExtractFileInfo(startupItem);
                            StartupItems.Add(startupItem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading from folder {folderPath}: {ex.Message}");
            }
        }

        private void LoadDisabledItems()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(DISABLED_ITEMS_KEY))
                {
                    if (key != null)
                    {
                        foreach (string valueName in key.GetValueNames())
                        {
                            string? itemData = key.GetValue(valueName)?.ToString();
                            if (!string.IsNullOrEmpty(itemData))
                            {
                                // Parse stored data: command|location|registryKey|registryHive
                                string[] parts = itemData.Split('|');
                                if (parts.Length >= 4)
                                {
                                    var startupItem = new StartupItem
                                    {
                                        Name = valueName,
                                        Command = parts[0],
                                        Location = parts[1],
                                        Type = "Registry",
                                        Status = "Disabled",
                                        RegistryKey = parts[2],
                                        RegistryHive = parts[3]
                                    };

                                    ExtractFileInfo(startupItem);
                                    StartupItems.Add(startupItem);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading disabled items: {ex.Message}");
            }
        }

        private bool IsExecutableFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return new[] { ".exe", ".bat", ".cmd", ".com", ".pif", ".scr", ".lnk" }.Contains(extension);
        }

        private void ExtractFileInfo(StartupItem item)
        {
            try
            {
                // Tách file path từ command
                string filePath = ExtractFilePathFromCommand(item.Command);

                if (File.Exists(filePath))
                {
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                    item.Publisher = versionInfo.CompanyName ?? "Unknown";
                    item.Description = versionInfo.FileDescription ?? Path.GetFileName(filePath);
                    item.FilePath = filePath;
                }
                else
                {
                    item.Publisher = "Unknown";
                    item.Description = item.Name;
                }
            }
            catch
            {
                item.Publisher = "Unknown";
                item.Description = item.Name;
            }
        }

        private string ExtractFilePathFromCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return string.Empty;

            // Xử lý command có dấu ngoặc kép
            if (command.StartsWith("\""))
            {
                int endQuote = command.IndexOf("\"", 1);
                if (endQuote > 0)
                {
                    return command.Substring(1, endQuote - 1);
                }
            }

            // Xử lý command không có dấu ngoặc kép
            string[] parts = command.Split(' ');
            return parts[0];
        }

        public void DisableStartupItem(StartupItem item)
        {
            try
            {
                if (item.Type == "Registry")
                {
                    // Lưu thông tin item vào disabled registry trước khi xóa
                    SaveDisabledItem(item);

                    // Xóa khỏi startup registry
                    RegistryKey baseKey = item.RegistryHive == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                    using (RegistryKey? key = baseKey.OpenSubKey(item.RegistryKey, true))
                    {
                        if (key != null)
                        {
                            key.DeleteValue(item.Name, false);
                            item.Status = "Disabled";
                        }
                    }
                }
                else if (item.Type == "Folder" && File.Exists(item.FilePath))
                {
                    // Rename file to disable it
                    string disabledPath = item.FilePath + ".disabled";
                    File.Move(item.FilePath, disabledPath);
                    item.Status = "Disabled";
                    item.FilePath = disabledPath;
                }

                // Tự động refresh danh sách
                RefreshStartupItems();

                MessageBox.Show($"Đã vô hiệu hóa startup item: {item.Name}", "Thành công",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi vô hiệu hóa startup item: {ex.Message}", "Lỗi",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void EnableStartupItem(StartupItem item)
        {
            try
            {
                if (item.Type == "Registry" && item.Status == "Disabled")
                {
                    // Restore to original registry location
                    RegistryKey baseKey = item.RegistryHive == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                    using (RegistryKey? key = baseKey.OpenSubKey(item.RegistryKey, true))
                    {
                        if (key != null)
                        {
                            key.SetValue(item.Name, item.Command);
                            item.Status = "Enabled";
                        }
                    }

                    // Remove from disabled items storage
                    RemoveDisabledItem(item);
                }
                else if (item.Type == "Folder" && item.Status == "Disabled" &&
                         item.FilePath.EndsWith(".disabled") && File.Exists(item.FilePath))
                {
                    string enabledPath = item.FilePath.Replace(".disabled", "");
                    File.Move(item.FilePath, enabledPath);
                    item.Status = "Enabled";
                    item.FilePath = enabledPath;
                }

                // Tự động refresh danh sách
                RefreshStartupItems();

                MessageBox.Show($"Đã kích hoạt startup item: {item.Name}", "Thành công",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi kích hoạt startup item: {ex.Message}", "Lỗi",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDisabledItem(StartupItem item)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(DISABLED_ITEMS_KEY))
                {
                    if (key != null)
                    {
                        // Store: command|location|registryKey|registryHive
                        string itemData = $"{item.Command}|{item.Location}|{item.RegistryKey}|{item.RegistryHive}";
                        key.SetValue(item.Name, itemData);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving disabled item: {ex.Message}");
            }
        }

        private void RemoveDisabledItem(StartupItem item)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(DISABLED_ITEMS_KEY, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(item.Name, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error removing disabled item: {ex.Message}");
            }
        }

        public void OpenFileLocation(StartupItem item)
        {
            try
            {
                string filePath = item.FilePath;
                if (item.Status == "Disabled" && item.Type == "Folder")
                {
                    // For disabled folder items, use the original path
                    filePath = item.FilePath.Replace(".disabled", "");
                }

                if (File.Exists(item.FilePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
                }
                else if (File.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else
                {
                    MessageBox.Show("Không tìm thấy file", "Thông báo",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi mở vị trí file: {ex.Message}", "Lỗi",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateUI()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_startupCountLabel != null)
                {
                    int enabledCount = StartupItems.Count(x => x.Status == "Enabled");
                    int totalCount = StartupItems.Count;
                    _startupCountLabel.Text = $"Tổng số startup items: {totalCount} (Enabled: {enabledCount}, Disabled: {totalCount - enabledCount})";
                }
            });
        }
        public void ForceUpdateUI()
        {
            UpdateUI();
        }
    }

    public class StartupItem
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string RegistryKey { get; set; } = string.Empty;
        public string RegistryHive { get; set; } = string.Empty;
    }
}