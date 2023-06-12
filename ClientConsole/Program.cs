using com.lobger.WebSocketServer;

namespace ClientConsole;

class Program
{
    static async Task Main(string[] args)
    {
        var server = new WebSocketServer("http://+:11011/");
        await server.Start();
    }
}

