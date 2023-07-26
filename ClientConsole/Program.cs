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
                var key = $@"MARTIN {Guid.NewGuid()}";
                Console.WriteLine($@"Generate: {key}");
                return Task.FromResult(key);
            });
            service.RegisterRequestHandler<BuildProgressCommand, BuildProgressResponse>("build/progress", (cid, mid, type, req) =>
            {
                Console.WriteLine($@"Handler: {nameof(BuildProgressCommand)} ({cid})");
                return Task.FromResult(new BuildProgressResponse(0.1f));
            });
            service.ClientConnected += (s, e) =>
            {
                clientId = e.Id;
                Console.WriteLine($@"Client connected: {clientId}");
            };

            var device = new WebMessageDevice { HostName = "localhost", Port = 13000 };

            var clients = new[]
            {
                new WebMessageClient(new SocketConnection()),
                //new WebMessageClient(new SocketConnection()),
                //new WebMessageClient(new SocketConnection()),
            };

            foreach (var client in clients)
            {
                client.PairingUpdated += (s, e) =>
                {
                    Console.WriteLine($@"Received: {e.PairingKey}");
                };
                Console.WriteLine($@"Attach: {device.IPAddress}");
                await client.AttachAsync(device);
            }

            await Task.WhenAll(clients.Select(c =>
            {
                Console.WriteLine($@"Connect: {device.IPAddress}");
                return c.ConnectAsync();
            }));

            foreach (var client in clients)
            {
                Console.WriteLine($@"Subscribe: {nameof(BuildProgressCommand)} ({device.IPAddress})");
                await client.SubscribeCommandAsync<BuildProgressCommand, BuildProgressResponse>(new(), r =>
                {
                    Console.WriteLine($@"Event: {r.ReturnValue}");
                }, default);
            }

            var buildProgress = await Task.WhenAll(clients.Select(c =>
            {
                Console.WriteLine($@"SendCommand: {nameof(BuildProgressCommand)} ({device.IPAddress})");
                return c.SendCommandAsync<BuildProgressCommand, BuildProgressResponse>(new());
            }));
            Console.WriteLine(string.Join("; ", buildProgress.Select(p => $@"{p}")));

            await service.RaiseEventAsync(new BuildProgressResponse(0.2f));

            await service.SendAsync(clientId, new HandshakeResponse { Key = "Just you!" });

            await service.BroadcastAsync(new HandshakeResponse { Key = "All of you!" });

            foreach (var client in clients)
            {
                client.Close();
            }
        }
    }

}