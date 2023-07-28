using System.Text.Json;
using System.Text.Json.Serialization;
using BuildMonitor.Extensions;
using BuildMonitor.Logging;
using Microsoft.Extensions.Logging;
using WebMessage.Messages;

namespace WebMessage.Server
{
    internal class WebMessageService : IWebMessageService
    {
        private static readonly ILogger<WebMessageService> _logger = LoggerFactory.Global.CreateLogger<WebMessageService>();

        private readonly List<RequestInfoBase> _registeredRequests;
        private readonly MessageTypeResolver _messageTypeResolver;
        private readonly List<IWebMessageConnection> _connections;

        public WebMessageService()
        {
            _registeredRequests = new();
            _messageTypeResolver = new();
            _connections = new();
        }

        public event EventHandler<ClientConnectionEventArgs>? ClientConnected;

        public event EventHandler<ClientConnectionEventArgs>? ClientDisconnected;

        public void RegisterRequestHandler<TResponse>(string uri, RequestHandler<TResponse> handler)
        {
            var requestInfo = _registeredRequests.FirstOrDefault(r => r.Uri == uri);
            if (requestInfo is not null)
            {
                if (requestInfo is RequestInfo<TResponse> ri)
                {
                    ri.Handler = handler;
                    return;
                }
                throw new ArgumentException($@"The request '{uri}' has already been registered", nameof(uri));
            }

            _registeredRequests.Add(new RequestInfo<TResponse>(uri, handler));
            _messageTypeResolver.Add(uri);

            _logger.LogInformation("Registered request handler: {uri}", uri);
        }

        public void RegisterRequestHandler<TRequest, TResponse>(string uri, RequestHandler<TRequest, TResponse> handler)
        {
            var requestInfo = _registeredRequests.FirstOrDefault(r => r.Uri == uri);
            if (requestInfo is not null)
            {
                if (requestInfo is RequestInfo<TRequest, TResponse> ri)
                {
                    ri.Handler = handler;
                    return;
                }
                throw new ArgumentException($@"The request '{uri}' has already been registered with another type: {typeof(TRequest).FullName}", nameof(uri));
            }

            _registeredRequests.Add(new RequestInfo<TRequest, TResponse>(uri, handler));
            _messageTypeResolver.Add<TRequest>(uri);

            _logger.LogInformation("Registered request handler: {uri} for {type}", uri, typeof(TRequest));
        }

        public void UnregisterRequestHandler(string uri)
        {
            _logger.LogInformation("Unregistering request handler: {uri}", uri);

            _registeredRequests.RemoveAll(r => r.Uri == uri);
            _messageTypeResolver.Remove(uri);
        }

        public Task<bool> SendAsync<TResponse>(string clientId, TResponse response)
        {
            ArgumentException.ThrowIfNullOrEmpty(clientId);

            var requestInfo = FindRequestInfo<TResponse>();

            var connection = _connections.FirstOrDefault(c => c.Id == clientId);
            if (connection is null)
            {
                _logger.LogWarning("Client with id {id} not found", clientId);
                return Task.FromResult(false);
            }

            var message = response.CreateMessage(Message.TypeResponse, requestInfo.Uri, string.Empty);
            return connection?.SendAsync(message.ToJson()) ?? Task.FromResult(false);
        }

        public Task<bool> BroadcastAsync<TResponse>(TResponse response)
        {
            // Find request info first. It will throw if the request is not valid.
            var requestInfo = FindRequestInfo<TResponse>();

            var connection = _connections.FirstOrDefault();
            if (connection is null)
            {
                _logger.LogInformation("No connections available");
                return Task.FromResult(true); // No clients connected - all good
            }

            var message = response.CreateMessage(Message.TypeResponse, requestInfo.Uri, string.Empty);
            return connection.BroadcastAsync(message.ToJson()) ?? Task.FromResult(false);
        }

        public async Task<bool> RaiseEventAsync<TResponse>(TResponse response)
        {
            var requestInfo = FindRequestInfo<TResponse>();
            var message = response.CreateMessage(Message.TypeEvent, requestInfo.Uri, string.Empty);
            var data = message.ToJson();
            var sendTasks = _connections
                .Where(c => requestInfo.Subscriptions.Contains(c.Id))
                .Select(c => c.SendAsync(data));
            var result = await Task.WhenAll(sendTasks);
            return result.All(r => r is true);
        }

        internal async Task<string> HandleRequest(string clientId, string message)
        {
            var request = message.FromJson(CreateOptions());
            if (request is null)
            {
                _logger.LogError("Unsupported request: {message}", message);
                return request.CreateError($@"Unsupported request: {message}").ToJson();
            }

            if (request.Type is Message.TypeResponse or Message.TypeError)
            {
                _logger.LogError("Unsupported request type: {type}", request.Type);
                return request.CreateError($@"Unsupported request type: {request.Type}").ToJson();
            }

            var requestInfo = _registeredRequests.FirstOrDefault(ri => ri.Uri == request.Uri);
            if (requestInfo is null)
            {
                _logger.LogError("Invalid request uri: {uri}", request.Uri);
                return request.CreateError($@"Invalid request uri: {request.Uri}").ToJson();
            }

            if (request.Type is Message.TypeSubscribe or Message.TypeUnsubscribe)
            {
                if (requestInfo.HandleSubscribe(clientId, request))
                {
                    return request.CreateResponse().ToJson();
                }
                _logger.LogError("Unable to {type} to {uri}", request.Type, request.Uri);
                return request.CreateError($@"Unable to {request.Type} to {request.Uri}").ToJson();
            }

            var result = await requestInfo.HandleRequest(clientId, request);
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException("Request must be handled correctly");
            }

            return result;
        }

        internal void AddConnection(IWebMessageConnection connection)
        {
            ArgumentNullException.ThrowIfNull(connection);
            if (string.IsNullOrEmpty(connection.Id))
            {
                throw new ArgumentException("Cannot add client with no id", nameof(connection));
            }

            var existing = _connections.FirstOrDefault(c => c.Id == connection.Id);
            if (existing is not null)
            {
                if (existing == connection)
                {
                    _logger.LogInformation("Connection already added");
                    return;
                }
                throw new ArgumentException($@"Connection with id '{connection.Id}' already exists");
            }
            _connections.Add(connection);
            ClientConnected?.Invoke(this, new(connection.Id));
        }

        internal void RemoveConnection(IWebMessageConnection connection)
        {
            ArgumentNullException.ThrowIfNull(connection);
            _connections.RemoveAll(c => c.Id == connection.Id);
            ClientDisconnected?.Invoke(this, new(connection.Id));
        }

        protected RequestInfoBase FindRequestInfo<TResponse>()
        {
            return _registeredRequests.FirstOrDefault(r => r.ResponseType == typeof(TResponse))
                ?? throw new InvalidOperationException($@"Cannot send a response that has not been registered: {typeof(TResponse)}");
        }

        protected JsonSerializerOptions CreateOptions()
        {
            return new JsonSerializerOptions
            {
                TypeInfoResolver = _messageTypeResolver,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
#if DEBUG
                WriteIndented = true,
#endif
            };
        }

    }

}