using WebSocketSharp;
using WebSocketSharp.Server;

namespace WebMessage.Server
{
    public interface IWebMessageConnection
    {
        string Id { get; }

        Task<bool> SendAsync(string data);
    }

    internal class WebMessageBehavior : WebSocketBehavior, IWebMessageConnection
    {
        internal WebMessageService? RequestManager { get; set; }


        #region IWebMessageConnection

        public string Id => ID;

        public Task<bool> SendAsync(string data)
        {
            var tcs = new TaskCompletionSource<bool>();
            SendAsync(data, r => tcs.TrySetResult(r));
            return tcs.Task;
        }

        #endregion

        protected override void OnOpen()
        {
            base.OnOpen();
            RequestManager!.AddClient(this);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            _ = HandleRequest(e);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            RequestManager!.RemoveClient(this);
        }

        protected virtual async Task HandleRequest(MessageEventArgs e)
        {
            if (e.Data is not null)
            {
                var response = await RequestManager!.HandleRequest(ID, e.Data);
                await SendAsync(response);
            }
        }

    }
}

