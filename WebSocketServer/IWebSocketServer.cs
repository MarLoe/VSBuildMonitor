using System;
using System.Threading;
using System.Threading.Tasks;

namespace com.lobger.WebSocketServer
{
    public interface IWebSocketServer
    {
        event EventHandler<ClientConnectionEventArgs> ClientConnected;

        event EventHandler<ClientConnectionEventArgs> ClientDisconnected;

        Task Start();

        void Stop();

        Task SendMessage(Guid clientId, string message, CancellationToken cancellationToken = default);

        Task BroadcastMessgaeAsync(string message, CancellationToken cancellationToken = default);
    }

    public class ClientConnectionEventArgs : EventArgs
    {
        public ClientConnectionEventArgs(Guid clientId)
        {
            ClientId = clientId;
        }

        public Guid ClientId { get; }
    }
}