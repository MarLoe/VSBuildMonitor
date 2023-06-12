using System.Text;
using System.Text.Json;
using com.lobger.WebSocketServer;
using com.lobger.WebSocketServer.Messages;

namespace ClientConsole;

class Program
{
    static async Task Main(string[] args)
    {
        var request = new Message<HandshakeRequest>
        {
            Type = "request",
            Uri = "handshake",
            Payload = new() { Token = "TheToken" },
        };

        var requestManager = new RequestManager();
        requestManager.RegisterRequest("subscribe", (c, m, t) =>
        {
            Console.WriteLine($@"Client {c}");
            return Task.CompletedTask;
        });
        requestManager.RegisterRequest<HandshakeRequest>("handshake", (c, m, t, r) =>
        {
            Console.WriteLine($@"Client {c}: {r.Token}");
            return Task.CompletedTask;
        });

        var message = await File.ReadAllTextAsync("Requests/SubscribeRequest.json");
        requestManager.HandleRequest(Guid.NewGuid(), message);

        message = await File.ReadAllTextAsync("Requests/HandshakeRequest.json");
        requestManager.HandleRequest(Guid.NewGuid(), message);

        //await File.WriteAllTextAsync("Requests/HandshakeRequest.json", json, Encoding.UTF8);

        //var req = JsonSerializer.Deserialize<Message>(json, options);

        var server = new WebSocketServer("http://+:11011/");
        await server.Start();
    }
}

