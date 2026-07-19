using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace WebSockets.Resilience;

/// <summary>
/// Layers reconnection, a configurable backoff strategy, a send queue that
/// survives reconnect gaps, and fragmented-message reassembly on top of
/// <see cref="IWebSocketClient"/>. This is a genuinely different contract
/// from <see cref="WebSocketClient"/>: a live <see cref="WebSocket"/> is a
/// single, one-shot connection by design, so a resilient client can't just
/// hand one back, it has to own the reconnect loop and give the caller a
/// stable send/receive surface across however many underlying connections
/// happen along the way.
///
/// This type doesn't construct or configure the wrapped <see cref="IWebSocketClient"/>
/// itself, it's handed one and calls <see cref="IWebSocketClient.ConnectAsync"/>
/// on it for every connection attempt, initial and every subsequent
/// reconnect. Configure <c>.Options</c> (headers, sub-protocols, TLS
/// callbacks, etc.) on the client instance before passing it in, e.g.:
/// <code>
/// var client = new WebSocketClient();
/// client.Options.RequestedSubProtocols.Add("chat");
///
/// var resilient = await client.AsResilient(uri).StartAsync();
/// </code>
/// Whatever's configured applies uniformly across every connection this
/// type makes, there's no separate configuration surface here to duplicate.
/// </summary>
public sealed class ResilientWebSocketClient : IResilientWebSocketClient
{
    private readonly IWebSocketClient _client;
    private readonly Uri _uri;
    private readonly Channel<string> _sendQueue;
    private readonly CancellationTokenSource _cts = new();

    private Task? _runLoop;
    private volatile WebSocket? _current;
    private volatile Task? _currentSendTask;
    private TaskCompletionSource<bool>? _firstConnectionReady;
    private int _tornDown;

    /// <inheritdoc/>
    public ResilientWebSocketClientOptions Options { get; }

    /// <inheritdoc/>
    public event Action<Exception>? ReconnectFailed;

    /// <inheritdoc/>
    public event Action? Reconnected;

    /// <inheritdoc/>
    public event Action<string>? MessageReceived;

