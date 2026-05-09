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

        // Evento para notificar a TrayIcon sobre mudanças de estado
        public event Action<string>? OnStatusChanged;

        public SocketServer(int port)
        {
            _port = port;
        }

        /// <summary>
        /// Inicia o loop de escuta. Deve ser chamado numa thread separada.
        /// </summary>
        public void Start()
        {
            try
            {
                // Escuta em todas as interfaces de rede (Wi-Fi, Ethernet, etc.)
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                OnStatusChanged?.Invoke($"Aguardando conexão na porta {_port}...");

                while (_isRunning)
                {
                    // Bloqueia aqui até um cliente conectar
                    TcpClient client = _listener.AcceptTcpClient();
                    string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

                    OnStatusChanged?.Invoke($"Celular conectado: {clientIp}");

                    // Cada cliente roda na própria thread para não bloquear novas conexões
                    Thread clientThread = new Thread(() => HandleClient(client))
                    {
                        IsBackground = true,
                        Name = $"Client-{clientIp}"
                    };
                    clientThread.Start();
                }
            }
            catch (SocketException ex) when (!_isRunning)
            {
                // Exceção esperada quando Stop() é chamado — ignora
                _ = ex;
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Erro no servidor: {ex.Message}");
            }
        }

        /// <summary>
        /// Loop de leitura de mensagens para um cliente específico.
        /// Protocolo: cada mensagem é uma linha JSON terminada em '\n'
        /// </summary>
        private void HandleClient(TcpClient client)
        {
            string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                try
                {
                    // Handshake: confirma conexão para o celular
                    writer.WriteLine("{\"status\":\"CONNECTED\",\"server\":\"RemoteServer v1.0\"}");

                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Parse do JSON recebido
                        JObject? json = TryParseJson(line);
                        if (json == null)
                        {
                            writer.WriteLine("{\"status\":\"ERROR\",\"msg\":\"JSON inválido\"}");
                            continue;
                        }

                        // Delega a execução do comando e pega a resposta
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

        /// <summary>
        /// Para o servidor com segurança.
        /// </summary>
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
