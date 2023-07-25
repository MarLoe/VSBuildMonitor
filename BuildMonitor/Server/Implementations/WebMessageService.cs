using System.Text.Json;
using System.Text.Json.Serialization;
using BuildMonitor.Extensions;
using WebMessage.Messages;

namespace WebMessage.Server
{
    internal class WebMessageService : IWebMessageService
    {
        private readonly List<RequestInfoBase> _registeredRequests;
        private readonly RequestTypeResolver _messageTypeResolver;
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
        }

        public void UnregisterRequestHandler(string uri)
        {
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
                return Task.FromResult(false);
            }

            var message = response.CreateMessage(Message.TypeResponse, requestInfo.Uri, string.Empty);
            return connection?.SendAsync(message.ToJson()) ?? Task.FromResult(false);
        }

        public Task<bool> BroadcastAsync<TResponse>(TResponse response)
        {
            var requestInfo = FindRequestInfo<TResponse>();

            var connection = _connections.FirstOrDefault();
            if (connection is null)
            {
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
                return request.CreateError($@"Unsupported request: {message}").ToJson();
            }

            if (request.Type is Message.TypeResponse or Message.TypeError)
            {
                return request.CreateError($@"Unsupported request type: {request.Type}").ToJson();
            }

            var requestInfo = _registeredRequests.FirstOrDefault(ri => ri.Uri == request.Uri);
            if (requestInfo is null)
            {
                return request.CreateError($@"Invalid request uri: {request.Uri}").ToJson();
            }

            if (request.Type is Message.TypeSubscribe or Message.TypeUnsubscribe)
            {
                if (requestInfo.HandleSubscribe(clientId, request))
                {
                    return request.CreateResponse().ToResponseJson();
                }
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