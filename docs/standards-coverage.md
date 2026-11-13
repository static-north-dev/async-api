# AsyncAPI standards coverage

M1 covers the JSON foundation for AsyncAPI 3.1.0. The draft names S0 and S1 without defining them, so this matrix records the tested M1 surface. Each later milestone can extend these rows as its own fixtures and compilation tests pass.

| Spec feature | Core model | Fluent builder | C# schema inference | Client codegen | Tests | Known limits |
| --- | --- | --- | --- | --- | --- | --- |
| Root version, `info.title`, `info.version` | Typed | M2 | — | — | Required fields, version rejection, official parser | UTF-8 JSON, exact 3.1.0 |
| Servers, channels, operations and components | Named JSON sections | M2 | — | M3 | Section shape, orders fixture, invalid action fixture | Section contents are opaque to Core |
| Messages, payload Schema Object and local `$ref` | Preserved as JSON | M2 | M2 | M3 | Orders fixture with channel and message references | No Core ref resolution, dialect interpretation or payload validation |
| Extensions and protocol bindings | Preserved as JSON | M2 and later | — | Deferred | Round-trip fixture and official parser on root extension | Binding-specific semantics are outside M1 |
| Deterministic JSON | Ordinal object-key order, original array order and number spelling | — | — | — | Exact bytes, repeat serialization, duplicate member rejection | Whitespace and string escapes are normalized |
| Other AsyncAPI versions, YAML and external references | Deferred | Deferred | — | Deferred | Unsupported version fixture | Core handles JSON and never resolves references |

The Core reader checks the version, required metadata, section shapes and duplicate members. The official parser tests cover compatibility for the fixtures in this repository. A document accepted by Core may still fail the official parser.
