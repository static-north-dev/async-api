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

For the Orders contract, `Orders.Contracts` contains `OrderCreated`, `IPublishOrderCreatedSubscriber`, `PublishOrderCreatedSubscriber`, and `PublishOrderCreatedMetadata`. A documented `send` gives the client a subscriber. A documented `receive` gives the client a publisher. The metadata preserves the operation ID, channel ID and address, message keys, content types, and JSON bindings.

Omit `AsyncApiNamespace` to generate under `AsyncApi.Generated.<document name>`. `AsyncApiAlias` replaces the filename in that default namespace. `AsyncApiModelPrefix` prefixes generated DTO names. `AsyncApiGenerate` accepts `both` (the default), `publishers`, or `subscribers`.

Run the checked-in example with `dotnet run --no-build --project samples/Consumer`. To use your own producer document, export it to a local file and use that file in `AdditionalFiles`. The generator supports AsyncAPI 3.1.0 JSON and local JSON Pointers. See the [client coverage and limits](docs/standards-coverage.md) for supported payloads and diagnostics.

## Connect a generated client to a transport

Pass an `IAsyncApiTransport` implementation to the generated client. For example, `new PublishOrderCreatedSubscriber(transport)` uses the fixed channel address in the checked-in Orders contract. Pass a concrete address as the second argument when the contract declares an address template or leaves the address unspecified. Keep the handle returned by `SubscribeOrderCreatedAsync` and dispose it to unsubscribe.

Use `InMemoryAsyncApiTransport` for local examples and tests. It matches the channel address and message key using ordinal comparison and awaits the matching handlers. Producer code can publish an `AsyncApiEnvelope` containing UTF-8 JSON bytes, an explicit route, headers, and a correlation ID. The generated subscriber deserializes the bytes before calling the handler. See the [transport contract](docs/architecture.md#transport-boundary) for lifetime and error behavior.

## Serve a contract from ASP.NET Core

In the host project, reference `StaticNorth.AsyncApi.Generator` and set `<AsyncApiEnableAspNetCore>true</AsyncApiEnableAspNetCore>`. Register a document builder with `builder.Services.AddAsyncApi(...)`, then map the endpoint with the host's authorization policy.

```csharp
app
    .MapAsyncApi("/asyncapi.json")
    .RequireAuthorization("InternalApi");
```

The [web sample](samples/WebProducer/Program.cs) shows generated definition registration and native ASP.NET Core authentication. It reads its demonstration bearer token from `ASYNCAPI_SPEC_TOKEN` and supports a custom route through `AsyncApi__Path`. The endpoint builds the document per request, responds with `application/json` and `Cache-Control: no-store`, and returns a generic 500 problem response for invalid document configuration.

If the contract is shared by all authorized callers, build it once and map a snapshot. The sample enables this mode with `AsyncApi__UseSnapshot=true`.

```csharp
var snapshot = new AsyncApiDocumentSnapshot(documentBuilder.Build());
app
    .MapAsyncApiSnapshot(snapshot, "/asyncapi.json")
    .RequireAuthorization("InternalApi");
```

Call `snapshot.Update(documentBuilder.Build())` when the contract changes. Each request uses a complete serialized version, and the host applies authorization and `Cache-Control: no-store` on every request.

## View the contract in a browser

Set `ASYNCAPI_SPEC_TOKEN` to a local demonstration token, then build and run the optional viewer in the web sample.

```sh
npm --prefix samples/WebProducer/Viewer ci --ignore-scripts
dotnet run --project samples/WebProducer -p:AsyncApiEnableViewer=true -- --urls http://127.0.0.1:5080
```

Open [the local documentation page](http://127.0.0.1:5080/asyncapi-ui) and enter the token. The page loads the same JSON endpoint, including a route set through `AsyncApi__Path`, and ASP.NET Core applies `InternalApi` to each document request. The browser sends the token only to that endpoint and keeps it out of storage and the URL. The sample serves the renderer locally. Publish with `-p:AsyncApiEnableViewer=true` to embed its assets in the sample assembly.

See the [renderer evaluation](docs/architecture.md#optional-documentation-viewer) for bundle sizes and the viewer's browser policy.

## Acquire a remote contract

Run the fetch sample explicitly before building a client. Add the resulting local file to `AdditionalFiles`.

```sh
dotnet run --project samples/FetchContract -- refresh https://example.com/asyncapi.json orders.asyncapi.json
dotnet run --project samples/FetchContract -- offline orders.asyncapi.json "$ASYNCAPI_SHA256"
```

Supply a trusted SHA-256 checksum as the optional last argument to `refresh` to pin downloaded bytes. Offline mode requires that checksum and reads only the local file. Use `ASYNCAPI_SPEC_TOKEN` from your credential source for the sample bearer provider, or implement `ISpecificationAuthenticationProvider` in your own fetch host. Authenticated downloads require HTTPS. Failed refreshes leave the previous file intact. The fetcher validates the supported document version, root structure, and inline operation actions. Run the official AsyncAPI parser when you need full contract validation.

## Build release packages

CI verifies the solution and uploads the three Release NuGet packages. To prepare for publishing, configure the GitHub `nuget` environment with deployment protection and its `NUGET_USER` secret. Create a NuGet Trusted Publishing policy for `static-north-dev/async-api`, workflow file `release.yml`, and environment `nuget`. Set the intended version in `Directory.Build.props`, then dispatch the **Publish NuGet packages** workflow from the default branch. [NuGet Trusted Publishing setup](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)

[Architecture decision](docs/architecture.md) · [Standards coverage and limits](docs/standards-coverage.md) · [Changelog](CHANGELOG.md) · [Apache-2.0 license](LICENSE)
