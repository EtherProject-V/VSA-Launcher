using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Fleck;
using Newtonsoft.Json;

namespace VSA_launcher.WebSocket
{
    public class WebSocketServerManager
    {
        private WebSocketServer? _server;
        private readonly List<IWebSocketConnection> _clients = new List<IWebSocketConnection>();
        private readonly object _lock = new object();

        public int Port { get; private set; }
        public bool IsRunning { get; private set; }

        public event EventHandler<string>? MessageReceived;
        public event EventHandler<bool>? ClientConnectionChanged;

        public void Start(int startPort = 28766, int maxAttempts = 10)
        {
            if (IsRunning)
            {
                Console.WriteLine("WebSocketサーバーは既に起動しています");
                return;
            }

            Port = FindAvailablePort(startPort, maxAttempts);

            _server = new WebSocketServer($"ws://0.0.0.0:{Port}");

            _server.Start(socket =>
            {
                socket.OnOpen = () => OnClientConnected(socket);
                socket.OnClose = () => OnClientDisconnected(socket);
                socket.OnMessage = message => OnMessageReceived(message);
                socket.OnError = exception => Console.WriteLine($"WebSocketエラー: {exception.Message}");
            });

            IsRunning = true;
            Console.WriteLine($"WebSocketサーバー起動: ws://localhost:{Port}");
        }

        public void Stop()
        {
            if (!IsRunning) return;

            _server?.Dispose();
            _server = null;

            lock (_lock)
            {
                _clients.Clear();
            }

            IsRunning = false;
            Console.WriteLine("WebSocketサーバー停止");
        }

        public void SendMessage(string message)
        {
            if (!IsRunning)
            {
                Console.WriteLine("WebSocketサーバーが起動していません");
                return;
            }

            lock (_lock)
            {
                foreach (var client in _clients.ToList())
                {
                    try
                    {
                        client.Send(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"メッセージ送信エラー: {ex.Message}");
                    }
                }
            }
        }

        private void OnClientConnected(IWebSocketConnection socket)
        {
            lock (_lock)
            {
                _clients.Add(socket);
            }

            Console.WriteLine($"クライアント接続: {socket.ConnectionInfo.ClientIpAddress}");
            ClientConnectionChanged?.Invoke(this, true);
        }

        private void OnClientDisconnected(IWebSocketConnection socket)
        {
            lock (_lock)
            {
                _clients.Remove(socket);
            }

            Console.WriteLine($"クライアント切断: {socket.ConnectionInfo.ClientIpAddress}");
            ClientConnectionChanged?.Invoke(this, _clients.Count > 0);
        }

        private void OnMessageReceived(string message)
        {
            Console.WriteLine($"メッセージ受信: {message}");
            MessageReceived?.Invoke(this, message);
        }

        private int FindAvailablePort(int startPort, int maxAttempts)
        {
            for (int port = startPort; port < startPort + maxAttempts; port++)
            {
                if (IsPortAvailable(port))
                {
                    return port;
                }
            }

            throw new Exception($"利用可能なポートが見つかりません（{startPort}～{startPort + maxAttempts - 1}）");
        }

        private bool IsPortAvailable(int port)
        {
            try
            {
                IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

                foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
                {
                    if (tcpi.LocalEndPoint.Port == port)
                    {
                        return false;
                    }
                }

                IPEndPoint[] tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
                foreach (IPEndPoint endpoint in tcpListeners)
                {
                    if (endpoint.Port == port)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
