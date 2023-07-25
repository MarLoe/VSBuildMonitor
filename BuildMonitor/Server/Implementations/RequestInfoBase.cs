using BuildMonitor.Extensions;
using WebMessage.Messages;

namespace WebMessage.Server
{
    /// <summary>
    /// Class for holding request info
    /// </summary>
    internal abstract class RequestInfoBase
    {
        public string Uri { get; }

        public Type RequestType { get; }

        public Type ResponseType { get; }

        public List<string> Subscriptions { get; }

        public RequestInfoBase(string uri, Type requestType, Type responseType)
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
            return response.CreateMessage(Message.TypeResponse, request.Uri, request.Id).ToResponseJson();
        }

        internal virtual string CreateError(Message request, string error)
        {
            return request.CreateError(error).ToJson();
        }
    }

    /// <summary>
    /// Hold request info for untyped requests
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    internal class RequestInfo<TResponse> : RequestInfoBase
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
                return CreateError(message, ex.Message);
            }
        }
    }

    /// <summary>
    /// Hold request info for typed requests
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    internal class RequestInfo<TRequest, TResponse> : RequestInfoBase
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
                    return CreateError(message, ex.Message);
                }
            }
            return CreateError(message, "Invalid request");
        }
    }

}