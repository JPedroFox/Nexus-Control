using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RemoteServer
{
    internal static class Program
    {
        public const int SERVER_PORT = 8888;

        // Must be called BEFORE any access to Screen, Graphics or Forms.
        // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
        // Without this, even with app.manifest, the .NET runtime may have already
        // initialized the process in virtualized mode before the manifest is read.
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
            = new IntPtr(-4);

        [STAThread]
        static void Main()
        {
            // ⚠️ First line of the program — before any WinForms initialization
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                File.WriteAllText("crash.log", e.ExceptionObject.ToString());
                MessageBox.Show(
                    e.ExceptionObject.ToString(),
                    "Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            };

            try
            {
                ApplicationConfiguration.Initialize();

                SocketServer socketServer = new SocketServer(SERVER_PORT);
                Application.Run(new TrayManager(socketServer));
            }
            catch (Exception ex)
            {
                File.WriteAllText("crash.log", ex.ToString());
                MessageBox.Show(
                    ex.ToString(),
                    "Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}
