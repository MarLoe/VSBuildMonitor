using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using BuildMonitor.Logging;
using Microsoft.Extensions.Logging;
using WebMessage.Commands.Api;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace WebMessage.Server
{
    public class WebMessageServer : IDisposable
    {
        private static readonly ILogger<WebMessageServer> _logger = LoggerFactory.Global.CreateLogger<WebMessageServer>();
        private readonly WebSocketServer _server;

        public WebMessageServer() : this(13001, true)
        {
        }

        public WebMessageServer(int port, bool secure)
        {
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

            //_server.AllowForwardedRequest = true;

#if DEBUG
            // To change the logging level.
            _server.Log.Level = WebSocketSharp.LogLevel.Trace;

            // To change the wait time for the response to the Ping or Close.
            _server.WaitTime = TimeSpan.FromSeconds(10);
#endif

            _server.Start();
        }

        ~WebMessageServer()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(false);
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            _server.Stop();
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        //public Func<HandshakeRequest, Task<string>>? PairingRequest { get; set; }

        private readonly ConcurrentDictionary<string, WebMessageService> _requestManagers = new();

        public IWebMessageService AddService(string path, Func<HandshakeCommand, Task<string>>? pairingRequest = null)
        {
            async Task<HandshakeResponse> HandshakeRequestHandler(string clientId, string messageId, string messageType, HandshakeCommand request)
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

            var service = new WebMessageService();
            if (!_requestManagers.TryAdd(path, service))
            {
                _logger.LogError("A service with the path: '{path}' is already registered", path);
                throw new ArgumentException($@"A service with the path: '{path}' is already registered", nameof(path));
            }

            service.RegisterRequestHandler<HandshakeCommand, HandshakeResponse>(string.Empty, HandshakeRequestHandler);
            _server.AddWebSocketService<WebMessageBehavior>(path, (b) =>
            {
                _logger.LogDebug("New web socket service for '{path}' with id: {id}", path, b.ID);
                b.RequestManager = service;
            });

            return service;
        }

    }
}

