using System;
using System.IO;

namespace UninstallTool.Modules
{
    public class FileCleaner
    {
        private const string AppName = "MyApplication";

        public void CleanAll()
        {
            CleanTempFiles();
            CleanAppData();
            CleanLocalAppData();
            CleanCommonAppData();
        }

        public void CleanTempFiles()
        {
            var appTempDir = Path.Combine(Path.GetTempPath(), AppName);
            DeleteDirectoryIfExists(appTempDir);
        }

        public void CleanAppData()
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppName);
            DeleteDirectoryIfExists(appDataDir);
        }

        public void CleanLocalAppData()
        {
            var localAppDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppName);
            DeleteDirectoryIfExists(localAppDataDir);
        }

        public void CleanCommonAppData()
        {
            var commonAppDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                AppName);
            DeleteDirectoryIfExists(commonAppDataDir);
        }

        private void DeleteDirectoryIfExists(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }
    }
}