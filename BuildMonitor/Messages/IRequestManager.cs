namespace BuildMonitor.Messages
{
    public delegate Task<TResponse> RequestHandler<TResponse>(string clientId, string messageId, string messageType);

    public delegate Task<TResponse> RequestHandler<TRequest, TResponse>(string clientId, string messageId, string messageType, TRequest request);

    internal interface IRequestManager
    {
        void RegisterRequestHandler<TResponse>(string uri, RequestHandler<TResponse> handler);

        void RegisterRequestHandler<TRequest, TResponse>(string uri, RequestHandler<TRequest, TResponse> handler);

        void UnregisterRequest(string uri);

        Task<string> HandleRequest(string clientId, string message);
    }
}