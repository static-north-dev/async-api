# StaticNorth.AsyncApi

A transport-agnostic AsyncAPI toolchain for .NET. M1 lays out the Core, Generator, and Client NuGet packages and adds a deterministic AsyncAPI 3.1.0 JSON codec. The Generator project contains both Roslyn scaffolds. Definition discovery, typed document authoring, and client code generation are future milestones.

## Build and verify

Use .NET SDK 11.0.100 and Node.js 24 or newer for the compatibility tests. Run these commands from the repository root.

```sh
dotnet build StaticNorth.AsyncApi.slnx
dotnet test --solution StaticNorth.AsyncApi.slnx --no-build --no-restore
npm --prefix tests/compatibility ci --ignore-scripts
npm --prefix tests/compatibility test
```

The solution builds both console samples. The compatibility suite checks Core's JSON output with the official parser and packs the three NuGet packages. It verifies that producer, client, and combined applications each resolve the shared analyzer once.

## Try the M1 samples

```sh
dotnet run --no-build --project samples/Producer -- samples/Contracts/orders.asyncapi.json
dotnet run --no-build --project samples/Consumer
```

The first command round-trips the bundled contract to standard output. The second builds with the same local contract as an `AdditionalFiles` item and prints the supported version.

[Architecture decision](docs/architecture.md) · [Standards coverage and limits](docs/standards-coverage.md) · [Changelog](CHANGELOG.md) · [Apache-2.0 license](LICENSE)
