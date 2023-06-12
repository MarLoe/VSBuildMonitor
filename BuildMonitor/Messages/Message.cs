using System.Text.Json.Serialization;

namespace WebMessage.Messages
{
    /// <summary>
    /// The actual message format to be sent to the WebOS device.
    /// </summary>
    internal class Message
    {
        public const string RequestTypeReqest = "request";

        public const string RequestTypeResponse = "response";

        public const string ResponseTypeError = "error";


        /// <summary>
        /// The id of the message. Resposed will be tagged
        /// with the same id to match request/response.
        /// </summary>
        [JsonPropertyOrder(1), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Id { get; set; } = GenerateId();

        /// <summary>
        /// The type of message. Can be:
        /// request
        /// </summary>
        [JsonPropertyOrder(2)]
        public string Type { get; set; } = RequestTypeReqest;

        /// <summary>
        /// The uri of the message identifying the command being sent.
        /// </summary>
        [JsonPropertyOrder(0), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Uri { get; set; } = string.Empty;

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
        [JsonPropertyOrder(99), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TPayload? Payload { get; set; }
    }

    public class HandshakeRequest
    {
        public string? Token { get; set; }
    }

    public class BuildRequest
    {
        public bool Rebuild { get; set; }
    }

}

