using System.Text.Json;
using System.Text.Json.Serialization;
using BuildMonitor.Extensions;
using WebMessage.Messages;

namespace WebMessage.Server
{

    internal class WebMessageService : IWebMessageService
    {
        protected abstract class RequestInfoBase
        {
            public string Uri { get; }

            public Type RequestType { get; }

            public Type ResponseType { get; }

            public RequestInfoBase(string uri, Type requestType, Type responseType)
            {
                Uri = uri;
                RequestType = requestType;
                ResponseType = responseType;
            }

            public abstract Task<string?> HandleRequest(string clientId, Message message);

            internal virtual string CreateResponse<TResponse>(Message request, TResponse response)
            {
                if (request.Uri != Uri)
                {
                    throw new InvalidOperationException($@"The actual request 'Uri: {request.Uri}' does not match the registered 'Uri: {Uri}'");
                }
                return response.CreateMessage(Message.TypeResponse, request.Uri, request.Id).ToJson();
            }

            internal virtual string CreateError(Message request, string error)
            {
                return request.CreateError(error).ToJson();
            }
        }

        protected class RequestInfo<TResponse> : RequestInfoBase
        {
            public RequestHandler<TResponse> Handler { get; set; }

            public RequestInfo(string uri, RequestHandler<TResponse> handler) : base(uri, typeof(Message), typeof(TResponse))
            {
                Handler = handler;
            }

            public override async Task<string?> HandleRequest(string clientId, Message message)
            {
                try
                {
                    var response = await Handler(clientId, message.Id, message.Type);
                    return CreateResponse<TResponse>(message, response);
                }
                catch (Exception ex)
                {
                    return CreateError(message, ex.Message);
                }
            }
        }

        protected class RequestInfo<TRequest, TResponse> : RequestInfoBase
        {
            public RequestHandler<TRequest, TResponse> Handler { get; set; }

            public RequestInfo(string uri, RequestHandler<TRequest, TResponse> handler) : base(uri, typeof(Message<TRequest>), typeof(TResponse))
            {
                Handler = handler;
            }

            public override async Task<string?> HandleRequest(string clientId, Message message)
            {
                if (message is Message<TRequest> request)
                {
                    try
                    {
                        var response = await Handler(clientId, message.Id, message.Type, request.Payload!);
                        return CreateResponse<TResponse>(message, response);
                    }
                    catch (Exception ex)
                    {
                        return CreateError(message, ex.Message);
                    }
                }
                return CreateError(message, "Invalid request");
            }
        }

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

        public void UnregisterRequest(string uri)
        {
            _registeredRequests.RemoveAll(r => r.Uri == uri);
            _messageTypeResolver.Remove(uri);
        }

        internal async Task<string> HandleRequest(string clientId, string message)
        {
            var request = DeserializeRequest(message);
            if (request is null)
            {
                var response = new Message
                {
                    Id = string.Empty,
                    Type = Message.TypeError,
                    Error = $@"Unsupported request: {message}"
                };
                return JsonSerializer.Serialize(response);
            }
            var requestType = request.GetType();
            var requestInfo = _registeredRequests.FirstOrDefault(ri => ri.RequestType == requestType);
            var result = await (requestInfo?.HandleRequest(clientId, request) ?? Task.FromResult<string?>(null));
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

        protected string SerializeRequest<TRequest>(TRequest request) where TRequest : Message
        {
            return JsonSerializer.Serialize(request, CreateOptions());
        }

        protected Message? DeserializeRequest(string request)
        {
            return JsonSerializer.Deserialize<Message>(request, CreateOptions());
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