using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace com.lobger.WebSocketServer
{
    public class WebSocketServer : IWebSocketServer
    {
        /// <summary>
        /// CLient information
        /// </summary>
        protected class ClientInfo
        {
            public WebSocket WebSocket { get; }

            public String? Token { get; set; }

            public ClientInfo(WebSocket websocket)
            {
                WebSocket = websocket;
            }
        }



        private readonly HttpListener _httpListener;
        private readonly ConcurrentDictionary<Guid, ClientInfo> _clients;
        private readonly IRequestManager _requestManager;

        private CancellationTokenSource? _cancellationTokenSource;

        public WebSocketServer(string url) : this(new RequestManager(), url)
        {
        }

        internal WebSocketServer(IRequestManager requestManager, string url)
        {
            _httpListener = new();
            _httpListener.Prefixes.Add(url);

            _clients = new();
            _requestManager = requestManager ?? throw new ArgumentNullException(nameof(requestManager));
        }

        #region IWebSocketServer

        public event EventHandler<ClientEventArgs>? ClientConnected;

        public event EventHandler<ClientEventArgs>? ClientDisconnected;

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        public async Task Start()
        {
            if (_cancellationTokenSource is not null)
            {
                return;
            }

            _cancellationTokenSource = new();

            _httpListener.Start();

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = ProcessWebSocketRequest(context, _cancellationTokenSource.Token);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (HttpListenerException ex)
                {
                    System.Diagnostics.Debug.WriteLine($@"ERROR: {ex}");
                    break;
                }
            }

            _cancellationTokenSource = null;
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _httpListener.Stop();
        }

        public void RegisterRequest(string uri, Callback callback)
        {
            _requestManager.RegisterRequest(uri, callback);
        }

        public void RegisterRequest<TRequest>(string uri, Callback<TRequest> callback)
        {
            _requestManager.RegisterRequest(uri, callback);
        }

        public void UnregisterRequest(string uri)
        {
            _requestManager.UnregisterRequest(uri);
        }

        public Task SendMessage(Guid clientId, string message, CancellationToken cancellationToken)
        {
            if (_clients.TryGetValue(clientId, value: out var client))
            {
                return SendMessage(client, message, cancellationToken);
            }
            return Task.CompletedTask;
        }

        public Task BroadcastMessgaeAsync(string message, CancellationToken cancellationToken)
        {
            return Task.WhenAll(_clients.Values.Select(client => SendMessage(client, message, cancellationToken)));
        }

        #endregion

        protected Task SendMessage(ClientInfo client, string message, CancellationToken cancellationToken)
        {
            if (_cancellationTokenSource is { } cts)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                var packet = Encoding.UTF8.GetBytes(message);
                return client.WebSocket.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Text, true, linkedCts.Token);
            }
            return Task.CompletedTask;
        }

        protected async Task ProcessWebSocketRequest(HttpListenerContext context, CancellationToken cancellationToken)
        {
            var clientId = Guid.NewGuid();

            try
            {
                const int BufferSize = 4096;
                byte[] buffer = new byte[BufferSize];

                var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
                using var webSocket = webSocketContext.WebSocket;

                _clients.TryAdd(clientId, new(webSocket));

                ClientConnected?.Invoke(this, new(clientId));

                // Handle WebSocket messages
                while (webSocket.State is WebSocketState.Open)
                {
                    var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (receiveResult.MessageType is WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken);
                        break;
                    }

                    _requestManager.HandleRequest(clientId, Encoding.UTF8.GetString(buffer, 0, receiveResult.Count));
                }
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"WebSocket handshake error: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
                return;
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                ClientDisconnected?.Invoke(this, new(clientId));
            }
        }

    }

}