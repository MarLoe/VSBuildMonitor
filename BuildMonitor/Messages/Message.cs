using System.Text.Json.Serialization;

namespace WebMessage.Messages
{
    /// <summary>
    /// The actual message format to be sent to the WebOS device.
    /// </summary>
    internal class Message
    {
        public const string TypeReqest = "request";

        public const string TypeSubscribe = "subscribe";

        public const string TypeUnsubscribe = "unsubscribe";

        public const string TypeResponse = "response";

        public const string TypeEvent = "event";

        public const string TypeError = "error";


        /// <summary>
        /// The uri of the message identifying the command being sent.
        /// </summary>
        [JsonPropertyOrder(int.MinValue), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string Uri { get; set; } = string.Empty;

        /// <summary>
        /// The id of the message. Resposed will be tagged
        /// with the same id to match request/response.
        /// </summary>
        [JsonPropertyOrder(int.MinValue + 1), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Id { get; set; } = GenerateId();

        /// <summary>
        /// The type of message. Can be:
        /// request
        /// </summary>
        [JsonPropertyOrder(2)]
        public string Type { get; set; } = TypeReqest;

        /// <summary>
        /// Error status when the message is recieved as a response.
        /// </summary>
        [JsonPropertyOrder(3)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Error { get; set; }

        internal static string GenerateId()
        {
            return Guid.NewGuid().ToString().Substring(0, 8);
        }
    }

    internal class Message<TPayload> : Message
    {
        /// <summary>
        /// The payload of the message (e.g. command json)
        /// </summary>
        [JsonPropertyOrder(int.MaxValue), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TPayload? Payload { get; set; }
    }

}

