using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace VeloxDNS_Complete
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (!IsAdministrator())
            {
                RestartAsAdmin();
                Shutdown();
                return;
            }

            base.OnStartup(e);
            new MainWindow().Show();
        }

        private bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void RestartAsAdmin()
        {
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            var startInfo = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            try
            {
                Process.Start(startInfo);
            }
            catch { }
        }
    }
}