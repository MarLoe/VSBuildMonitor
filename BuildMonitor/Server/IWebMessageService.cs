namespace WebMessage.Server
{
    public delegate Task<TResponse> RequestHandler<TResponse>(string clientId, string messageId, string messageType);

    public delegate Task<TResponse> RequestHandler<TRequest, TResponse>(string clientId, string messageId, string messageType, TRequest request);

    public interface IWebMessageService
    {
        event EventHandler<ClientConnectionEventArgs> ClientConnected;

        event EventHandler<ClientConnectionEventArgs> ClientDisconnected;

        void RegisterRequestHandler<TResponse>(string uri, RequestHandler<TResponse> handler);

        void RegisterRequestHandler<TRequest, TResponse>(string uri, RequestHandler<TRequest, TResponse> handler);

        void UnregisterRequestHandler(string uri);

        Task<bool> SendAsync<TResponse>(string clientId, TResponse response);

        Task<bool> BroadcastAsync<TResponse>(TResponse response);

        Task<bool> RaiseEventAsync<TResponse>(TResponse response);
    }

    public class ClientConnectionEventArgs : EventArgs
    {
        public string Id { get; }

        public ClientConnectionEventArgs(string id)
        {
            Id = id;
        }
    }
}