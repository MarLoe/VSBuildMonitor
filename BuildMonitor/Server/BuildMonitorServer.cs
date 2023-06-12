using System.Security.Cryptography.X509Certificates;
using BuildMonitor.Commands.Api;
using BuildMonitor.Messages;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace BuildMonitor.Server
{
    public class BuildMonitorServer
    {
        private readonly RequestManager _requestManager;
        private readonly WebSocketServer _server;

        public BuildMonitorServer() : this(13001, true)
        {
        }

        public BuildMonitorServer(int port, bool secure)
        {
            _requestManager = new RequestManager();
            _requestManager.RegisterRequestHandler<HandshakeRequest, HandshakeResponse>(string.Empty, HandshakeRequestHandler);

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
            _server.AddWebSocketService<BuildMonitorBehavior>("/", b => b.RequestManager = _requestManager);

            _server.AllowForwardedRequest = true;

#if DEBUG
            // To change the logging level.
            _server.Log.Level = LogLevel.Trace;

            // To change the wait time for the response to the Ping or Close.
            _server.WaitTime = TimeSpan.FromSeconds(10);

#endif

            _server.Start();
        }


        public Func<HandshakeRequest, Task<string>>? PairingRequest { get; set; }

        private async Task<HandshakeResponse> HandshakeRequestHandler(string clientId, string messageId, string messageType, HandshakeRequest request)
        {
            var response = new HandshakeResponse();
            if (PairingRequest is null)
            {
                response.Key = Guid.NewGuid().ToString();
            }
            else
            {
                response.Key = await PairingRequest.Invoke(request);
                response.ReturnValue = !string.IsNullOrEmpty(response.Key);
            }
            return response;
        }

    }

    internal class BuildMonitorBehavior : WebSocketBehavior
    {
        public IRequestManager? RequestManager { get; set; }

        protected override void OnMessage(MessageEventArgs e)
        {
            _ = HandleRequest(e);
        }

        private async Task HandleRequest(MessageEventArgs e)
        {
            if (e.Data is not null)
            {
                var response = await RequestManager!.HandleRequest(ID, e.Data);
                Send(response);
            }
        }
    }
}

