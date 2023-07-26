using WebMessage.Commands;
using WebMessage.Device;

namespace WebMessage.Client
{
    /// <summary>
    /// Client for communicating with a message server.
    /// </summary>
    public interface IWebMessageClient
    {

        /// <summary>
        /// Raised when when connection is established or closed.
        /// </summary>
        event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;

        /// <summary>
        /// Raised if an invalid message is received.
        /// </summary>
        event EventHandler<InvalidMessageEventArgs> InvalidMessage;

        /// <summary>
        /// Raised when the pairing key is updated.
        /// </summary>
        event EventHandler<PairingUpdatedEventArgs> PairingUpdated;

        /// <summary>
        /// Returns true if client has an active connection.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Returns true if client has paired with device.<br/>
        /// Pairing happens when<see cref="ConnectAsync(CancellationToken)"/> is called successfully.
        /// </summary>
        bool IsPaired { get; }

        /// <summary>
        /// Attach to device.<br/>
        /// This allows to connect with a device without initiating pairing.
        /// This can be used in e.g. discovery situations, where you might want
        /// to connect to a device to verify that is is in fact connectable but
        /// without risking the "pairing toast" on the TV. It also allows for
        /// keeping track if the device (or you) "leaves" the network.
        /// </summary>
        /// <param name="device">
        /// The device to attach to.
        /// </param>
        Task AttachAsync(IDevice device);

        /// <summary>
        /// Connect to the device that was attached using <see cref="AttachAsync(IDevice)"/>.
        /// Establish connection with a handshake.
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// Connect to the device that was attached using <see cref="AttachAsync(IDevice)"/>.
        /// Establish connection with a handshake.
        /// </summary>
        /// <param name="cancellationToken">
        /// Connecting will start by sending a handshake. If the device is not
        /// yet paired, a pairing request will be shown on screen. This means
        /// that we will not get a response until the user has accpeted/rejected
        /// the request and thus this method will not return.<br/>
        /// The <paramref name="cancellationToken"/> allows for the handshake to
        /// be cancelled.
        /// </param>
        Task ConnectAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Close the connection.
        /// </summary>
        void Close();

        /// <summary>
        /// Send command to the server.
        /// </summary>
        /// <typeparam name="TResponse">
        /// The expected response type.
        /// </typeparam>
        /// <param name="command">
        /// The command to send.
        /// </param>
        /// <returns>
        /// The response or Task failed on error.
        /// </returns>
        Task<TResponse?> SendCommandAsync<TCommand, TResponse>(TCommand command) where TCommand : ICommand where TResponse : class, IResponse, new();

        /// <summary>
        /// Send command to the server.
        /// </summary>
        /// <typeparam name="TResponse">
        /// The expected response type.
        /// </typeparam>
        /// <param name="command">
        /// The command to send.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token for aborting the command.
        /// </param>
        /// <returns>
        /// The response or Task failed on error.
        /// </returns>
        Task<TResponse?> SendCommandAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken) where TCommand : ICommand where TResponse : class, IResponse, new();

        /// <summary>
        /// Subscribe to events on the server
        /// </summary>
        /// <typeparam name="TCommand">
        /// The type of the command to subscribe to.
        /// </typeparam>
        /// <typeparam name="TResponse">
        /// The expected response type.
        /// </typeparam>
        /// <param name="command">
        /// The command to subscribe to.
        /// </param>
        /// <param name="eventHandler">
        /// Called when ever an event is raised on the server.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token for aborting the command.
        /// </param>
        /// <returns>
        /// On success <c>true</c>, else <c>false</c>.
        /// </returns>
        Task<bool> SubscribeCommandAsync<TCommand, TResponse>(TCommand command, Action<TResponse> eventHandler, CancellationToken cancellationToken) where TCommand : ICommand where TResponse : class, IResponse, new();
    }


    public abstract class DeviceEventArgs : EventArgs
    {
        public DeviceEventArgs(IDevice device)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
        }

        /// <summary>
        /// The updated device.
        /// </summary>
        public IDevice Device { get; }
    }


    /// <summary>
    /// Event arguments for <see cref="IWebMessageClient.ConnectionChanged"/>
    /// </summary>
    public class ConnectionChangedEventArgs : DeviceEventArgs
    {
        public ConnectionChangedEventArgs(IDevice device, bool isConnected) : base(device)
        {
            IsConnected = isConnected;
        }

        public bool IsConnected { get; }
    }


    /// <summary>
    /// Event arguments for <see cref="IWebMessageClient.InvalidMessage"/>
    /// </summary>
    public class InvalidMessageEventArgs : DeviceEventArgs
    {
        public InvalidMessageEventArgs(IDevice device, string message) : base(device)
        {
            Message = message;
        }

        public string Message { get; }
    }


    /// <summary>
    /// Event arguemnts for <see cref="IWebMessageClient.PairingUpdated"/>.
    /// </summary>
    public class PairingUpdatedEventArgs : DeviceEventArgs
    {
        public PairingUpdatedEventArgs(IDevice device, string pairingKey) : base(device)
        {
            PairingKey = pairingKey;
        }

        public string PairingKey { get; }
    }
}