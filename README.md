# StaticNorth.AsyncApi

A transport-agnostic AsyncAPI toolchain for .NET. Annotate C# definitions and build an AsyncAPI 3.1.0 document at runtime, or generate client contracts from a local document at compile time. The Core, Generator, and Client packages share one compiler analyzer.

## Build and verify

Use .NET SDK 11.0.100 and Node.js 24 or newer for the compatibility tests. Run these commands from the repository root.

```sh
dotnet build StaticNorth.AsyncApi.slnx
dotnet test --solution StaticNorth.AsyncApi.slnx --no-build --no-restore
npm --prefix tests/compatibility ci --ignore-scripts
npm --prefix tests/compatibility test
```

The solution builds both console samples. The compatibility suite validates the producer export with the official AsyncAPI parser. It also builds applications from the packed NuGets and checks that a generated client can deserialize the producer's exported Orders example.

## Export an AsyncAPI document

```sh
dotnet run --no-build --project samples/Producer -- export > /tmp/orders.asyncapi.json
```

In your application, reference `StaticNorth.AsyncApi.Generator` and implement a marked definition class for each message, channel, or operation. The [Orders sample](samples/Producer/OrderCreatedDefinition.cs) declares an `OrderCreated` message, links it to a channel, and declares a `send` operation. `AsyncApiDefinitions.Register(builder)` registers the discovered definitions. Call `Build()` to execute their `Configure` methods and `AsyncApiJson.Write(document)` to export UTF-8 JSON. Use explicit factory registration for definitions that need injected services.

The builder infers payload schemas from `System.Text.Json` metadata. Use `PayloadSchema` when the wire format depends on custom serialization. The [standards matrix](docs/standards-coverage.md) tracks the tested surface and its limits.

## Generate client contracts

Reference `StaticNorth.AsyncApi.Client` in a .NET 11 project and add a local `*.asyncapi.json` file to `AdditionalFiles`. Build with the .NET 11 SDK.

```xml
<ItemGroup>
  <AdditionalFiles Include="Contracts/orders.asyncapi.json"
                   AsyncApiNamespace="Orders.Contracts"
                   AsyncApiGenerate="subscribers" />
</ItemGroup>
```

For the Orders contract, `Orders.Contracts` contains `OrderCreated`, `IPublishOrderCreatedSubscriber`, and `PublishOrderCreatedMetadata`. A documented `send` gives the client a subscriber interface. A documented `receive` gives the client a publisher interface. The metadata preserves the operation ID, channel ID and address, message keys, content types, and JSON bindings. M4 covers transport adapters and implementation of those interfaces.

Omit `AsyncApiNamespace` to generate under `AsyncApi.Generated.<document name>`. `AsyncApiAlias` replaces the filename in that default namespace. `AsyncApiModelPrefix` prefixes generated DTO names. `AsyncApiGenerate` accepts `both` (the default), `publishers`, or `subscribers`.

Run the checked-in example with `dotnet run --no-build --project samples/Consumer`. To use your own producer document, export it to a local file and use that file in `AdditionalFiles`. The generator supports AsyncAPI 3.1.0 JSON and local JSON Pointers. See the [client coverage and limits](docs/standards-coverage.md) for supported payloads and diagnostics.

[Architecture decision](docs/architecture.md) · [Standards coverage and limits](docs/standards-coverage.md) · [Changelog](CHANGELOG.md) · [Apache-2.0 license](LICENSE)
