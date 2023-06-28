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

        public static Message CreateResponse(this Message request)
        {
            var responseMessage = new Message
            {
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

