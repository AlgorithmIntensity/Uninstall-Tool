using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using UninstallTool.Modules;

namespace UninstallTool
{
    public partial class Form1 : Form
    {
        private List<InstalledApp> installedApps;
        private ProcessManager processManager;
        private ServiceRemover serviceRemover;
        private FileCleaner fileCleaner;
        private RegistryCleaner registryCleaner;
        private string currentLanguage = "en-US";

        public Form1()
        {
            InitializeComponent();
            InitializeComponents();
            LoadInstalledAppsFromRegistry();
            ApplyLanguage();
        }

        private void InitializeComponents()
        {
            processManager = new ProcessManager();
            serviceRemover = new ServiceRemover();
            fileCleaner = new FileCleaner();
            registryCleaner = new RegistryCleaner();

            currentLanguage = System.Globalization.CultureInfo.CurrentCulture.Name;
            if (currentLanguage.StartsWith("ru"))
                currentLanguage = "ru-RU";
            else
                currentLanguage = "en-US";

            killProcessesBtn.Enabled = false;
            UninstallBtn.Enabled = false;

            appsList.DisplayMember = "DisplayName";
            appsList.SelectionMode = SelectionMode.MultiExtended;

            logTextBox.ReadOnly = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            txtDetails.ReadOnly = true;
            txtDetails.ScrollBars = ScrollBars.Vertical;

            UpdateStatus(GetLocalizedString("Ready", "Готов к работе"));
        }

        private void ApplyLanguage()
        {
            if (currentLanguage == "ru-RU")
            {
                this.Text = "Удаление программ";
                label1.Text = "Uninstall Tool";
                label2.Text = "by @argdus | @AlgorithmIntensity";
                label3.Text = "Лог:";
                label4.Text = "Информация о программе:";
                killProcessesBtn.Text = "Остановить процессы";
                UninstallBtn.Text = "Удалить";
                refreshBtn.Text = "Обновить";
                scanBtn.Text = "Сканировать";
            }
            else
            {
                this.Text = "Uninstall Tool";
                label1.Text = "Uninstall Tool";
                label2.Text = "by @argdus | @AlgorithmIntensity";
                label3.Text = "Log:";
                label4.Text = "Program info:";
                killProcessesBtn.Text = "Kill Processes";
                UninstallBtn.Text = "Uninstall";
                refreshBtn.Text = "Refresh";
                scanBtn.Text = "Scan";
            }
        }

        private string GetLocalizedString(string english, string russian)
        {
            return currentLanguage == "ru-RU" ? russian : english;
        }

        private void LoadInstalledAppsFromRegistry()
        {
            try
            {
                installedApps = new List<InstalledApp>();
                progressBar.Visible = true;
                progressBar.Style = ProgressBarStyle.Marquee;
                UpdateStatus(GetLocalizedString("Scanning registry...", "Сканирую реестр..."));

                ReadRegistryUninstallKeys(RegistryView.Registry64);
                ReadRegistryUninstallKeys(RegistryView.Registry32);

                ReadCurrentUserUninstallKeys();

                installedApps = installedApps
                    .OrderBy(app => app.DisplayName)
                    .ToList();

                appsList.DataSource = null;
                appsList.DataSource = installedApps;
                appsList.DisplayMember = "DisplayName";

                progressBar.Visible = false;
                UpdateStatus(GetLocalizedString($"Found programs: {installedApps.Count}",
                    $"Найдено программ: {installedApps.Count}"));
            }
            catch (Exception ex)
            {
                progressBar.Visible = false;
                UpdateStatus(GetLocalizedString($"Error loading programs: {ex.Message}",
                    $"Ошибка загрузки программ: {ex.Message}"), true);
            }
        }

