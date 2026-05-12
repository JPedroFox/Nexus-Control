using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace RemoteServer
{
    /// <summary>
    /// Gerencia o TcpListener e spawna uma thread por cliente conectado.
    /// </summary>
    public class SocketServer
    {
        private readonly int _port;
        private TcpListener? _listener;
        private bool _isRunning = false;

        public event Action<string>? OnStatusChanged;

        public SocketServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                OnStatusChanged?.Invoke($"Aguardando conexão na porta {_port}...");

                while (_isRunning)
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    string clientIp  = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

                    OnStatusChanged?.Invoke($"Celular conectado: {clientIp}");

                    Thread clientThread = new Thread(() => HandleClient(client))
                    {
                        IsBackground = true,
                        Name         = $"Client-{clientIp}"
                    };
                    clientThread.Start();
                }
            }
            catch (SocketException ex) when (!_isRunning)
            {
                _ = ex;
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Erro no servidor: {ex.Message}");
            }
        }

        private void HandleClient(TcpClient client)
        {
            string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader  reader = new StreamReader(stream, Encoding.UTF8))
            using (StreamWriter  writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                try
                {
                    // Handshake: identifica o servidor para o app Android
                    writer.WriteLine("{\"status\":\"CONNECTED\",\"server\":\"Nexus Control v1.0\"}");

                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        JObject? json = TryParseJson(line);
                        if (json == null)
                        {
                            writer.WriteLine("{\"status\":\"ERROR\",\"msg\":\"JSON inválido\"}");
                            continue;
                        }

                        string response = CommandExecutor.Execute(json);
                        writer.WriteLine(response);
                    }
                }
                catch (IOException)
                {
                    // Cliente desconectou — comportamento normal
                }
                catch (Exception ex)
                {
                    OnStatusChanged?.Invoke($"Erro com cliente {clientIp}: {ex.Message}");
                }
                finally
                {
                    OnStatusChanged?.Invoke($"Celular desconectado: {clientIp}");
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }

        private static JObject? TryParseJson(string raw)
        {
            try { return JObject.Parse(raw); }
            catch { return null; }
        }
    }
}