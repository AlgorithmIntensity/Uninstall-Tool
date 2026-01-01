using Microsoft.Win32;

namespace UninstallTool.Modules
{
    public class RegistryCleaner
    {
        private const string AppName = "MyApplication";

        public void CleanAll()
        {
            CleanUserRegistry();
            CleanMachineRegistry();
            CleanClassesRoot();
        }

        public void CleanUserRegistry()
        {
            CleanRegistry(Registry.CurrentUser, @"Software\" + AppName);
            RemoveRunEntry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
        }

        public void CleanMachineRegistry()
        {
            CleanRegistry(Registry.LocalMachine, @"SOFTWARE\" + AppName);
            RemoveRunEntry(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
        }

        public void CleanClassesRoot()
        {
            CleanRegistry(Registry.ClassesRoot, AppName);
        }

        private void CleanRegistry(RegistryKey rootKey, string subKeyPath)
        {
            try
            {
                rootKey.DeleteSubKeyTree(subKeyPath, false);
            }
            catch
            {
            }
        }

        private void RemoveRunEntry(RegistryKey rootKey, string runKeyPath)
        {
            using (var runKey = rootKey.OpenSubKey(runKeyPath, true))
            {
                if (runKey != null && runKey.GetValue(AppName) != null)
                {
                    runKey.DeleteValue(AppName, false);
                }
            }
        }
    }
}