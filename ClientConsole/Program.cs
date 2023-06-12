using WebMessage.Client;
using WebMessage.Commands.Api;
using WebMessage.Device;
using WebMessage.Server;

namespace ClientConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var clientId = string.Empty;

            var server = new WebMessageServer();
            var service = server.AddService("/", r =>
            {
                var key = $@"MARTIN {Guid.NewGuid().ToString()}";
                Console.WriteLine($@"Generate: {key}");
                return Task.FromResult(key);
            });
            service.ClientConnected += (s, e) =>
            {
                clientId = e.Id;
                Console.WriteLine($@"Client connected: {clientId}");
            };

            var device = new WebMessageDevice { HostName = "localhost", Port = 13000 };
            var _socket = new SocketConnection();

            var clients = new[]
            {
                new WebMessageClient(new SocketConnection()),
                new WebMessageClient(new SocketConnection()),
                new WebMessageClient(new SocketConnection()),
            };

            foreach (var client in clients)
            {
                client.PairingUpdated += (s, e) =>
                {
                    Console.WriteLine($@"Received: {e.PairingKey}");
                };
                await client.AttachAsync(device);
            }

            await Task.WhenAll(clients.Select(c => c.ConnectAsync()));

            await service.SendAsync(clientId, new HandshakeResponse { Key = "Just you!" });

            await service.BroadcastAsync(new HandshakeResponse { Key = "All of you!" });

            foreach (var client in clients)
            {
                client.Close();
            }
        }
    }

}