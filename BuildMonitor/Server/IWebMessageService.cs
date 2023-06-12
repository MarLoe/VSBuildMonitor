namespace WebMessage.Server
{
    public delegate Task<TResponse> RequestHandler<TResponse>(string clientId, string messageId, string messageType);

    public delegate Task<TResponse> RequestHandler<TRequest, TResponse>(string clientId, string messageId, string messageType, TRequest request);

    public interface IWebMessageService
    {
        void RegisterRequestHandler<TResponse>(string uri, RequestHandler<TResponse> handler);

        void RegisterRequestHandler<TRequest, TResponse>(string uri, RequestHandler<TRequest, TResponse> handler);

        void UnregisterRequest(string uri);
    }
}