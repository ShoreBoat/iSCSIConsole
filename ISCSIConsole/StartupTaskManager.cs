using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace ISCSIConsole
{
    public static class StartupTaskManager
    {
        private const string TaskName = "iSCSIConsole AutoStart";

        public static bool EnsureInstalled(out string error)
        {
            error = String.Empty;
            if (!RuntimeHelper.IsWin32)
            {
                return true;
            }

            string exePath = Assembly.GetEntryAssembly().Location;
            string taskCommand = String.Format("\"{0}\" /autostart", exePath);
            string arguments = String.Format(
                "/Create /F /TN \"{0}\" /SC ONLOGON /RL HIGHEST /TR \"\\\"{1}\\\" /autostart\"",
                TaskName,
                exePath);

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
                startInfo.Arguments = arguments;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                using (Process process = Process.Start(startInfo))
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        error = String.IsNullOrEmpty(stderr) ? stdout : stderr;
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
