# StaticNorth.AsyncApi

A transport-agnostic AsyncAPI toolchain for .NET. Annotate C# definitions and build an AsyncAPI 3.1.0 document at runtime. The Core, Generator, and Client projects share one compiler analyzer. Client code generation is planned for M3.

## Build and verify

Use .NET SDK 11.0.100 and Node.js 24 or newer for the compatibility tests. Run these commands from the repository root.

```sh
dotnet build StaticNorth.AsyncApi.slnx
dotnet test --solution StaticNorth.AsyncApi.slnx --no-build --no-restore
npm --prefix tests/compatibility ci --ignore-scripts
npm --prefix tests/compatibility test
```

The solution builds both console samples. The compatibility suite validates the exported producer document with the official AsyncAPI parser. It also builds and runs producer apps from the packed NuGets and checks that producer, client, and combined apps each resolve the shared analyzer once.

## Export an AsyncAPI document

```sh
dotnet run --no-build --project samples/Producer -- export > /tmp/orders.asyncapi.json
```

In your application, reference `StaticNorth.AsyncApi.Generator` and implement a marked definition class for each message, channel, or operation. The [Orders sample](samples/Producer/OrderCreatedDefinition.cs) declares an `OrderCreated` message, links it to a channel, and declares a `send` operation. `AsyncApiDefinitions.Register(builder)` registers the discovered definitions. Call `Build()` to execute their `Configure` methods and `AsyncApiJson.Write(document)` to export UTF-8 JSON. Use explicit factory registration for definitions that need injected services.

The builder infers payload schemas from `System.Text.Json` metadata. Use `PayloadSchema` when the wire format depends on custom serialization. The [standards matrix](docs/standards-coverage.md) tracks the tested surface and its limits.

[Architecture decision](docs/architecture.md) · [Standards coverage and limits](docs/standards-coverage.md) · [Changelog](CHANGELOG.md) · [Apache-2.0 license](LICENSE)
