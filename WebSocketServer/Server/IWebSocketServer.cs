using System;
using System.Threading;
using System.Threading.Tasks;

namespace com.lobger.WebSocketServer
{
    public delegate Task Callback(Guid clientId, string messageId, string messageType);

    public delegate Task Callback<TRequest>(Guid clientId, string messageId, string messageType, TRequest request);

    public interface IWebSocketServer
    {
        event EventHandler<ClientEventArgs> ClientConnected;

        event EventHandler<ClientEventArgs> ClientDisconnected;

        event EventHandler<MessageReceivedEventArgs> MessageReceived;

        Task Start();

        void Stop();

        void RegisterRequest(string uri, Callback callback);

        void RegisterRequest<TRequest>(string uri, Callback<TRequest> callback);

        void UnregisterRequest(string uri);

        Task SendMessage(Guid clientId, string message, CancellationToken cancellationToken = default);

        Task BroadcastMessgaeAsync(string message, CancellationToken cancellationToken = default);
    }

    public class ClientEventArgs : EventArgs
    {
        public ClientEventArgs(Guid clientId)
        {
            ClientId = clientId;
        }

        public Guid ClientId { get; }
    }

    public class MessageReceivedEventArgs : ClientEventArgs
    {
        public MessageReceivedEventArgs(Guid clientId, string message) : base(clientId)
        {
            Message = message;
        }

        public string Message { get; }
    }
}