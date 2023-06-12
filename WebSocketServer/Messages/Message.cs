using System;
using System.Text.Json.Serialization;
//using Newtonsoft.Json.Linq;

namespace com.lobger.WebSocketServer.Messages
{
    /// <summary>
    /// The actual message format to be sent to the WebOS device.
    /// </summary>
    internal class Message
    {
        /// <summary>
        /// The id of the message. Resposed will be tagged
        /// with the same id to match request/response.
        /// </summary>
        [JsonPropertyOrder(1)]
        public string? Id { get; set; } = GenerateId();

        /// <summary>
        /// The type of message. Can be:
        /// request
        /// </summary>
        [JsonPropertyOrder(2)]
        public string? Type { get; set; }

        /// <summary>
        /// The uri of the message identifying the command being sent.
        /// </summary>
        [JsonPropertyOrder(0)]
        public string? Uri { get; set; }

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
        [JsonPropertyOrder(99)]
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

