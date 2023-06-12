using WebMessage.Device;
using WebSocketSharp;

namespace WebMessage.Client
{
    public class SocketConnection : ISocketConnection
    {
        private WebSocket? _socket;

        #region ISocketConnection

        public string Url { get; protected set; } = string.Empty;

        public bool IsAlive => _socket?.IsAlive is true;

        public event EventHandler? OnDisconnected;

        public event EventHandler<SocketMessageEventArgs>? OnMessage;

        public void Connect(IDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            Connect($"wss://{device.HostName ?? device.IPAddress}:{device.Port + 1}", $"ws://{device.HostName ?? device.IPAddress}:{device.Port}");
        }

        public void Connect(string url)
        {
            ArgumentNullException.ThrowIfNull(url);
            Connect(url, null); // no fallback
        }

        public void Send(string content)
        {
            _socket?.Send(content);
        }

        public void Close()
        {
            _socket?.Close();
            _socket = null;
        }

        #endregion

        protected void Connect(string url, string? fallbackUrl)
        {
            var socket = new WebSocket(url);
            socket.Log.Level = LogLevel.Debug;

            socket.OnMessage += (s, e) => OnMessage?.Invoke(this, new SocketMessageEventArgs(e.Data));
            socket.OnClose += (s, e) =>
            {
                if (string.IsNullOrEmpty(fallbackUrl))
                {
                    OnDisconnected?.Invoke(this, EventArgs.Empty);
                    return;
                }
                Connect(fallbackUrl, null);
            };
            socket.OnError += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            };
            if (socket.IsSecure)
            {
                socket.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;
            }
            socket.Connect();

            if (socket.ReadyState is WebSocketState.Connecting or WebSocketState.Open)
            {
                _socket = socket;

                // Make sure OnDisconnected is called in the OnClose event
                fallbackUrl = null;
            }
        }

    }
}
