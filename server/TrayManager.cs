using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace RemoteServer
{
    /// <summary>
    /// Gerencia o ícone na bandeja do sistema (System Tray / Notification Area).
    /// Mantém o app vivo sem janela visível e exibe o IP para o usuário apontar no celular.
    /// </summary>
    public class TrayManager : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _menu;
        private readonly SocketServer _server;
        private readonly ToolStripMenuItem _statusItem;

        public TrayManager(SocketServer server)
        {
            _server = server;

            // Escuta eventos de status do servidor para atualizar o tooltip
            _server.OnStatusChanged += UpdateStatus;

            // ── Menu de contexto (clique direito no ícone) ──────────────────
            _statusItem = new ToolStripMenuItem("Iniciando...") { Enabled = false };

            ToolStripMenuItem ipItem = new ToolStripMenuItem($"IP: {GetLocalIpAddress()}") { Enabled = false };

            ToolStripMenuItem portItem = new ToolStripMenuItem($"Porta: {Program.SERVER_PORT}") { Enabled = false };

            ToolStripMenuItem copyIpItem = new ToolStripMenuItem("Copiar IP");
            copyIpItem.Click += (s, e) => Clipboard.SetText(GetLocalIpAddress());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Encerrar servidor");
            exitItem.Click += OnExit;

            _menu = new ContextMenuStrip();
            _menu.Items.Add(_statusItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(ipItem);
            _menu.Items.Add(portItem);
            _menu.Items.Add(copyIpItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(exitItem);

            // ── Ícone na bandeja ─────────────────────────────────────────────
            _trayIcon = new NotifyIcon
            {
                Icon             = CreateDefaultIcon(),
                Text             = $"RemoteServer — Porta {Program.SERVER_PORT}",
                ContextMenuStrip = _menu,
                Visible          = true
            };

            // Clique duplo → mostra o IP em uma mensagem rápida
            _trayIcon.DoubleClick += (s, e) =>
            {
                MessageBox.Show(
                    $"Servidor ativo!\n\nIP:    {GetLocalIpAddress()}\nPorta: {Program.SERVER_PORT}\n\n" +
                    "Use esses dados no app Android para conectar.",
                    "RemoteServer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            };

            // ✅ _trayIcon já está pronto — agora é seguro subir a thread do servidor
            // Se subir antes, OnStatusChanged dispara com _trayIcon ainda null (NullReferenceException)
            Thread serverThread = new Thread(_server.Start)
            {
                IsBackground = true,
                Name = "SocketServerThread"
            };
            serverThread.Start();

            UpdateStatus($"Aguardando conexão na porta {Program.SERVER_PORT}...");
        }

        // ─── EVENTOS ──────────────────────────────────────────────────────────

        private void UpdateStatus(string message)
        {
            // Eventos podem vir de outra thread — Invoke garante thread-safety
            if (_menu.InvokeRequired)
            {
                _menu.Invoke(() => UpdateStatus(message));
                return;
            }

            _statusItem.Text = message;
            _trayIcon.Text   = $"RemoteServer — {message}";

            // Balão de notificação para eventos importantes
            if (message.Contains("conectado") || message.Contains("desconectado"))
            {
                _trayIcon.ShowBalloonTip(
                    timeout: 3000,
                    tipTitle: "RemoteServer",
                    tipText: message,
                    tipIcon: ToolTipIcon.Info
                );
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            _server.Stop();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Application.Exit();
        }

        // ─── HELPERS ──────────────────────────────────────────────────────────

        /// <summary>
        /// Pega o IP local da máquina na rede Wi-Fi/LAN.
        /// Esse é o IP que o celular vai usar para conectar.
        /// </summary>
        private static string GetLocalIpAddress()
        {
            try
            {
                using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530); // Não faz conexão real — só resolve a interface
                return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
            }
            catch
            {
                return "IP não encontrado";
            }
        }

        /// <summary>
        /// Gera um ícone simples em código para não depender de arquivo .ico externo.
        /// Substitua por Icon.ExtractAssociatedIcon() ou um .ico real se quiser.
        /// </summary>
        private static Icon CreateDefaultIcon()
        {
            using Bitmap bmp = new Bitmap(16, 16);
            using Graphics g = Graphics.FromImage(bmp);

            g.Clear(Color.Transparent);
            g.FillEllipse(Brushes.DodgerBlue, 1, 1, 14, 14);
            g.DrawString("R", new Font("Arial", 7, FontStyle.Bold), Brushes.White, 3, 2);

            return Icon.FromHandle(bmp.GetHicon());
        }
    }
}