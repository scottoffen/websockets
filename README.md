# WebSockets

> [!IMPORTANT]
> **Is `WebSockets` still maintained?**
>
> It can be easy to assume that no activity on a repository and/or no updates in a long time means that a project has been abandoned. That is absolutely not the case here! See the [Contributing](#contributing) section below.
>
> The reality is that this package is designed to fix an issue that isn't going to change, so we might go long stretches between updates, if there's ever another one needed at all. That's driven by a few things:
> - .NET Framework 4.6.2, while still supported by Microsoft as part of Windows, is a stable, feature-frozen target that Microsoft's ongoing .NET investment has moved past.
> - .NET 5.0 is out of official support - but we chose that target so that we could provide support for all future .NET versions.
> - Microsoft treats this as intended behavior rather than a defect to patch, so there's no reason to expect it to disappear on its own in some future .NET release.
> - Given that nothing here is expected to change, there's nothing that would obviously require this package to change either.
>
> You can rest assured that this package will continue to be maintained **if** any issues are ever reported or new features requested.

## Overview

This repo hosts a small family of .NET WebSocket packages: `WebSockets`, a `ClientWebSocket` replacement that fixes a specific, durable handshake-validation bug present across every version of .NET, and `WebSockets.Resilience`, an optional reconnection and resilience layer built on top of it. See [Packages](#packages) below, or each package's own README, for details.

## Packages

| Package | Description |
|---|---|
| [`WebSockets`](src/WebSockets/README.md) | A `ClientWebSocket` replacement that fixes its overly strict `Connection` response-header validation, with fully overridable handshake validation. |
| [`WebSockets.Resilience`](src/WebSockets.Resilience/README.md) | Reconnection, configurable backoff, and a send queue that survives reconnect gaps, layered on top of `WebSockets`. |

## Documentation

Full documentation is at https://scottoffen.github.io/websockets.

## Community and Support

Engage in our [community discussions](https://github.com/scottoffen/websockets/discussions) for Q&A, ideas, and show and tell!

## Contributing

We welcome contributions from the community! In order to ensure the best experience for everyone, before creating an issue or submitting a pull request, please see the [contributing guidelines](./.github/contributing.md) and the [code of conduct](./.github/code_of_conduct.md). Failure to adhere to these guidelines can result in significant delays in getting your contributions included in the project.

## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, see the [tags on this repository](https://github.com/scottoffen/websockets/releases).

## Test Coverage

You can generate and open a test coverage report by running the following command in the project root:

```bash
pwsh ./test-coverage.ps1
```

> [!NOTE]
> This is a [Powershell](https://learn.microsoft.com/en-us/powershell/) script. You must have Powershell installed to run this command.

## License

WebSockets is dual-licensed under the [MIT License](LICENSE-MIT) and the [Apache License 2.0](LICENSE-APACHE). You may use it under either license.

## Using WebSockets? We'd Love To Hear About It!

Few things are as satisfying as hearing that your open source project is being used and appreciated by others. Jump over to the discussion boards and [share the love](https://github.com/scottoffen/websockets/discussions)!