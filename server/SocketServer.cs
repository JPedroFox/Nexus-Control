using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace RemoteServer
{
    /// <summary>
    /// Manages the TcpListener and spawns one thread per connected client.
    ///
    /// PIN-based session security:
    ///  • A 6-digit PIN is randomly generated when the server starts.
    ///  • The PIN is displayed in the TrayManager info window.
    ///  • The first message from every new device must be:
    ///      {"cmd":"AUTH","pin":"123456"}
    ///  • If the PIN matches, the device IP is added to the in-memory whitelist
    ///    and will not need to authenticate again while the server is running.
    ///  • If the PIN is wrong, the connection is closed immediately.
    ///  • After each successful authorization the PIN rotates, so the next new
    ///    device must use a different code.
    ///  • When the server stops, the whitelist and PIN are discarded entirely.
    /// </summary>
    public class SocketServer
    {
        private readonly int _port;
        private TcpListener? _listener;
        private bool _isRunning = false;

        // Current PIN — rotates after every newly authorized device
        public string SessionPin { get; private set; } = GeneratePin();

        // IPs authorized in this session — never written to disk
        private readonly ConcurrentHashSet _authorizedIps = new ConcurrentHashSet();

        public event Action<string>? OnStatusChanged;

        /// <summary>
        /// Fired when the PIN rotates after a new device is authorized.
        /// The argument is the newly generated PIN.
        /// </summary>
        public event Action<string>? OnPinChanged;

        public SocketServer(int port)
        {
            _port = port;
        }

        // ─── SERVER ───────────────────────────────────────────────────────────

        public void Start()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                OnStatusChanged?.Invoke($"Waiting for connection on port {_port}...");

                while (_isRunning)
                {
                    TcpClient client   = _listener.AcceptTcpClient();
                    string    clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

                    Thread clientThread = new Thread(() => HandleClient(client, clientIp))
                    {
                        IsBackground = true,
                        Name         = $"Client-{clientIp}"
                    };
                    clientThread.Start();
                }
            }
            catch (SocketException) when (!_isRunning)
            {
                // Normal shutdown via Stop()
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Server error: {ex.Message}");
            }
        }

        // ─── CLIENT ───────────────────────────────────────────────────────────

        private void HandleClient(TcpClient client, string clientIp)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader  reader = new StreamReader(stream, Encoding.UTF8))
            using (StreamWriter  writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                try
                {
                    // ── Initial handshake ─────────────────────────────────────
                    bool alreadyAuthorized = _authorizedIps.Contains(clientIp);

                    if (alreadyAuthorized)
                    {
                        // Known device — accept immediately
                        writer.WriteLine("{\"status\":\"CONNECTED\",\"server\":\"Nexus Control v1.0\",\"auth\":\"OK\"}");
                        OnStatusChanged?.Invoke($"Device reconnected: {clientIp}");
                    }
                    else
                    {
                        // New device — request PIN
                        writer.WriteLine("{\"status\":\"CONNECTED\",\"server\":\"Nexus Control v1.0\",\"auth\":\"PIN_REQUIRED\"}");
                        OnStatusChanged?.Invoke($"New device waiting for PIN: {clientIp}");

                        // Read the authentication response (first line)
                        string? authLine = reader.ReadLine();
                        if (!ValidatePin(authLine, clientIp, writer))
                            return; // connection closed inside ValidatePin
                    }

                    // ── Command loop ──────────────────────────────────────────
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        JObject? json = TryParseJson(line);
                        if (json == null)
                        {
                            writer.WriteLine("{\"status\":\"ERROR\",\"msg\":\"Invalid JSON\"}");
                            continue;
                        }

                        string response = CommandExecutor.Execute(json);
                        writer.WriteLine(response);
                    }
                }
                catch (IOException)
                {
                    // Client disconnected — expected behavior
                }
                catch (Exception ex)
                {
                    OnStatusChanged?.Invoke($"Error with client {clientIp}: {ex.Message}");
                }
                finally
                {
                    OnStatusChanged?.Invoke($"Device disconnected: {clientIp}");
                }
            }
        }

        /// <summary>
        /// Validates the authentication JSON sent by the app.
        /// Expected format: {"cmd":"AUTH","pin":"123456"}
        /// Returns true if authenticated, false if rejected (connection already closed).
        /// </summary>
        private bool ValidatePin(string? authLine, string clientIp, StreamWriter writer)
        {
            if (authLine != null)
            {
                JObject? authJson = TryParseJson(authLine);
                if (authJson != null
                    && authJson["cmd"]?.ToString()?.ToUpper() == "AUTH"
                    && authJson["pin"]?.ToString() == SessionPin)
                {
                    // Correct PIN — add to whitelist and confirm
                    _authorizedIps.Add(clientIp);
                    writer.WriteLine("{\"status\":\"AUTH_OK\",\"msg\":\"Device authorized\"}");
                    OnStatusChanged?.Invoke($"✅ Device authorized: {clientIp}");

                    // Rotate PIN — next device will need a different code
                    SessionPin = GeneratePin();
                    OnPinChanged?.Invoke(SessionPin);
                    OnStatusChanged?.Invoke("🔑 New PIN generated for the next device");

                    return true;
                }
            }

            // Wrong PIN or invalid message — reject and close
            writer.WriteLine("{\"status\":\"AUTH_FAIL\",\"msg\":\"Invalid PIN. Connection closed.\"}");
            OnStatusChanged?.Invoke($"❌ Authentication failed: {clientIp}");
            return false;
        }

        // ─── CONTROL ──────────────────────────────────────────────────────────

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            // Whitelist discarded with the object — nothing written to disk
        }

        // ─── HELPERS ──────────────────────────────────────────────────────────

        private static string GeneratePin()
        {
            // Random.Shared is sufficient for a local network PIN
            return Random.Shared.Next(100_000, 999_999).ToString();
        }

        private static JObject? TryParseJson(string raw)
        {
            try { return JObject.Parse(raw); }
            catch { return null; }
        }
    }

    // ─── THREAD-SAFE SET ──────────────────────────────────────────────────────

    /// <summary>
    /// Minimal thread-safe HashSet for the IP whitelist.
    /// Backed by ConcurrentDictionary — the idiomatic C# pattern.
    /// </summary>
    internal sealed class ConcurrentHashSet
    {
        private readonly ConcurrentDictionary<string, byte> _dict = new();

        public bool Contains(string value) => _dict.ContainsKey(value);
        public void Add(string value)      => _dict.TryAdd(value, 0);
    }
}
