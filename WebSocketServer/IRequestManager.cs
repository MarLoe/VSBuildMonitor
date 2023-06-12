namespace com.lobger.WebSocketServer
{
    internal interface IRequestManager
    {
        void RegisterRequest(string uri, Callback callback);

        void RegisterRequest<TRequest>(string uri, Callback<TRequest> callback);

        void UnregisterRequest(string uri);

        void HandleRequest(Guid clientId, string message);
    }
}