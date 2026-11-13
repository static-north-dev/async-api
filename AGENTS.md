# Repository instructions

## Code formatting and readability

- Group related files in folders by feature or responsibility. Keep a feature's implementation, supporting types, and related tests together within their respective projects. Use descriptive folder names that reflect the code's purpose.
- Always format code you add or modify using the repository's formatting and style conventions. For C#, use `dotnet format` with the relevant project or solution and `--include` paths.
- Use explicit, structured curly braces for every control-flow body, including single-statement `if`, `else`, loop, `using`, `try`, and `catch` blocks. In C#, place braces and their contents on separate, properly indented lines.
- Separate guard clauses, conditional blocks, loops, exception handling, and other logical sections with blank lines. Group closely related declarations where that improves readability.
- Avoid multiple executable statements on one line. Wrap long method signatures, arguments, conditions, queries, and chained calls so the code is easy to scan without horizontal scrolling.
- Put each call in a method chain on its own line, including the first call after the receiver. Write `builder` on the first line, followed by `.MapPost(...)` and `.RequireAuthorization(...)` on separate indented lines.
- Apply the same fluent-chain formatting to validators. Put `context` on its own line, followed by `.Property(...)` and each validation rule on separate lines.
- Prefer block-bodied methods and lambdas when an expression contains nested calls, asynchronous work, or multiple operations. Use named intermediate variables and separate statements when they make the flow easier to read.
- Avoid intermediate variables that only rename a property or immediately return an unchanged result.
- In ASP.NET Core integration, map endpoints to separate named handler methods. Explicitly annotate route, query, body, and injected-service parameters with their binding attributes. Framework-supplied parameters such as `HttpContext` and `CancellationToken` remain unannotated. Use named results and return responses in separate statements.
- Apply these rules to existing code you change as well as new code. Review spacing, wrapping, and braces after automated formatting.

## Performance and allocations

- Optimize the common paths for publishing, receiving, serving contracts, and incremental generation. Measure throughput, latency, and allocated bytes per operation in Release on the pinned .NET SDK with representative payloads and concurrency. Measure cold startup and steady state separately when both matter. Set an allocation budget for hot paths and verify zero managed allocations where the operation can reuse existing data.
- Use native async APIs for network, file, and response I/O. Pass cancellation through reads, writes, and delivery callbacks. Avoid blocking waits, `Task.Run` wrappers for I/O, and holding locks across `await`. Keep CPU-only operations synchronous and forward existing awaitables directly when no cleanup or error handling is required. Use `ValueTask` on measured hot paths that often complete synchronously, and consume each instance once.
- Avoid repeated metadata construction, copies, materialization, boxing, and captured lambdas on hot paths. Prefer spans for synchronous buffer access, memory for buffers that cross `await`, and caller-provided destinations or `IBufferWriter<T>` for output where the API permits it. Use bounded `stackalloc` for small scratch buffers and pool larger buffers when measurements justify it. Return rentals on every exit path after their last consumer finishes, and clear sensitive data before returning them. Preserve public snapshot and ownership guarantees.
- Reuse immutable routes, serializer options, and JSON metadata when their configuration is stable. Prefer source-generated `System.Text.Json` metadata for known types where it preserves caller options and wire behavior. Keep caches bounded or tied to an explicit lifetime and invalidation policy. Preserve deterministic output, delivery order, and cancellation semantics when optimizing.
- Use current stable C# features and .NET 11 APIs supported by the pinned SDK when they improve readability or performance. Check the target of each project: the Roslyn analyzer ships as `netstandard2.0`, so analyzer code must use APIs available to that target and its compiler host. Keep generator inputs small and equatable so unrelated edits can reuse cached work. Compile and test emitted code as part of generator changes.

## Code index

After every repository change, update the Graphify code index from the repository root with `graphify update . --force`. Run this after the final edit and before reporting completion so `graphify-out/graph.json` reflects the current source code. The generated `graphify-out/` directory is local and ignored by Git.
