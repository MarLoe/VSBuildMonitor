using BuildMonitor.Extensions;
using BuildMonitor.Logging;
using Microsoft.Extensions.Logging;
using WebMessage.Messages;

namespace WebMessage.Server
{
    /// <summary>
    /// Class for holding request info
    /// </summary>
    internal abstract class RequestInfo
    {
        protected static readonly ILogger _logger = LoggerFactory.Global.CreateLogger<RequestInfo>();

        public string Uri { get; }

        public Type RequestType { get; }

        public Type ResponseType { get; }

        public List<string> Subscriptions { get; }

        public RequestInfo(string uri, Type requestType, Type responseType)
        {
            Uri = uri;
            RequestType = requestType;
            ResponseType = responseType;
            Subscriptions = new();
        }

        public abstract Task<string?> HandleRequest(string clientId, Message message);

        public bool HandleSubscribe(string clientId, Message message)
        {
            if (message.Type is Message.TypeUnsubscribe)
            {
                Subscriptions.Remove(clientId);
                return true;
            }
            if (message.Type is Message.TypeSubscribe)
            {
                if (!Subscriptions.Contains(clientId))
                {
                    Subscriptions.Add(clientId);
                }
                return true;
            }
            return false;
        }

        internal virtual string CreateResponse<TResponse>(Message request, TResponse response)
        {
            if (request.Uri != Uri)
            {
                throw new InvalidOperationException($@"The actual request 'Uri: {request.Uri}' does not match the registered 'Uri: {Uri}'");
            }
            return response.CreateMessage(Message.TypeResponse, request.Uri, request.Id).ToJson(logger: _logger);
        }

        internal virtual string CreateError(Message request, string error)
        {
            return request.CreateError(error).ToJson(logger: _logger);
        }
    }

    /// <summary>
    /// Hold request info for untyped requests
    /// </summary>
    /// <typeparam name="TResponse">
    /// Type of the response.
    /// </typeparam>
    internal class RequestInfo<TResponse> : RequestInfo
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
                return CreateResponse(message, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle request from client {id}", clientId);
                return CreateError(message, ex.Message);
            }
        }
    }

    /// <summary>
    /// Hold request info for typed requests
    /// </summary>
    /// <typeparam name="TRequest">
    /// Type of the request.
    /// </typeparam>
    /// <typeparam name="TResponse">
    /// Type of the response.
    /// </typeparam>
    internal class RequestInfo<TRequest, TResponse> : RequestInfo
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
                    return CreateResponse(message, response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle request from client {id}", clientId);
                    return CreateError(message, ex.Message);
                }
            }
            return CreateError(message, "Invalid request");
        }
    }

}