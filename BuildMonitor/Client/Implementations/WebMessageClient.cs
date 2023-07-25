using System.Collections.Concurrent;
using BuildMonitor.Extensions;
using WebMessage.Commands;
using WebMessage.Commands.Api;
using WebMessage.Device;
using WebMessage.Exceptions;
using WebMessage.Messages;

namespace WebMessage.Client
{
    public class WebMessageClient : IWebMessageClient, IDisposable
    {
        private readonly ISocketConnection _socket;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _completionSources = new();
        private readonly ConcurrentDictionary<string, Type> _typeDiscriminators = new();

        private IDevice? _device;

        public int CommandTimeout { get; set; } = 5000;

        public WebMessageClient(ISocketConnection socketConnection)
        {
            _socket = socketConnection ?? throw new ArgumentNullException(nameof(socketConnection));
        }

        #region IBuildMonitorClient

        public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;

        public event EventHandler<InvalidMessageEventArgs>? InvalidMessage;

        public event EventHandler<InvalidMessageEventArgs>? MessageReceived;

        public event EventHandler<PairingUpdatedEventArgs>? PairingUpdated;

        public virtual bool IsConnected => _socket?.IsAlive is true;

        public virtual bool IsPaired { get; protected set; }


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

        public virtual Task ConnectAsync()
        {
            // Create a default timeout - in case we never get a response.
            var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            return ConnectAsync(ctsTimeout.Token);
        }

        public virtual async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (_device is null)
            {
                throw new InvalidOperationException($@"Please call {nameof(AttachAsync)}() before connecting");
            }

            // Attach existing device - just in case
            await AttachAsync(_device);

            var handshakeResponse = await SendCommandInternalAsync<HandshakeCommand, HandshakeResponse>(new HandshakeCommand(_device.PairingKey), cancellationToken);
            if (handshakeResponse?.ReturnValue is true)
            {
                IsPaired = true;
                PairingUpdated?.Invoke(this, new(_device, handshakeResponse.Key));
            }
        }

        public virtual void Close()
        {
            _device = null;
            CloseSockets();
        }

        public virtual Task<TResponse?> SendCommandAsync<TCommand, TResponse>(TCommand command) where TCommand : ICommand where TResponse : class, IResponse, new()
        {
            // Create a default timeout - in case we never get a response.
            var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            return SendCommandAsync<TCommand, TResponse>(command, ctsTimeout.Token);
        }

        public virtual async Task<TResponse?> SendCommandAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken) where TCommand : ICommand where TResponse : class, IResponse, new()
        {
            await ValidateConnectionAsync();
            return await SendCommandInternalAsync<TCommand, TResponse>(command, cancellationToken);
        }

        public virtual async Task<bool> SubscribeCommandAsync<TCommand, TResponse>(TCommand command, Action<TResponse> eventHandler, CancellationToken cancellationToken) where TCommand : ICommand where TResponse : class, IResponse, new()
        {
            await ValidateConnectionAsync();
            return await SubscribeCommandInternalAsync(command, eventHandler, cancellationToken);
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

        private void OnSocketMessage(object? sender, SocketMessageEventArgs e)
        {
            //_logger.LogTrace("Received: {data}", e.Data);

            var message = e.Data.FromResponseJson(_typeDiscriminators);
            if (message is null)
            {
                InvalidMessage?.Invoke(this, new(_device!, e.Data));
                return;
            }

            if (PreProcessMessage(message))
            {
                if (_completionSources.TryRemove(message.Id, out var taskCompletion))
                {
                    if (message.Type is Message.TypeError)
                    {
                        taskCompletion.TrySetException(new CommandException(message.Error ?? "Unknown error"));
                    }
                    else
                    {
                        taskCompletion.TrySetResult(message);
                    }
                }
            }
        }

        protected void OnSocketDisconnect(object? sender, EventArgs e)
        {
            ConnectionChanged?.Invoke(this, new(_device!, false));
        }

        #endregion


        #region Virtual Helper Methods

        /// <summary>
        /// Send command without any prerequisite checks
        /// </summary>
        protected virtual async Task<TResponse?> SendCommandInternalAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken) where TCommand : ICommand where TResponse : class, IResponse, new()
        {
            var request = command.CreateMessage(Message.TypeReqest, command.Uri);
            var response = await SendMessageAsync<Message<TCommand>, Message<TResponse>>(request, cancellationToken);
            if (response is null)
            {
                return new TResponse { ReturnValue = false };
            }
            return response.Payload;
        }

        /// <summary>
        /// Subscribe command without any prerequisite checks
        /// </summary>
        protected virtual async Task<bool> SubscribeCommandInternalAsync<TCommand, TResponse>(TCommand command, Action<TResponse> eventHandler, CancellationToken cancellationToken) where TCommand : ICommand where TResponse : class, IResponse, new()
        {
            // TODO: Register eventHandler for receiveing events
            var request = command.CreateMessage(Message.TypeSubscribe, command.Uri);
            var response = await SendMessageAsync<Message<TCommand>, Message>(request, cancellationToken);
            return response is not null;
        }

        protected virtual bool PreProcessResponse(object? response, string uri, string type, string id)
        {
            return true;
        }

        protected virtual async Task ValidateConnectionAsync()
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
        }

        protected virtual void CloseSockets()
        {
            if (_socket is not null)
            {
                _socket.OnMessage -= OnSocketMessage;
                _socket.OnDisconnected -= OnSocketDisconnect;
                _socket.Close();
            }
        }

        #endregion


        #region Private Helper Methods

        private async Task<TResponse?> SendMessageAsync<TRequest, TResponse>(TRequest message, CancellationToken cancellationToken) where TRequest : Message where TResponse : Message, new()
        {
            var taskSource = new TaskCompletionSource<Message>();
            if (!_completionSources.TryAdd(message.Id, taskSource))
            {
                throw new CommandException($@"There is already a pending command with id: {message.Id}");
            }

            if (_typeDiscriminators.TryAdd(message.Id, typeof(TResponse)) is false)
            {
                throw new CommandException($@"There is already a registered command with id: {message.Id}");
            }

            using var ctr = cancellationToken.Register(() =>
            {
                if (_completionSources.TryRemove(message.Id, out var taskCompletion))
                {
                    taskCompletion.TrySetException(new TimeoutException($@"The command with id '{message.Id}' timed out"));
                }
            });

            try
            {
                await Task.Run(() =>
                {
                    //var options = new JsonSerializerOptions
                    //{
                    //    TypeInfoResolver = new MessageTypeResolver(new Dictionary<string, Type> { [message.Uri] = typeof(Message<TCommand>) })
                    //};
                    // TODO: Use options
                    var json = message.ToJson();

                    //_logger.LogTrace("Sending: {json}", json);
                    _socket.Send(json);
                }, cancellationToken);

                if (await taskSource.Task is TResponse response)
                {
                    return response;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                _typeDiscriminators.TryRemove(message.Id, out var _);
            }

            return null;
        }

        private bool PreProcessMessage(Message message)
        {
            var propertyInfo = message.GetType().GetProperty(nameof(Message<object>.Payload));
            var payload = propertyInfo?.GetValue(message);
            return PreProcessResponse(payload, message.Uri, message.Type, message.Id);
        }

        #endregion

    }
}
