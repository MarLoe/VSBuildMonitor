using WebMessage.Client;
using WebMessage.Device;
using WebMessage.Server;

namespace ClientConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new WebMessageServer();
            var requestManager = server.AddService("/", r =>
            {
                var key = $@"MARTIN {Guid.NewGuid().ToString()}";
                Console.WriteLine($@"Generate: {key}");
                return Task.FromResult(key);
            });

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

            foreach (var client in clients)
            {
                client.Close();
            }

            await Task.Delay(1000);

            Console.ReadLine();
        }
    }

}