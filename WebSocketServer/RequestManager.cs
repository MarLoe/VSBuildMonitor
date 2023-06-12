using System.Text.Json;
using com.lobger.WebSocketServer.Messages;

namespace com.lobger.WebSocketServer
{
    internal class RequestManager : IRequestManager
    {
        protected abstract class RequestInfoBase
        {
            public string Uri { get; }

            public Type RequestType { get; }

            public RequestInfoBase(string uri, Type requestType)
            {
                Uri = uri;
                RequestType = requestType;
            }
        }

        protected class RequestInfo : RequestInfoBase
        {
            public Callback Callback { get; set; }

            public RequestInfo(string uri, Callback callback) : base(uri, typeof(Message))
            {
                Callback = callback;
            }
        }

        protected class RequestInfo<TRequest> : RequestInfoBase
        {
            public Callback<TRequest> Callback { get; set; }

            public RequestInfo(string uri, Callback<TRequest> callback) : base(uri, typeof(Message<TRequest>))
            {
                Callback = callback;
            }
        }

        private readonly HashSet<RequestInfoBase> _registeredRequests;

        private JsonSerializerOptions? _options;

        public RequestManager()
        {
            _registeredRequests = new();

            UpdateOptions();
        }

        public void RegisterRequest(string uri, Callback callback)
        {
            var requestInfo = _registeredRequests.FirstOrDefault(r => r.Uri == uri);
            if (requestInfo is not null)
            {
                if (requestInfo is RequestInfo ri)
                {
                    ri.Callback = callback;
                    return;
                }
                throw new ArgumentException($@"The request '{uri}' has already been registered", nameof(uri));
            }

            _registeredRequests.Add(new RequestInfo(uri, callback));
            UpdateOptions();
        }

        public void RegisterRequest<TRequest>(string uri, Callback<TRequest> callback)
        {
            var requestInfo = _registeredRequests.FirstOrDefault(r => r.Uri == uri);
            if (requestInfo is not null)
            {
                if (requestInfo is RequestInfo<TRequest> ri)
                {
                    ri.Callback = callback;
                    return;
                }
                throw new ArgumentException($@"The request '{uri}' has already been registered with another type: {typeof(TRequest).FullName}", nameof(uri));
            }

            _registeredRequests.Add(new RequestInfo<TRequest>(uri, callback));
            UpdateOptions();
        }

        public void UnregisterRequest(string uri)
        {
            _registeredRequests.RemoveWhere(r => r.Uri == uri);
            UpdateOptions();
        }

        public void HandleRequest(Guid clientId, string message)
        {
            dynamic? request = DeserializeRequest(message);
            if (request is not null)
            {
                var requestType = request.GetType();
                dynamic? requestInfo = _registeredRequests.FirstOrDefault(ri => ri.RequestType == requestType);
                if (requestInfo is RequestInfo ri)
                {
                    ri.Callback(clientId, request.Id, request.Type);
                }
                else
                {
                    requestInfo?.Callback(clientId, request.Id, request.Type, request.Payload);
                }
            }
        }

        internal string SerializeRequest<TRequest>(TRequest request) where TRequest : Message
        {
            return JsonSerializer.Serialize(request, _options);
        }

        internal Message? DeserializeRequest(string request)
        {
            return JsonSerializer.Deserialize<Message>(request, _options);
        }

        private void UpdateOptions()
        {
            _options = new JsonSerializerOptions
            {
                TypeInfoResolver = new MessageRequestTypeResolver(_registeredRequests.ToDictionary(k => k.Uri, v => v.RequestType))
            };
        }

    }

}