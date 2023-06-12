using BuildMonitor.Client;
using BuildMonitor.Device;
using BuildMonitor.Server;

namespace ClientConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new BuildMonitorServer();

            var device = new BuildDevice { HostName = "localhost", Port = 13000 };
            var _socket = new SocketConnection();

            var client = new BuildMonitorClient(new SocketConnection());
            client.PairingUpdated += (s, e) => Console.WriteLine(e.Device.PairingKey);
            await client.AttachAsync(device);
            await client.ConnectAsync();

            Console.ReadLine();
        }
    }

}