    /// <summary>
    /// Wraps <paramref name="client"/> for connecting to <paramref name="uri"/>
    /// with reconnection, backoff, and message-queuing behavior. Doesn't
    /// configure <paramref name="client"/> itself; configure its <c>.Options</c>
    /// before passing it in, see the class remarks above.
    /// </summary>
    public ResilientWebSocketClient(IWebSocketClient client, Uri uri, ResilientWebSocketClientOptions? options = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Options = options ?? new ResilientWebSocketClientOptions();

        _sendQueue = Channel.CreateBounded<string>(new BoundedChannelOptions(Options.SendQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    /// <inheritdoc/>
    public async Task<IResilientWebSocketClient> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_runLoop is not null)
        {
            throw new InvalidOperationException($"{nameof(ResilientWebSocketClient)} has already been started.");
        }

        _current = await _client.ConnectAsync(_uri, cancellationToken).ConfigureAwait(false);

        _firstConnectionReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _runLoop = Task.Run(() => RunAsync(_cts.Token));

        // Don't return until the background loop has actually started
        // pumping send/receive for this connection, not just been scheduled
        // to. Without this, _currentSendTask could still be observed as
        // null by a caller (or a fast-following CloseAsync) under enough
        // thread-pool scheduling contention, Task.Run only queues the work,
        // it doesn't guarantee it's started by the time this method returns.
        await _firstConnectionReady.Task.ConfigureAwait(false);

        return this;
    }

    /// <inheritdoc/>
    public async Task<IResilientWebSocketClient> SendAsync(string message, CancellationToken cancellationToken = default)
    {
        await _sendQueue.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        return this;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task sendTask;
            Task receiveTask;

            try
            {
                if (_current is null)
                {
                    _current = await _client.ConnectAsync(_uri, cancellationToken).ConfigureAwait(false);
                    attempt = 0;
                    Reconnected?.Invoke();
                }

                sendTask = SendLoopAsync(_current, connectionCts.Token);
                _currentSendTask = sendTask;
                _firstConnectionReady?.TrySetResult(true);
                receiveTask = ReceiveLoopAsync(_current, connectionCts.Token);

                await Task.WhenAny(sendTask, receiveTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                ReconnectFailed?.Invoke(ex);
                _current = null;
                _currentSendTask = null;
                await DelayAsync(++attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // One of send/receive ended (socket closed, faulted, or the
            // other side disconnected); stop whichever is still running
            // before tearing down and reconnecting.
            connectionCts.Cancel();
            try
            {
                await Task.WhenAll(sendTask, receiveTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected from whichever task we just cancelled.
            }
            catch (Exception ex)
            {
                ReconnectFailed?.Invoke(ex);
            }

            _current = null;
            _currentSendTask = null;

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await DelayAsync(++attempt, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = GetDelay(attempt);
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelled during the delay; the outer loop's cancellation
            // check handles exiting cleanly.
        }
    }

    private TimeSpan GetDelay(int attempt)
        => ReconnectBackoff.GetDelay(Options.BackoffStrategy, Options.InitialReconnectDelay, Options.MaxReconnectDelay, attempt);

    private async Task SendLoopAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        while (await _sendQueue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_sendQueue.Reader.TryRead(out var message))
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(message);
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // The message wasn't confirmed delivered, whether this
                    // connection is being torn down for a reconnect or the
                    // socket genuinely failed mid-write, TCP doesn't
                    // guarantee which. Put it back so the next connection's
                    // send loop picks it up instead of losing it silently.
                    // (Note: this can reorder it behind whatever else was
                    // queued after it; fine for our purposes, not a strict
                    // ordering guarantee.)
                    try
                    {
                        await _sendQueue.Writer.WriteAsync(message, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Writer already completed (e.g. a concurrent
                        // DisposeAsync); nothing more we can do for this message.
                    }

                    throw;
                }
            }
        }
    }

    private async Task ReceiveLoopAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var messageBuffer = new MemoryStream();

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken).ConfigureAwait(false);
                return;
            }

            messageBuffer.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                messageBuffer.SetLength(0);
                MessageReceived?.Invoke(message);
            }
        }
    }

    /// <inheritdoc/>
    public async Task CloseAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        // Stop accepting new sends immediately, so nothing can be queued
        // after this point and get silently lost, that's the gap a
        // standalone "flush" step (without also tearing down atomically)
        // would leave open.
        _sendQueue.Writer.TryComplete();

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                var sendTask = _currentSendTask;

                if (sendTask is not null)
                {
                    // Wait for whatever's actually in flight to finish.
                    // Reader.Completion alone isn't enough here: an item
                    // counts as "read" from the channel's own perspective
                    // the instant it's dequeued, even before the
                    // socket.SendAsync call for it has actually completed.
                    await Task.WhenAny(sendTask, Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token)).ConfigureAwait(false);
                }
                else if (!_sendQueue.Reader.Completion.IsCompleted)
                {
                    // Disconnected or between connections with messages
                    // still queued; give the reconnect loop a moment to
                    // establish a new connection and pick them up, rather
                    // than busy-looping while it does.
                    await Task.Delay(TimeSpan.FromMilliseconds(50), linkedCts.Token).ConfigureAwait(false);
                }

                // Done once the channel is confirmed drained and no newer
                // send task started while we were waiting on this one (a
                // reconnect could have started one in between).
                if (_sendQueue.Reader.Completion.IsCompleted && ReferenceEquals(sendTask, _currentSendTask))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timed out or externally cancelled; proceed to teardown regardless.
        }

        await TeardownAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _sendQueue.Writer.TryComplete();
        await TeardownAsync().ConfigureAwait(false);
    }

    private async Task TeardownAsync()
    {
        // Guards against running twice, e.g. CloseAsync followed by a later
        // DisposeAsync (via `await using`), or DisposeAsync called more than
        // once; CancellationTokenSource.Cancel() throws if called after
        // Dispose(), so the second caller through here needs to be a no-op.
        if (Interlocked.Exchange(ref _tornDown, 1) != 0)
        {
            return;
        }

        _cts.Cancel();

        if (_runLoop is not null)
        {
            try
            {
                await _runLoop.ConfigureAwait(false);
            }
            catch
            {
                // Shutdown shouldn't surface run-loop exceptions to a caller
                // that's just closing/disposing.
            }
        }

        _current?.Dispose();
        _cts.Dispose();
    }
}