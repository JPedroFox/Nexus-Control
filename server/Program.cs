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

        // Deve ser chamado ANTES de qualquer acesso a Screen, Graphics ou Forms.
        // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
        // Sem isso, mesmo com app.manifest, o .NET runtime pode já ter inicializado
        // o processo em modo virtualizado antes do manifesto ser lido.
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
            = new IntPtr(-4);

        [STAThread]
        static void Main()
        {
            // ⚠️ Primeira linha do programa — antes de qualquer WinForms init
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                File.WriteAllText("crash.log", e.ExceptionObject.ToString());
                MessageBox.Show(
                    e.ExceptionObject.ToString(),
                    "Erro ao iniciar",
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
                    "Erro ao iniciar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}