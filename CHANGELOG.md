# Changelog

## Unreleased — M4 transport

- Added the transport interface, message route, and envelope in Core.
- Generated JSON publisher and subscriber implementations backed by a supplied transport.
- Added an in-memory transport and verified that producer-serialized Orders reach a generated subscriber using the packed Client package.

## Unreleased — M3 client

- Generate DTOs, publisher and subscriber interfaces, and binding metadata from local AsyncAPI 3.1.0 JSON contracts.
- Support per-file namespace, alias, model prefix, and direction settings. Report malformed documents, unsupported schemas, and generated symbol conflicts during compilation.
- Verify that a client built from the packed NuGets can consume the producer's exported Orders document.

## Unreleased — M2 producer

- Added annotated message, channel, and operation definitions with generated registration.
- Added runtime document builders, reference validation, and payload schema inference from `System.Text.Json`.
- Added an Orders export example with official parser and packed NuGet verification.

## 0.1.0-alpha.1 — M1 foundations

- Pinned the .NET 11.0.100 SDK and adopted Larder's C# formatting and build conventions.
- Added the Core, Generator, and Client projects. Generator builds both source generators into one analyzer assembly.
- Added deterministic AsyncAPI 3.1.0 JSON reading and writing, with official parser compatibility tests.
- Recorded the producer registration and export decisions for M2.
