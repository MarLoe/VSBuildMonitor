using System.Collections.Concurrent;
using BuildMonitor.Extensions;
using BuildMonitor.Logging;
using Microsoft.Extensions.Logging;
using WebMessage.Commands;
using WebMessage.Commands.Api;
using WebMessage.Device;
using WebMessage.Exceptions;
using WebMessage.Messages;

namespace WebMessage.Client
{
    public class WebMessageClient : IWebMessageClient, IDisposable
    {
        private static readonly ILogger<WebMessageClient> _logger = LoggerFactory.Global.CreateLogger<WebMessageClient>();

        private readonly ISocketConnection _socket;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _completionSources = new();
        private readonly ConcurrentDictionary<string, Type> _typeDiscriminators = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Action<Message>>> _eventHandlers = new();

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
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                ctsTimeout.CancelAfter(TimeSpan.FromSeconds(150));
            }
#endif
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
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                ctsTimeout.CancelAfter(TimeSpan.FromSeconds(600));
            }
#endif
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
            _logger.LogTrace("Received: {data}", e.Data);

            var message = e.Data.FromResponseJson(_typeDiscriminators, _logger);
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
                else
                {
                    // It is not a response for a pending command.
                    if (message.Type is Message.TypeEvent)
                    {
                        Task.Run(() => HandleEvent(message));
                    }
                }
            }
        }

        private void HandleEvent(Message message)
        {
            if (_eventHandlers.TryGetValue(message.Uri, out var commandEventHandlers))
            {
                commandEventHandlers.Values.ToList().ForEach(h => h(message));
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
            var request = command.CreateMessage(Message.TypeSubscribe, command.Uri);

            var response = await SendMessageAsync<Message<TCommand>, Message<TResponse>>(request, cancellationToken);
            if (response is null)
            {
                return false;
            }

            var commandEventHandlers = _eventHandlers.GetOrAdd(command.Uri, _ => new());
            return commandEventHandlers.TryAdd(request.Id, m =>
            {
                if (m is Message<TResponse> { Payload: { } payload })
                {
                    eventHandler(payload);
                }
            });
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

            var existingType = _typeDiscriminators.GetOrAdd(message.Uri, typeof(TResponse));
            if (existingType != typeof(TResponse))
            {
                throw new CommandException($@"There is already a registered command with discriminator: {message.Uri} for a different type ({existingType})");
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
                    var json = message.ToJson(logger: _logger);

                    _logger.LogTrace("Sending: {json}", json);
                    _socket.Send(json);
                }, cancellationToken);

                if (await taskSource.Task is TResponse response)
                {
                    return response;
                }
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending message");
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
