using System.Diagnostics;

namespace UninstallTool.Modules
{
    public class ProcessManager
    {
        public void TerminateProcess(string processName)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                }
                catch
                {
                }
            }
        }

        public void TerminateProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch
            {
            }
        }

        public bool IsProcessRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }

        public void WaitForProcessExit(string processName, int timeoutMs)
        {
            var processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                process.WaitForExit(timeoutMs);
            }
        }
    }
}