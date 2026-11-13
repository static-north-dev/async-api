# ADR 0001 — AsyncAPI document generation and export

Accepted for M1. The producer and consumer APIs below define the direction for M2 and M3.

## Packages and compatibility

`StaticNorth.AsyncApi.Core` owns the document model and JSON codec. `StaticNorth.AsyncApi.Generator` and `StaticNorth.AsyncApi.Client` each depend on Core for their `net11.0` runtime libraries.

The Generator project also builds a `netstandard2.0` target for compiler hosts. That target contains the producer and client incremental generators and ships as `StaticNorth.AsyncApi.Generator.Analyzers.dll` under `analyzers/dotnet/cs` in the Generator package. Client depends on the Generator package for its analyzer asset. An application referencing both packages resolves the same analyzer once. The analyzer target has private compiler references and loads independently of the runtime libraries.

The first wire format is AsyncAPI 3.1.0 JSON. Its version is independent of both the package version and the API's `info.version`. Core models document sections independently of producer builders and client generation. The M1 reader accepts exactly 3.1.0 and retains section contents as JSON for fidelity. The writer orders object properties by ordinal name, keeps array order and preserves numeric token spelling. The official document schema and the default AsyncAPI payload Schema Object are distinct. M2 will emit the default Schema Object for supported payloads, with explicit schema overrides when needed.

The [AsyncAPI 3.1.0 specification](https://www.asyncapi.com/docs/reference/specification/v3.1.0) and [tooling release notes](https://www.asyncapi.com/blog/release-notes-3.1.0) govern compatibility. Compatibility tests use the official JavaScript parser pinned to 3.6.6 with `@asyncapi/specs` 6.11.1. Node and the parser are test tooling.

## Producer API for M2

Types in `StaticNorth.AsyncApi.Generator` will expose these entry points.

| Entry point | Contract |
| --- | --- |
| `[AsyncApiDefinition]` | Marks a class for discovery in the current C# compilation. |
| `IAsyncApiMessageDefinition<TMessage>.Configure(IMessageBuilder<TMessage>)` | Declares message metadata and the payload schema. |
| `IAsyncApiChannelDefinition.Configure(IChannelBuilder)` | Declares a channel ID, address and channel metadata. |
| `IAsyncApiOperationDefinition.Configure(IOperationBuilder)` | Declares an operation ID, channel ID, action and message references. |
| `AsyncApiDocumentBuilder(Action<IDocumentBuilder> configure)` | Configures root metadata, including `info.title` and `info.version`. |
| `AddMessage<TMessage>(Func<IAsyncApiMessageDefinition<TMessage>> create)`, `AddChannel(Func<IAsyncApiChannelDefinition> create)`, `AddOperation(Func<IAsyncApiOperationDefinition> create)` | Registers explicit factories on the document builder. Each method returns the builder. |
| `AsyncApiDocumentBuilder.Build()` | Executes root and definition configuration, validates the resulting graph and returns a Core `AsyncApiDocument`. |
| `AsyncApiJson.Write(AsyncApiDocument)` | Serializes the document as UTF-8 JSON. |

Roslyn emits an internal `StaticNorth.AsyncApi.Generated.AsyncApiDefinitions` type per application compilation with a `public static void Register(AsyncApiDocumentBuilder builder)` method. It registers marked, accessible, concrete types with accessible parameterless constructors through strongly typed factories in fully qualified name order. Definitions requiring services can be registered explicitly with factories at the application's composition root. Factories run once per registration on each `Build()` and the caller owns the lifetime of any objects it supplies. The builder keeps no document cache. A host can cache a successful snapshot and rebuild when its configuration changes. A failed build yields no snapshot. Configuration should be deterministic and side-effect free.

Roslyn validates declarations such as inaccessible types, unsupported generic shapes and a conflicting generated entry point. The builder validates configured IDs, required metadata and references when it executes `Configure`. Each operation's message references must point to messages declared on its channel.

Reserve `SNA1xxx` for producer diagnostics and `SNA2xxx` for client diagnostics. Record assigned IDs in the Generator project's analyzer release tracking files when first emitted. M1 reserves the ranges and exercises both generators through Roslyn's driver.

## Output and hosting

An application can write `AsyncApiJson.Write(builder.Build())` to a file or stream. An explicit export command in the compiled host calls that same API in a separate process. Compilation performs discovery and registration generation only. An explicit export host is sufficient for M2. A reusable CLI remains a later developer experience decision.

An ASP.NET Core host can opt in to `MapAsyncApi`, with `/asyncapi.json` as the default route. Its implementation must preserve the worker and console dependency graph, for example through opt-in build integration for web hosts. It returns the native endpoint convention builder so the host can apply its existing authorization policies.

## Consumer boundary

The Client generator will read local JSON documents from `AdditionalFiles`. From the consuming application's perspective, a documented `send` produces a subscriber contract and a documented `receive` produces a publisher contract. Transport lifetimes and delivery semantics belong to the later runtime contracts and their adapters. Remote acquisition runs in a separate, explicitly invoked process.
