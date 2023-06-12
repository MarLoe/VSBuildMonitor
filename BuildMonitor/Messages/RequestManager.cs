
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BuildMonitor.Messages
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

            public abstract Task<string?> HandleRequest(string clientId, Message message);

            protected virtual string CreateResponse<TResponse>(Message request, TResponse response)
            {
                var responseMessage = new Message<TResponse>
                {
                    Uri = request.Uri,
                    Id = request.Id,
                    Type = "response",
                    Payload = response,
                };
                return CreateMessage(responseMessage);
            }

            protected virtual string CreateError(Message request, string error)
            {
                var responseMessage = new Message
                {
                    Uri = request.Uri,
                    Id = request.Id,
                    Type = Message.ResponseTypeError,
                };
                return CreateMessage(responseMessage);
            }

            protected virtual string CreateMessage<TMessage>(TMessage response) where TMessage : Message
            {
                var options = new JsonSerializerOptions
                {
                    //TypeInfoResolver = responseOptions,
                    //TypeInfoResolver = new DefaultJsonTypeInfoResolver
                    //{
                    //    Modifiers = { ExcludeEmptyStrings }
                    //},
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
#if DEBUG
                    WriteIndented = true,
#endif
                };
                //options.Converters.Add(new IgnoreEmptyStringConverter());
                return JsonSerializer.Serialize(response, options);
            }

            //static void ExcludeEmptyStrings(JsonTypeInfo jsonTypeInfo)
            //{
            //    if (jsonTypeInfo.Kind is JsonTypeInfoKind.Object)
            //    {
            //        foreach (JsonPropertyInfo jsonPropertyInfo in jsonTypeInfo.Properties.Where(p => p.PropertyType == typeof(string)))
            //        {
            //            jsonPropertyInfo.ShouldSerialize = static (obj, value) =>
            //            {
            //                return !string.IsNullOrEmpty((string?)value);
            //            };
            //        }
            //    }
            //}
        }

        protected class RequestInfo<TResponse> : RequestInfoBase
        {
            public RequestHandler<TResponse> Handler { get; set; }

            public RequestInfo(string uri, RequestHandler<TResponse> handler) : base(uri, typeof(Message))
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

            public RequestInfo(string uri, RequestHandler<TRequest, TResponse> handler) : base(uri, typeof(Message<TRequest>))
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

        private readonly HashSet<RequestInfoBase> _registeredRequests;

        private JsonSerializerOptions? _options;

        public RequestManager()
        {
            _registeredRequests = new();

            UpdateOptions();
        }

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
            UpdateOptions();
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
            UpdateOptions();
        }

        public void UnregisterRequest(string uri)
        {
            _registeredRequests.RemoveWhere(r => r.Uri == uri);
            UpdateOptions();
        }

        public async Task<string> HandleRequest(string clientId, string message)
        {
            var request = DeserializeRequest(message);
            if (request is null)
            {
                var response = new Message
                {
                    Id = "",
                    Type = Message.ResponseTypeError,
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
                TypeInfoResolver = new MessageRequestTypeResolver(_registeredRequests.ToDictionary(k => k.Uri, v => v.RequestType)),
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
#if DEBUG
                WriteIndented = true,
#endif
            };
        }

    }

    //public class IgnoreEmptyStringConverter : JsonConverter<string>
    //{
    //    // Override default null handling
    //    public override bool HandleNull => true;

    //    // Ignore for this exampke
    //    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    //    {
    //        if (!string.IsNullOrWhiteSpace(value))
    //        {
    //            writer.WriteStringValue(value);
    //        }
    //    }
    //}

}