using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using WebMessage.Commands.Api;
using WebMessage.Messages;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace WebMessage.Server
{
    public class WebMessageServer
    {
        //private readonly RequestManager _requestManager;
        private readonly WebSocketServer _server;

        public WebMessageServer() : this(13001, true)
        {
        }

        public WebMessageServer(int port, bool secure)
        {
            //_requestManager = new RequestManager();
            //_requestManager.RegisterRequestHandler<HandshakeRequest, HandshakeResponse>(string.Empty, HandshakeRequestHandler);

            _server = new WebSocketServer(port, secure)
            {
                KeepClean = true,
            };
            if (_server.IsSecure)
            {
                var path = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location) ?? "", "Certificate", "server.pfx");
                _server.SslConfiguration.ServerCertificate = new X509Certificate2(path, "BuildMonitorServer");
                _server.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                _server.SslConfiguration.ClientCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // Do something to validate the server certificate.
                    return true; // If the server certificate is valid.
                };
            }

            _server.AllowForwardedRequest = true;

#if DEBUG
            // To change the logging level.
            _server.Log.Level = LogLevel.Trace;

            // To change the wait time for the response to the Ping or Close.
            _server.WaitTime = TimeSpan.FromSeconds(10);
#endif

            _server.Start();
        }


        //public Func<HandshakeRequest, Task<string>>? PairingRequest { get; set; }

        private readonly ConcurrentDictionary<string, WebMessageService> _requestManagers = new();

        public IWebMessageService AddService(string path, Func<HandshakeRequest, Task<string>>? pairingRequest = null)
        {
            async Task<HandshakeResponse> HandshakeRequestHandler(string clientId, string messageId, string messageType, HandshakeRequest request)
            {
                var response = new HandshakeResponse();
                if (pairingRequest is null)
                {
                    response.Key = Guid.NewGuid().ToString();
                }
                else
                {
                    response.Key = await pairingRequest.Invoke(request);
                    response.ReturnValue = !string.IsNullOrEmpty(response.Key);
                }
                return response;
            }

            var requestManager = new WebMessageService();
            if (!_requestManagers.TryAdd(path, requestManager))
            {
                throw new ArgumentException($@"A service with the path: '{path}' is already registered", nameof(path));
            }

            requestManager.RegisterRequestHandler<HandshakeRequest, HandshakeResponse>(string.Empty, HandshakeRequestHandler);
            _server.AddWebSocketService<WebMessageBehavior>(path, b =>
            {
                System.Diagnostics.Debug.WriteLine(b.ID);
                b.RequestManager = requestManager;
            });

            return requestManager;
        }

    }
}

