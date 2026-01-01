using System;
using System.Diagnostics;

// i don't wanna use System.ServiceProcess

namespace UninstallTool.Modules
{
    public class ServiceRemover
    {
        public void RemoveService(string serviceName)
        {
            try
            {
                StopService(serviceName);
                DeleteService(serviceName);
            }
            catch
            {
            }
        }

        private void StopService(string serviceName)
        {
            var stopProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "sc",
                Arguments = $"stop \"{serviceName}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            stopProcess?.WaitForExit(10000);
        }

        private void DeleteService(string serviceName)
        {
            var deleteProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "sc",
                Arguments = $"delete \"{serviceName}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            deleteProcess?.WaitForExit(10000);
        }

        public bool ServiceExists(string serviceName)
        {
            try
            {
                var queryProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments = $"query \"{serviceName}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                queryProcess.Start();
                queryProcess.WaitForExit(5000);

                return queryProcess.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public string GetServiceStatus(string serviceName)
        {
            try
            {
                var queryProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments = $"query \"{serviceName}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                queryProcess.Start();
                var output = queryProcess.StandardOutput.ReadToEnd();
                queryProcess.WaitForExit(5000);

                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}