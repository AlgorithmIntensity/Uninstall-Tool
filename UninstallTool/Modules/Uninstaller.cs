using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace UninstallTool.Modules
{
    public class Uninstaller
    {
        private const string AppName = "MyApplication";
        private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName;

        public void Execute()
        {
            StopProcesses();
            RemoveShortcuts();
            RemoveRegistryEntries();
            DeleteInstallationDirectory();
            RemoveFromProgramsList();
        }

        private void StopProcesses()
        {
            var processes = Process.GetProcessesByName(AppName);
            foreach (var process in processes)
            {
                process.Kill();
                process.WaitForExit(5000);
            }
        }

        private void RemoveShortcuts()
        {
            var startMenuPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs",
                AppName);

            var desktopPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"{AppName}.lnk");

            if (Directory.Exists(startMenuPath))
                Directory.Delete(startMenuPath, true);

            if (File.Exists(desktopPath))
                File.Delete(desktopPath);
        }

        private void RemoveRegistryEntries()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(RegistryKey, true))
            {
                key?.DeleteValue("DisplayName", false);
                key?.DeleteValue("UninstallString", false);
                key?.DeleteValue("InstallLocation", false);
            }
            Registry.LocalMachine.DeleteSubKeyTree(RegistryKey, false);
        }

        private void DeleteInstallationDirectory()
        {
            var installDir = GetInstallDirectory();
            if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
            {
                Directory.Delete(installDir, true);
            }
        }

        private void RemoveFromProgramsList()
        {
            var uninstallKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                true);
            uninstallKey?.DeleteSubKeyTree(AppName, false);
        }

        private string GetInstallDirectory()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(RegistryKey))
            {
                return key?.GetValue("InstallLocation") as string;
            }
        }
    }
}