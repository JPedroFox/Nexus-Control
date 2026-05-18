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
    /// Manages the system tray icon (Notification Area).
    /// On startup, displays a window with the IP, port and session PIN.
    /// The PIN is also accessible via the tray icon context menu.
    /// </summary>
    public class TrayManager : ApplicationContext
    {
        private readonly NotifyIcon        _trayIcon;
        private readonly ContextMenuStrip  _menu;
        private readonly SocketServer      _server;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _pinItem;

        // Reference to the PIN label inside the currently open info dialog (null when closed)
        private Label? _infoPinLabel;

        public TrayManager(SocketServer server)
        {
            _server = server;
            _server.OnStatusChanged += UpdateStatus;
            _server.OnPinChanged    += UpdatePin;

            string ip  = GetLocalIpAddress();
            string pin = _server.SessionPin;

            // ── Context menu ──────────────────────────────────────────────────
            _statusItem = new ToolStripMenuItem("Starting...") { Enabled = false };
            _pinItem    = new ToolStripMenuItem($"PIN: {pin}")
            {
                Enabled = false,
                Font    = new Font("Consolas", 11f, FontStyle.Bold)
            };

            ToolStripMenuItem ipItem   = new ToolStripMenuItem($"IP: {ip}")              { Enabled = false };
            ToolStripMenuItem portItem = new ToolStripMenuItem($"Port: {Program.SERVER_PORT}") { Enabled = false };

            ToolStripMenuItem copyIpItem  = new ToolStripMenuItem("Copy IP");
            copyIpItem.Click += (s, e) => Clipboard.SetText(ip);

            ToolStripMenuItem copyPinItem = new ToolStripMenuItem("Copy PIN");
            copyPinItem.Click += (s, e) => Clipboard.SetText(_server.SessionPin);

            ToolStripMenuItem showInfoItem = new ToolStripMenuItem("Show IP / PIN...");
            showInfoItem.Click += (s, e) => ShowInfoDialog();

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit Nexus Control");
            exitItem.Click += OnExit;

            _menu = new ContextMenuStrip();
            _menu.Items.Add(_statusItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(ipItem);
            _menu.Items.Add(portItem);
            _menu.Items.Add(_pinItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(copyIpItem);
            _menu.Items.Add(copyPinItem);
            _menu.Items.Add(showInfoItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(exitItem);

            // ── Tray icon ─────────────────────────────────────────────────────
            _trayIcon = new NotifyIcon
            {
                Icon             = CreateNexusIcon(),
                Text             = $"Nexus Control — Port {Program.SERVER_PORT}",
                ContextMenuStrip = _menu,
                Visible          = true
            };

            // Double-click → open info window
            _trayIcon.DoubleClick += (s, e) => ShowInfoDialog();

            // ✅ Icon ready — start the server thread
            Thread serverThread = new Thread(_server.Start)
            {
                IsBackground = true,
                Name         = "SocketServerThread"
            };
            serverThread.Start();

            UpdateStatus($"Waiting for connection on port {Program.SERVER_PORT}...");

            // Show info window on startup
            ShowInfoDialog();
        }

        // ─── INFO WINDOW ──────────────────────────────────────────────────────

        /// <summary>
        /// Styled window showing IP, port and PIN so the user can configure the app.
        /// </summary>
        private void ShowInfoDialog()
        {
            string ip  = GetLocalIpAddress();
            string pin = _server.SessionPin;

            using Form dlg = new Form
            {
                Text            = "Nexus Control — Connection Info",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition   = FormStartPosition.CenterScreen,
                MaximizeBox     = false,
                MinimizeBox     = false,
                BackColor       = Color.FromArgb(13, 13, 26),
                ForeColor       = Color.White,
                Width           = 360,
                Height          = 340,
                ShowInTaskbar   = true,
            };

            Label lblTitle = new Label
            {
                Text      = "NEXUS CONTROL",
                Font      = new Font("Consolas", 16f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 229, 255),
                AutoSize  = false,
                Width     = 320,
                Height    = 30,
                Left      = 20,
                Top       = 18,
                TextAlign = ContentAlignment.MiddleCenter,
            };

            Label lblSub = new Label
            {
                Text      = "Enter the details below in the Android app",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(150, 170, 190),
                AutoSize  = false,
                Width     = 320,
                Height    = 18,
                Left      = 20,
                Top       = 50,
                TextAlign = ContentAlignment.MiddleCenter,
            };

            Panel sep = new Panel
            {
                BackColor = Color.FromArgb(0, 229, 255, 40),
                Left = 20, Top = 74, Width = 320, Height = 1,
            };

            Control[] rows = BuildInfoRows(90, ip, pin, out int rowY);

            Button btnOk = new Button
            {
                Text         = "OK",
                DialogResult = DialogResult.OK,
                Left         = 130,
                Top          = rowY + 12,
                Width        = 100,
                Height       = 34,
                BackColor    = Color.FromArgb(0, 229, 255),
                ForeColor    = Color.Black,
                FlatStyle    = FlatStyle.Flat,
                Font         = new Font("Consolas", 10f, FontStyle.Bold),
            };
            btnOk.FlatAppearance.BorderSize = 0;

            dlg.Controls.Add(lblTitle);
            dlg.Controls.Add(lblSub);
            dlg.Controls.Add(sep);
            foreach (Control c in rows) dlg.Controls.Add(c);
            dlg.Controls.Add(btnOk);
            dlg.AcceptButton = btnOk;
            dlg.FormClosed += (_, _) => _infoPinLabel = null;

            dlg.ShowDialog();
        }

        /// <summary>Builds the IP / Port / PIN label rows inside the info window.</summary>
        private Control[] BuildInfoRows(int startY, string ip, string pin, out int finalY)
        {
            var controls = new System.Collections.Generic.List<Control>();
            int y = startY;

            // Static local function — captures nothing from the outer scope
            static (Label lbl, Label val) MakeRow(string label, string value, Color accent, int top)
            {
                return (
                    new Label
                    {
                        Text      = label,
                        Font      = new Font("Segoe UI", 9f),
                        ForeColor = Color.FromArgb(130, 150, 170),
                        Left = 24, Top = top, Width = 60, Height = 20,
                        TextAlign = ContentAlignment.MiddleLeft,
                    },
                    new Label
                    {
                        Text      = value,
                        Font      = new Font("Consolas", 13f, FontStyle.Bold),
                        ForeColor = accent,
                        Left = 90, Top = top - 2, Width = 220, Height = 26,
                        TextAlign = ContentAlignment.MiddleLeft,
                    }
                );
            }

            Color cyan   = Color.FromArgb(0, 229, 255);
            Color white  = Color.White;
            Color yellow = Color.FromArgb(255, 214, 0);

            var (l1, v1) = MakeRow("IP",   ip,                             white, y);
            controls.Add(l1); controls.Add(v1); y += 36;

            var (l2, v2) = MakeRow("Port", Program.SERVER_PORT.ToString(), cyan,  y);
            controls.Add(l2); controls.Add(v2); y += 36;

            // Separator before PIN
            controls.Add(new Panel
            {
                BackColor = Color.FromArgb(30, 255, 255, 255),
                Left = 20, Top = y, Width = 320, Height = 1,
            });
            y += 10;

            controls.Add(new Label
            {
                Text      = "Session PIN (expires when the server closes)",
                Font      = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(120, 140, 160),
                Left = 24, Top = y, Width = 310, Height = 16,
            });
            y += 18;

            var (l3, v3) = MakeRow("PIN", pin, yellow, y);
            controls.Add(l3); controls.Add(v3); y += 36;

            finalY = y;
            _infoPinLabel = v3;   // store reference so UpdatePin can reach it
            return controls.ToArray();
        }

        // ─── EVENTS ───────────────────────────────────────────────────────────

        private void UpdateStatus(string message)
        {
            if (_menu.InvokeRequired)
            {
                _menu.Invoke(() => UpdateStatus(message));
                return;
            }

            _statusItem.Text = message;
            _trayIcon.Text   = $"Nexus Control — {message}";

            if (message.Contains("connected")    || message.Contains("disconnected") ||
                message.Contains("authorized")   || message.Contains("failed"))
            {
                _trayIcon.ShowBalloonTip(
                    timeout:  3000,
                    tipTitle: "Nexus Control",
                    tipText:  message,
                    tipIcon:  ToolTipIcon.Info
                );
            }
        }

        /// <summary>
        /// Updates the PIN menu item and shows a balloon tip with the new code.
        /// Called from the server thread — marshals to the UI thread.
        /// </summary>
       private void UpdatePin(string newPin)
        {
            if (_menu.InvokeRequired)
            {
                _menu.Invoke(() => UpdatePin(newPin));
                return;
            }

            _pinItem.Text = $"PIN: {newPin}";

            // ← UPDATE AQUI: atualiza o label dentro da janela se ela estiver aberta
            if (_infoPinLabel != null && !_infoPinLabel.IsDisposed)
                _infoPinLabel.Text = newPin;

            _trayIcon.ShowBalloonTip(
                timeout:  5000,
                tipTitle: "Nexus Control — New PIN",
                tipText:  $"Next device must use PIN: {newPin}",
                tipIcon:  ToolTipIcon.Info
            );
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
                return "IP not found";
            }
        }

        // ─── ICON ─────────────────────────────────────────────────────────────

        private static Icon CreateNexusIcon()
        {
            const int SIZE = 32;

            using Bitmap  bmp = new Bitmap(SIZE, SIZE);
            using Graphics g  = Graphics.FromImage(bmp);

            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            Color bgColor   = Color.FromArgb(255,  13,  13,  26);
            Color cyanColor = Color.FromArgb(255,   0, 229, 255);
            Color cyanDim   = Color.FromArgb( 80,   0, 229, 255);
            Color cyanGlow  = Color.FromArgb( 40,   0, 229, 255);

            var bounds = new Rectangle(0, 0, SIZE, SIZE);

            using GraphicsPath bgPath = RoundedRect(bounds, radius: 5);
            using SolidBrush bgBrush  = new SolidBrush(bgColor);
            g.FillPath(bgBrush, bgPath);

            using Pen borderPen = new Pen(cyanDim, 1f);
            g.DrawPath(borderPen, bgPath);

            using Font font         = new Font("Arial", 17, FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush cBrush = new SolidBrush(cyanColor);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            using SolidBrush glowBrush = new SolidBrush(cyanGlow);
            g.DrawString("N", font, glowBrush, new RectangleF(-1, -1, SIZE + 2, SIZE + 2), sf);
            g.DrawString("N", font, glowBrush, new RectangleF( 1,  1, SIZE + 2, SIZE + 2), sf);
            g.DrawString("N", font, cBrush,    new RectangleF(0, 0, SIZE, SIZE), sf);

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

        private static GraphicsPath RoundedRect(Rectangle b, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(b.X,         b.Y,          d, d, 180, 90);
            path.AddArc(b.Right - d, b.Y,          d, d, 270, 90);
            path.AddArc(b.Right - d, b.Bottom - d, d, d,   0, 90);
            path.AddArc(b.X,         b.Bottom - d, d, d,  90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
