using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
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

            ToolStripMenuItem ipItem   = new ToolStripMenuItem($"IP: {GetLocalIpAddress()}") { Enabled = false };
            ToolStripMenuItem portItem = new ToolStripMenuItem($"Porta: {Program.SERVER_PORT}") { Enabled = false };

            ToolStripMenuItem copyIpItem = new ToolStripMenuItem("Copiar IP");
            copyIpItem.Click += (s, e) => Clipboard.SetText(GetLocalIpAddress());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Encerrar Nexus Control");
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
                Icon             = CreateNexusIcon(),
                Text             = $"Nexus Control — Porta {Program.SERVER_PORT}",
                ContextMenuStrip = _menu,
                Visible          = true
            };

            // Clique duplo → mostra o IP em uma mensagem rápida
            _trayIcon.DoubleClick += (s, e) =>
            {
                MessageBox.Show(
                    $"Nexus Control ativo!\n\nIP:    {GetLocalIpAddress()}\nPorta: {Program.SERVER_PORT}\n\n" +
                    "Use esses dados no app Android para conectar.",
                    "Nexus Control",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            };

            // ✅ _trayIcon já está pronto — agora é seguro subir a thread do servidor
            Thread serverThread = new Thread(_server.Start)
            {
                IsBackground = true,
                Name         = "SocketServerThread"
            };
            serverThread.Start();

            UpdateStatus($"Aguardando conexão na porta {Program.SERVER_PORT}...");
        }

        // ─── EVENTOS ──────────────────────────────────────────────────────────

        private void UpdateStatus(string message)
        {
            if (_menu.InvokeRequired)
            {
                _menu.Invoke(() => UpdateStatus(message));
                return;
            }

            _statusItem.Text = message;
            _trayIcon.Text   = $"Nexus Control — {message}";

            if (message.Contains("conectado") || message.Contains("desconectado"))
            {
                _trayIcon.ShowBalloonTip(
                    timeout:  3000,
                    tipTitle: "Nexus Control",
                    tipText:  message,
                    tipIcon:  ToolTipIcon.Info
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

        private static string GetLocalIpAddress()
        {
            try
            {
                using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
            }
            catch
            {
                return "IP não encontrado";
            }
        }

        // ─── ÍCONE ────────────────────────────────────────────────────────────

        /// <summary>
        /// Gera o ícone do Nexus Control programaticamente em 32×32px.
        ///
        /// Design:
        ///  • Fundo escuro (#0D0D1A) com cantos arredondados
        ///  • Borda fina em cyan translúcido
        ///  • "N" bold centralizado em cyan (#00E5FF)
        ///  • 4 nós de conexão nos cantos (identidade visual "nexus")
        ///
        /// Por que programático e não um .ico externo?
        ///  Num publish single-file os recursos embutidos via ApplicationIcon
        ///  exigem configuração extra de EmbeddedResource. Gerar em código
        ///  garante que funciona sem nenhum arquivo adicional.
        /// </summary>
        private static Icon CreateNexusIcon()
        {
            const int SIZE = 32;

            using Bitmap  bmp = new Bitmap(SIZE, SIZE);
            using Graphics g  = Graphics.FromImage(bmp);

            g.SmoothingMode      = SmoothingMode.AntiAlias;
            g.TextRenderingHint  = TextRenderingHint.AntiAlias;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            // ── Paleta ────────────────────────────────────────────────────────
            Color bgColor     = Color.FromArgb(255,  13,  13,  26); // #0D0D1A
            Color cyanColor   = Color.FromArgb(255,   0, 229, 255); // #00E5FF
            Color cyanDim     = Color.FromArgb( 80,   0, 229, 255); // #00E5FF @ 31%
            Color cyanGlow    = Color.FromArgb( 40,   0, 229, 255); // #00E5FF @ 16%

            var bounds = new Rectangle(0, 0, SIZE, SIZE);

            // ── Fundo com cantos arredondados ─────────────────────────────────
            using GraphicsPath bgPath = RoundedRect(bounds, radius: 5);
            using SolidBrush bgBrush  = new SolidBrush(bgColor);
            g.FillPath(bgBrush, bgPath);

            // ── Borda cyan fina ───────────────────────────────────────────────
            using Pen borderPen = new Pen(cyanDim, 1f);
            g.DrawPath(borderPen, bgPath);

            // ── "N" centralizado ──────────────────────────────────────────────
            using Font font        = new Font("Arial", 17, FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush cBrush = new SolidBrush(cyanColor);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            // Leve sombra/glow atrás da letra
            using SolidBrush glowBrush = new SolidBrush(cyanGlow);
            g.DrawString("N", font, glowBrush, new RectangleF(-1, -1, SIZE + 2, SIZE + 2), sf);
            g.DrawString("N", font, glowBrush, new RectangleF( 1,  1, SIZE + 2, SIZE + 2), sf);
            g.DrawString("N", font, cBrush,    new RectangleF(0, 0, SIZE, SIZE), sf);

            // ── Nós de conexão nos cantos (identidade "nexus") ────────────────
            const int NODE_R = 2;
            const int MARGIN = 4;
            using SolidBrush nodeBrush = new SolidBrush(cyanColor);

            DrawNode(g, nodeBrush, MARGIN,        MARGIN,        NODE_R);
            DrawNode(g, nodeBrush, SIZE - MARGIN,  MARGIN,        NODE_R);
            DrawNode(g, nodeBrush, MARGIN,         SIZE - MARGIN, NODE_R);
            DrawNode(g, nodeBrush, SIZE - MARGIN,  SIZE - MARGIN, NODE_R);

            return Icon.FromHandle(bmp.GetHicon());
        }

        private static void DrawNode(Graphics g, Brush brush, int cx, int cy, int r)
            => g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);

        /// <summary>Cria um GraphicsPath de retângulo com cantos arredondados.</summary>
        private static GraphicsPath RoundedRect(Rectangle b, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(b.X,              b.Y,               d, d, 180, 90);
            path.AddArc(b.Right - d,      b.Y,               d, d, 270, 90);
            path.AddArc(b.Right - d,      b.Bottom - d,      d, d,   0, 90);
            path.AddArc(b.X,              b.Bottom - d,      d, d,  90, 90);
            path.CloseFigure();
            return path;
        }
    }
}