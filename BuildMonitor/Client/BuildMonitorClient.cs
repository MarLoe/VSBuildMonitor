using System.Collections.Concurrent;
using System.Text.Json;
using BuildMonitor.Commands;
using BuildMonitor.Commands.Api;
using BuildMonitor.Device;
using BuildMonitor.Exceptions;
using BuildMonitor.Messages;

namespace BuildMonitor.Client
{
    public class BuildMonitorClient : IBuildMonitorClient, IDisposable
    {
        //private readonly IFactory<ISocketConnection> _socketFactory;
        //private readonly ILogger<WebOSClient> _logger;

        private readonly ISocketConnection _socket;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _completionSources = new();
        private readonly MessageTypeResolver _responseOptions = new();

        private IDevice? _device;

        public int CommandTimeout { get; set; } = 5000;

        //public BuildMonitorClient() : this(new NullLogger<WebOSClient>())
        //{
        //}

        //public BuildMonitorClient(ILogger<WebOSClient> logger) : this(new Factory<ISocketConnection>(() => new SocketConnection()), logger)
        //{
        //}

        //internal BuildMonitorClient(IFactory<ISocketConnection> socketFactory, ILogger<WebOSClient> logger)
        //{
        //    _socketFactory = socketFactory;
        //    _logger = logger;
        //}

        public BuildMonitorClient(ISocketConnection socketConnection)
        {
            _socket = socketConnection ?? throw new ArgumentNullException(nameof(socketConnection));
        }

        #region IBuildMonitorClient

        public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;

        public event EventHandler<PairingUpdatedEventArgs>? PairingUpdated;

        public virtual bool IsConnected => _socket?.IsAlive is true;

        public virtual bool IsPaired { get; private set; }


        public virtual async Task AttachAsync(IDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);

            if (device == _device && _socket.IsAlive is true)
            {
                // All good - no need to reattach
                return;
            }

            CloseSockets();

            _device = device;

            _socket.OnMessage += OnSocketMessage;
            _socket.OnDisconnected += OnSocketDisconnect;

            await Task.Run(() => _socket.Connect(_device));

            if (!_socket.IsAlive)
            {
                throw new ConnectionException($"Unable to conenct to WebOS device at {device.HostName ?? device.IPAddress}.");
            }

            ConnectionChanged?.Invoke(this, new(_device, true));
        }

        public virtual async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_device is null)
            {
                throw new InvalidOperationException($@"Please call {nameof(AttachAsync)}() before connecting");
            }

            // Attach existing device - just in case
            await AttachAsync(_device);

            var handshakeResponse = await SendCommandAsyncInternal<HandshakeCommand, HandshakeResponse>(new HandshakeCommand(_device.PairingKey), cancellationToken);
            if (handshakeResponse.ReturnValue)
            {
                var updated = _device.PairingKey != handshakeResponse.Key;
                IsPaired = true;
                _device.PairingKey = handshakeResponse.Key;
                PairingUpdated?.Invoke(this, new(_device, updated));
            }
        }

        public virtual void Close()
        {
            _device = null;
            CloseSockets();
        }

        public virtual Task<TResponse> SendCommandAsync<TCommand, TResponse>(TCommand command) where TCommand : ICommand where TResponse : ResponseBase, new()
        {
            // Create a default timeout - in case we never get a response.
            var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            return SendCommandAsync<TCommand, TResponse>(command, ctsTimeout.Token);
        }

        public virtual async Task<TResponse> SendCommandAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken) where TCommand : ICommand where TResponse : ResponseBase, new()
        {
            if (_device is null)
            {
                throw new InvalidOperationException("Please attach to a device before sending commands");
            }

            if (!IsPaired)
            {
                throw new InvalidOperationException("Please connect device to make handshake before sending commands");
            }

            if (_socket?.IsAlive is not true)
            {
                await ConnectAsync(default);
            }

            return await SendCommandAsyncInternal<TCommand, TResponse>(command, cancellationToken);
        }

        #endregion


        #region IDisposable

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
            Close();
        }

        #endregion


        #region Event Handlers

        protected void OnSocketMessage(object? sender, SocketMessageEventArgs e)
        {
            //_logger.LogTrace("Received: {data}", e.Data);

            var options = new JsonSerializerOptions { TypeInfoResolver = _responseOptions };
            var response = JsonSerializer.Deserialize<Message>(e.Data, options);
            if (response is null)
            {
                // TODO: Report unknown message
                return;
            }

            if (_completionSources.TryRemove(response.Id, out var taskCompletion))
            {
                if (response.Type is Message.ResponseTypeError)
                {
                    taskCompletion.TrySetException(new CommandException(response.Error ?? "Unknown error"));
                }
                else
                {
                    taskCompletion.TrySetResult(response);
                }
            }
        }

        protected void OnSocketDisconnect(object? sender, EventArgs e)
        {
            ConnectionChanged?.Invoke(this, new(_device!, false));
        }

        #endregion


        #region Internal Methods

        /// <summary>
        /// Send command without any prerequisite checks
        /// </summary>
        protected async Task<TResponse> SendCommandAsyncInternal<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand where TResponse : ResponseBase, new()
        {
            var request = new Message<TCommand>
            {
                Uri = command.Uri,
                Type = "request",
                Payload = command,
            };

            if (command is ICommandCustom commandCustom)
            {
                if (!string.IsNullOrEmpty(commandCustom.CustomId))
                {
                    request.Id = commandCustom.CustomId;
                }

                if (!string.IsNullOrEmpty(commandCustom.CustomType))
                {
                    request.Type = commandCustom.CustomType;
                }
            }

            var taskSource = new TaskCompletionSource<Message>();
            if (!_completionSources.TryAdd(request.Id, taskSource))
            {
                throw new CommandException($@"There is already a pending command with id: {request.Id}");
            }

            using var ctr = cancellationToken.Register(() =>
            {
                if (_completionSources.TryRemove(request.Id, out var taskCompletion))
                {
                    taskCompletion.TrySetException(new TimeoutException($@"The command with id '{request.Id}' timed out"));
                }
            });

            _responseOptions.Add<TResponse>(request.Uri);

            try
            {
                await Task.Run(() =>
                {
                    var options = new JsonSerializerOptions
                    {
                        TypeInfoResolver = new MessageTypeResolver(new Dictionary<string, Type> { [command.Uri] = typeof(TCommand) })
                    };
                    var json = JsonSerializer.Serialize(request, options);


                    //_logger.LogTrace("Sending: {json}", json);
                    _socket.Send(json);
                }, cancellationToken);

                if (await taskSource.Task is Message<TResponse> { Payload: not null } response)
                {
                    return response.Payload;
                }
            }
            finally
            {
                _responseOptions.Remove(request.Uri);
            }

            return new TResponse { ReturnValue = false };
        }

        protected void CloseSockets()
        {
            if (_socket is not null)
            {
                _socket.OnMessage -= OnSocketMessage;
                _socket.OnDisconnected -= OnSocketDisconnect;
                _socket.Close();
            }
        }

        #endregion

    }
}
