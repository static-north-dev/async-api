# Repository instructions

## Code formatting and readability

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

## Code index

After every repository change, update the Graphify code index from the repository root with `graphify update . --force`. Run this after the final edit and before reporting completion so `graphify-out/graph.json` reflects the current source code. The generated `graphify-out/` directory is local and ignored by Git.
