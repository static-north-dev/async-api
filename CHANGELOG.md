# Changelog

## 0.1.0-alpha.1

The first prerelease targets .NET 11 and AsyncAPI 3.1.0 JSON. It includes the `StaticNorth.AsyncApi.Core`, `StaticNorth.AsyncApi.Generator`, and `StaticNorth.AsyncApi.Client` NuGet packages.

### Foundations (M1)

- Core reads and writes deterministic AsyncAPI 3.1.0 JSON. The compatibility suite checks generated documents with the official AsyncAPI parser.
- Generator ships one analyzer assembly for producer and client generation. The repository pins .NET SDK 11.0.100.

### Producer (M2)

- Annotate message, channel, and operation definitions to generate registration code. The runtime builder executes those definitions, validates their references, and infers payload schemas from C# types using `System.Text.Json` metadata.
- Export the document explicitly from the producer. The Orders sample demonstrates this workflow.

### Client generation (M3)

- Generate DTOs, typed publisher and subscriber interfaces, and binding metadata from a local AsyncAPI 3.1.0 JSON document. Configure namespace, alias, model prefix, and operation direction per input file.
- Compiler diagnostics identify malformed documents, unsupported schemas, and generated symbol conflicts.

### Transport (M4)

- Core defines the transport interface, message route, and envelope. Generated publishers and subscribers serialize and deserialize JSON through a supplied transport.
- The in-memory adapter supports local testing. The packed-package test delivers a producer-serialized Orders event to a generated subscriber.

### Hosting and contract acquisition (M5)

- Opt in to an ASP.NET Core JSON endpoint with a configurable route and native host authorization. The endpoint builds a fresh document per request. Hosts can cache exported snapshots separately.
- Fetch remote contracts explicitly with optional custom authentication, or verify a local copy offline against a SHA-256 checksum. Conditional HTTP requests and automatic cache fallback are deferred.
- CI builds and packs all three NuGets. A manually dispatched workflow supports NuGet Trusted Publishing.

### Documentation viewer (M6)

- The ASP.NET Core sample includes an opt-in, self-hosted AsyncAPI React viewer. It displays the live document through the existing authorization policy. React and Node dependencies are confined to the sample tooling.

See the [standards coverage matrix](docs/standards-coverage.md) for supported schemas and reference handling. The in-memory adapter provides in-process delivery. Production broker adapters and broker-specific delivery guarantees are outside this release.
