using System.Text.Json;
using System.Text.Json.Serialization;
using WebMessage.Commands;
using WebMessage.Messages;

namespace BuildMonitor.Extensions
{
    internal static class MessageExtensions
    {
        public static string ToJson<TMessage>(this TMessage message, JsonSerializerOptions? options = null) where TMessage : Message
        {
            options ??= new JsonSerializerOptions();
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
#if DEBUG
            options.WriteIndented = true;
#endif
            return JsonSerializer.Serialize(message, options);
        }

        public static string ToResponseJson<TResponse>(this TResponse response) where TResponse : Message
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new ResponseTypeResolver(response.Id, typeof(TResponse))
            };
            return response.ToJson(options);
        }

        public static Message? FromJson(this string data, JsonSerializerOptions? options = null)
        {
            return data.FromJson<Message>(options);
        }

        public static TMessage? FromJson<TMessage>(this string data, JsonSerializerOptions? options = null) where TMessage : Message
        {
            options ??= new JsonSerializerOptions();
            try
            {
                return JsonSerializer.Deserialize<TMessage>(data, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return null;
            }
        }

        public static Message? FromResponseJson(this string data, IDictionary<string, Type> typeDiscriminators)
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new ResponseTypeResolver(typeDiscriminators)
            };
            return data.FromJson<Message>(options);
        }

        public static Message CreateResponse(this Message request)
        {
            var responseMessage = new Message
            {
                Uri = request.Uri,
                Id = request.Id,
                Type = Message.TypeResponse,
            };
            return responseMessage;
        }

        public static Message<TPayload> CreateResponse<TPayload>(this Message request, TPayload payload)
        {
            return payload.CreateMessage(Message.TypeResponse, request.Uri, request.Id);
        }

        public static Message CreateError(this Message? request, string error)
        {
            return new Message
            {
                Uri = request?.Uri ?? string.Empty,
                Id = request?.Id ?? string.Empty,
                Type = Message.TypeError,
                Error = error,
            };
        }

        public static Message<TPayload> CreateMessage<TPayload>(this TPayload payload, string type, string uri, string? id = null)
        {
            var message = new Message<TPayload>
            {
                Uri = uri,
                Type = type,
                Payload = payload,
            };

            if (id is not null)
            {
                message.Id = id;
            }

            if (payload is ICommandCustom commandCustom)
            {
                if (!string.IsNullOrEmpty(commandCustom.CustomId))
                {
                    message.Id = commandCustom.CustomId;
                }

                if (!string.IsNullOrEmpty(commandCustom.CustomType))
                {
                    message.Type = commandCustom.CustomType;
                }
            }

            return message;
        }
    }
}

