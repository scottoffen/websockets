---
sidebar_position: 5
title: ResilientWebSocketClientOptions
---

All configuration for [`ResilientWebSocketClient`](./resilience) lives on `Options`, a `ResilientWebSocketClientOptions` instance.

| Property | Type | Default |
|---|---|---|
| `BackoffStrategy` | `BackoffStrategy` (`None`, `Linear`, `Exponential`) | `None` |
| `InitialReconnectDelay` | `TimeSpan` | 1 second |
| `MaxReconnectDelay` | `TimeSpan` | 30 seconds |
| `SendQueueCapacity` | `int` | 256 |

## Setting options

Either construct the options object directly and pass it to `AsResilient`:

```csharp
var options = new ResilientWebSocketClientOptions
{
    BackoffStrategy = BackoffStrategy.Exponential,
    InitialReconnectDelay = TimeSpan.FromSeconds(2),
    MaxReconnectDelay = TimeSpan.FromMinutes(1),
};

var resilient = await client.AsResilient(uri, options).StartAsync();
```

or configure inline via the other `AsResilient` overload:

```csharp
var resilient = await client
    .AsResilient(uri, o => o.BackoffStrategy = BackoffStrategy.Linear)
    .StartAsync();
```

`BackoffStrategy`, `InitialReconnectDelay`, and `MaxReconnectDelay` can also be set after construction via fluent extensions, see [Fluent Extensions](./fluent-extensions). `SendQueueCapacity` cannot, see below.

## `BackoffStrategy`

How long to wait between reconnect attempts after a dropped connection.

### `None` (the default)

No delay between reconnect attempts, retries immediately. This is deliberately the default: it's a real behavior, and a real risk (hammering a server that's genuinely down), rather than a hidden fixed delay, so using this in production requires actively choosing `Linear` or `Exponential` rather than inheriting an assumption made on your behalf.

### `Linear`

Delay grows linearly with each attempt: `InitialReconnectDelay * attempt` (1s, 2s, 3s, 4s... with the defaults), capped at `MaxReconnectDelay`.

### `Exponential`

Delay doubles with each attempt: `InitialReconnectDelay * 2^(attempt - 1)` (1s, 2s, 4s, 8s... with the defaults), capped at `MaxReconnectDelay`.

The actual calculation is exposed as a pure, static function independent of any live connection, `ReconnectBackoff.GetDelay(BackoffStrategy strategy, TimeSpan initialDelay, TimeSpan maxDelay, int attempt)`, if you need to reason about or test the delay sequence directly.

## `SendQueueCapacity`

The maximum number of not-yet-sent messages buffered while disconnected. Unlike the other three properties above, this one is only ever read once, at construction, to size the internal send queue. Changing it afterward, even immediately after construction, has no effect, which is exactly why there's no `WithSendQueueCapacity` fluent extension. Set it via the `AsResilient(uri, Action<ResilientWebSocketClientOptions>)` overload shown above, which configures the options before the client is actually built.