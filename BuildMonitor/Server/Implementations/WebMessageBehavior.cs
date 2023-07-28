using BuildMonitor.Logging;
using Microsoft.Extensions.Logging;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace WebMessage.Server
{
    public interface IWebMessageConnection
    {
        string Id { get; }

        Task<bool> SendAsync(string data);

        Task<bool> BroadcastAsync(string data);
    }

    internal class WebMessageBehavior : WebSocketBehavior, IWebMessageConnection
    {
        private static readonly ILogger<WebMessageBehavior> _logger = LoggerFactory.Global.CreateLogger<WebMessageBehavior>();

        internal WebMessageService? RequestManager { get; set; }


        #region IWebMessageConnection

        public string Id => ID;

        public Task<bool> SendAsync(string data)
        {
            return Task.Run(() =>
            {
                try
                {
                    Send(data);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public Task<bool> BroadcastAsync(string data)
        {
            return Task.Run(() =>
            {
                try
                {
                    Sessions.Broadcast(data);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        #endregion


        protected override void OnOpen()
        {
            base.OnOpen();
            RequestManager!.AddConnection(this);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            _ = HandleRequest(e);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            RequestManager!.RemoveConnection(this);
        }

        protected virtual async Task HandleRequest(MessageEventArgs e)
        {
            if (e.Data is not null)
            {
                try
                {
                    var response = await RequestManager!.HandleRequest(ID, e.Data);
                    await SendAsync(response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle request: {data}", e.Data);
                }
            }
        }

    }
}