        private void ReadRegistryUninstallKeys(RegistryView registryView)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
                using (var uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (uninstallKey == null) return;

                    foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        try
                        {
                            using (var subKey = uninstallKey.OpenSubKey(subKeyName))
                            {
                                var app = CreateAppFromRegistryKey(subKey, subKeyName, registryView);
                                if (app != null && !string.IsNullOrEmpty(app.DisplayName))
                                {
                                    installedApps.Add(app);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private void ReadCurrentUserUninstallKeys()
        {
            try
            {
                using (var uninstallKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (uninstallKey == null) return;

                    foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        try
                        {
                            using (var subKey = uninstallKey.OpenSubKey(subKeyName))
                            {
                                var app = CreateAppFromRegistryKey(subKey, subKeyName, RegistryView.Default);
                                if (app != null && !string.IsNullOrEmpty(app.DisplayName))
                                {
                                    installedApps.Add(app);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private InstalledApp CreateAppFromRegistryKey(RegistryKey key, string keyName, RegistryView registryView)
        {
            try
            {
                var displayName = key.GetValue("DisplayName") as string;
                var displayVersion = key.GetValue("DisplayVersion") as string;
                var publisher = key.GetValue("Publisher") as string;
                var installDate = key.GetValue("InstallDate") as string;
                var uninstallString = key.GetValue("UninstallString") as string;
                var installLocation = key.GetValue("InstallLocation") as string;
                var quietUninstallString = key.GetValue("QuietUninstallString") as string;

                if (string.IsNullOrEmpty(displayName) ||
                    displayName.Contains("Security Update") ||
                    displayName.Contains("Update for") ||
                    displayName.Contains("Hotfix") ||
                    displayName.Contains("Service Pack") ||
                    key.GetValue("SystemComponent") != null ||
                    key.GetValue("ParentKeyName") != null ||
                    key.GetValue("WindowsInstaller") != null)
                {
                    return null;
                }

                return new InstalledApp
                {
                    RegistryKey = keyName,
                    DisplayName = displayName,
                    Version = displayVersion ?? GetLocalizedString("Unknown", "Неизвестно"),
                    Publisher = publisher ?? GetLocalizedString("Unknown", "Неизвестно"),
                    InstallDate = ParseInstallDate(installDate),
                    UninstallString = uninstallString ?? "",
                    QuietUninstallString = quietUninstallString ?? "",
                    InstallPath = installLocation ?? "",
                    EstimatedSize = GetEstimatedSize(key),
                    Is64Bit = registryView == RegistryView.Registry64
                };
            }
            catch
            {
                return null;
            }
        }

        private DateTime ParseInstallDate(string installDate)
        {
            if (string.IsNullOrEmpty(installDate) || installDate.Length != 8)
                return DateTime.MinValue;

            try
            {
                int year = int.Parse(installDate.Substring(0, 4));
                int month = int.Parse(installDate.Substring(4, 2));
                int day = int.Parse(installDate.Substring(6, 2));
                return new DateTime(year, month, day);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private string GetEstimatedSize(RegistryKey key)
        {
            try
            {
                var sizeValue = key.GetValue("EstimatedSize");
                if (sizeValue != null)
                {
                    long size = Convert.ToInt64(sizeValue);
                    if (size > 0)
                    {
                        return FormatFileSize(size * 1024);
                    }
                }
            }
            catch { }
            return GetLocalizedString("Unknown", "Неизвестно");
        }

        private string FormatFileSize(long bytes)
        {
            if (currentLanguage == "ru-RU")
            {
                string[] sizes = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len /= 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
            else
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len /= 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }

        private void appsList_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool hasSelection = appsList.SelectedItems.Count > 0;
            killProcessesBtn.Enabled = hasSelection;
            UninstallBtn.Enabled = hasSelection;

            if (appsList.SelectedItem != null)
            {
                var selectedApp = appsList.SelectedItem as InstalledApp;
                UpdateAppDetails(selectedApp);
            }
        }

        private void UpdateAppDetails(InstalledApp app)
        {
            if (app == null) return;

            if (currentLanguage == "ru-RU")
            {
                txtDetails.Text = $"Название: {app.DisplayName}\r\n" +
                                 $"Версия: {app.Version}\r\n" +
                                 $"Издатель: {app.Publisher}\r\n" +
                                 $"Дата установки: {app.InstallDate:dd.MM.yyyy}\r\n" +
                                 $"Размер: {app.EstimatedSize}\r\n" +
                                 $"Путь установки: {app.InstallPath}\r\n" +
                                 $"Разрядность: {(app.Is64Bit ? "64-bit" : "32-bit")}";
            }
            else
            {
                txtDetails.Text = $"Name: {app.DisplayName}\r\n" +
                                 $"Version: {app.Version}\r\n" +
                                 $"Publisher: {app.Publisher}\r\n" +
                                 $"Install date: {app.InstallDate:MM/dd/yyyy}\r\n" +
                                 $"Size: {app.EstimatedSize}\r\n" +
                                 $"Install path: {app.InstallPath}\r\n" +
                                 $"Architecture: {(app.Is64Bit ? "64-bit" : "32-bit")}";
            }
        }

        private void killProcessesBtn_Click(object sender, EventArgs e)
        {
            if (appsList.SelectedItems.Count == 0)
            {
                MessageBox.Show(
                    GetLocalizedString("Select a program from the list", "Выберите программу из списка"),
                    GetLocalizedString("Attention", "Внимание"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int killedCount = 0;

                foreach (InstalledApp app in appsList.SelectedItems)
                {
                    UpdateStatus(GetLocalizedString($"Stopping processes: {app.DisplayName}",
                        $"Останавливаем процессы: {app.DisplayName}"));

                    string exeName = Path.GetFileNameWithoutExtension(app.InstallPath);
                    if (!string.IsNullOrEmpty(exeName))
                    {
                        processManager.TerminateProcess(exeName);
                        killedCount++;
                    }

                    if (processManager.IsProcessRunning(exeName))
                    {
                        UpdateStatus(GetLocalizedString($"Failed to stop: {app.DisplayName}",
                            $"Не удалось остановить: {app.DisplayName}"), true);
                    }
                    else
                    {
                        UpdateStatus(GetLocalizedString($"Processes stopped: {app.DisplayName}",
                            $"Процессы остановлены: {app.DisplayName}"));
                    }
                }

                MessageBox.Show(
                    GetLocalizedString($"Stopped processes: {killedCount}", $"Остановлено процессов: {killedCount}"),
                    GetLocalizedString("Done", "Готово"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                UpdateStatus(GetLocalizedString($"Error: {ex.Message}", $"Ошибка: {ex.Message}"), true);
                MessageBox.Show(
                    GetLocalizedString($"Error: {ex.Message}", $"Ошибка: {ex.Message}"),
                    GetLocalizedString("Error", "Ошибка"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UninstallBtn_Click(object sender, EventArgs e)
        {
            if (appsList.SelectedItems.Count == 0)
            {
                MessageBox.Show(
                    GetLocalizedString("Select a program to uninstall", "Выберите программу для удаления"),
                    GetLocalizedString("Attention", "Внимание"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show(
                GetLocalizedString(
                    "Are you sure you want to uninstall the selected programs?\n" +
                    "This action cannot be undone.",
                    "Вы уверены, что хотите удалить выбранные программы?\n" +
                    "Это действие невозможно отменить."),
                GetLocalizedString("Confirm uninstallation", "Подтверждение удаления"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            try
            {
                progressBar.Visible = true;
                progressBar.Value = 0;
                progressBar.Maximum = appsList.SelectedItems.Count;
                progressBar.Style = ProgressBarStyle.Blocks;

                int completed = 0;

                foreach (InstalledApp app in appsList.SelectedItems)
                {
                    UpdateStatus(GetLocalizedString($"Uninstalling: {app.DisplayName}",
                        $"Удаляем: {app.DisplayName}"));

                    if (ExecuteUninstall(app))
                    {
                        installedApps.Remove(app);
                        UpdateStatus(GetLocalizedString($"Successfully removed: {app.DisplayName}",
                            $"Успешно удалено: {app.DisplayName}"));
                    }
                    else
                    {
                        UpdateStatus(GetLocalizedString($"Failed to remove: {app.DisplayName}",
                            $"Не удалось удалить: {app.DisplayName}"), true);
                    }

                    completed++;
                    progressBar.Value = completed;
                    Application.DoEvents();
                }

                appsList.DataSource = null;
                appsList.DataSource = installedApps;
                appsList.DisplayMember = "DisplayName";

                progressBar.Visible = false;
                txtDetails.Clear();

                MessageBox.Show(
                    GetLocalizedString("Uninstallation completed!", "Удаление завершено!"),
                    GetLocalizedString("Done", "Готово"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                UpdateStatus(GetLocalizedString($"Uninstallation error: {ex.Message}",
                    $"Ошибка удаления: {ex.Message}"), true);
                MessageBox.Show(
                    GetLocalizedString($"Error: {ex.Message}", $"Ошибка: {ex.Message}"),
                    GetLocalizedString("Error", "Ошибка"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private bool ExecuteUninstall(InstalledApp app)
        {
            try
            {
                if (!string.IsNullOrEmpty(app.QuietUninstallString))
                {
                    return RunUninstallCommand(app.QuietUninstallString, true);
                }

                if (!string.IsNullOrEmpty(app.UninstallString))
                {
                    return RunUninstallCommand(app.UninstallString, false);
                }

                return ManualUninstall(app);
            }
            catch (Exception ex)
            {
                UpdateStatus(GetLocalizedString($"Error uninstalling {app.DisplayName}: {ex.Message}",
                    $"Ошибка при удалении {app.DisplayName}: {ex.Message}"), true);
                return false;
            }
        }

        private bool RunUninstallCommand(string command, bool quiet)
        {
            try
            {
                string args = "/quiet /norestart";
                string fileName = command;

                if (command.StartsWith("\""))
                {
                    int endQuote = command.IndexOf('\"', 1);
                    if (endQuote > 0)
                    {
                        fileName = command.Substring(1, endQuote - 1);
                        args = command.Substring(endQuote + 1).Trim();
                    }
                }

                if (quiet && !args.Contains("/quiet"))
                {
                    args += " /quiet /norestart";
                }

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = args,
                        CreateNoWindow = quiet,
                        UseShellExecute = !quiet,
                        Verb = "runas"
                    }
                };

                UpdateStatus(GetLocalizedString($"Starting: {fileName} {args}",
                    $"Запуск: {fileName} {args}"));
                bool started = process.Start();

                if (quiet)
                {
                    process.WaitForExit(30000);
                    return process.ExitCode == 0;
                }

                return started;
            }
            catch (Exception ex)
            {
                UpdateStatus(GetLocalizedString($"Error starting uninstall: {ex.Message}",
                    $"Ошибка запуска удаления: {ex.Message}"), true);
                return false;
            }
        }

        private bool ManualUninstall(InstalledApp app)
        {
            try
            {
                string exeName = Path.GetFileNameWithoutExtension(app.InstallPath);
                if (!string.IsNullOrEmpty(exeName))
                {
                    processManager.TerminateProcess(exeName);
                    processManager.WaitForProcessExit(exeName, 5000);
                }

                if (!string.IsNullOrEmpty(app.InstallPath) && Directory.Exists(app.InstallPath))
                {
                    try
                    {
                        Directory.Delete(app.InstallPath, true);
                        UpdateStatus(GetLocalizedString($"Deleted folder: {app.InstallPath}",
                            $"Удалена папка: {app.InstallPath}"));
                    }
                    catch { }
                }

                try
                {
                    var registryPath = app.Is64Bit ?
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + app.RegistryKey :
                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\" + app.RegistryKey;

                    using (var key = Registry.LocalMachine.OpenSubKey(registryPath, true))
                    {
                        key?.DeleteSubKeyTree(app.RegistryKey, false);
                    }

                    UpdateStatus(GetLocalizedString($"Registry entries removed: {app.RegistryKey}",
                        $"Удалены записи реестра: {app.RegistryKey}"));
                }
                catch { }

                CleanShortcuts(app.DisplayName);

                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus(GetLocalizedString($"Manual uninstall error: {ex.Message}",
                    $"Ошибка ручного удаления: {ex.Message}"), true);
                return false;
            }
        }

        private void CleanShortcuts(string appName)
        {
            try
            {
                string startMenuPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs");

                if (Directory.Exists(startMenuPath))
                {
                    foreach (var shortcut in Directory.GetFiles(startMenuPath, "*.lnk", SearchOption.AllDirectories))
                    {
                        if (shortcut.Contains(appName))
                        {
                            File.Delete(shortcut);
                        }
                    }
                }

                string desktopPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"{appName}.lnk");

                if (File.Exists(desktopPath))
                {
                    File.Delete(desktopPath);
                }
            }
            catch { }
        }

        private void refreshBtn_Click(object sender, EventArgs e)
        {
            LoadInstalledAppsFromRegistry();
            txtDetails.Clear();
            UpdateStatus(GetLocalizedString("List updated", "Список обновлен"));
        }

        private void scanBtn_Click(object sender, EventArgs e)
        {
            UpdateStatus(GetLocalizedString("Scanning registry...", "Сканирование реестра..."));
            LoadInstalledAppsFromRegistry();
            UpdateStatus(GetLocalizedString("Scan completed", "Сканирование завершено"));
        }

        private void searchBox_TextChanged(object sender, EventArgs e)
        {
            string searchText = searchBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                appsList.DataSource = installedApps;
            }
            else
            {
                var filtered = installedApps
                    .Where(app => app.DisplayName.ToLower().Contains(searchText) ||
                                  app.Publisher.ToLower().Contains(searchText))
                    .ToList();

                appsList.DataSource = null;
                appsList.DataSource = filtered;
                appsList.DisplayMember = "DisplayName";

                UpdateStatus(GetLocalizedString($"Found: {filtered.Count} programs",
                    $"Найдено: {filtered.Count} программ"));
            }
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            statusLabel.Text = message;
            statusLabel.ForeColor = isError ? Color.Red : Color.Black;

            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
            logTextBox.ScrollToCaret();
            Application.DoEvents();
        }

        public class InstalledApp
        {
            public string RegistryKey { get; set; }
            public string DisplayName { get; set; }
            public string Version { get; set; }
            public string Publisher { get; set; }
            public DateTime InstallDate { get; set; }
            public string UninstallString { get; set; }
            public string QuietUninstallString { get; set; }
            public string InstallPath { get; set; }
            public string EstimatedSize { get; set; }
            public bool Is64Bit { get; set; }

            public override string ToString()
            {
                return $"{DisplayName} {Version}";
            }
        }
    }
}