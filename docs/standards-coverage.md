# AsyncAPI standards coverage

M4 adds in-process delivery through generated clients to the AsyncAPI 3.1.0 producer and consumer workflows.

| Spec feature | Core model | Fluent builder | C# schema inference | Client codegen | Tests | Known limits |
| --- | --- | --- | --- | --- | --- | --- |
| Root version, `info.title`, `info.version`, tags and servers | Typed info and named JSON sections | Supported | — | — | Required fields, version rejection, official parser on producer export | UTF-8 JSON, exact 3.1.0 |
| Channels, parameters, operations and traits | Named JSON sections | Supported | — | Publisher/subscriber interfaces, transport implementations and channel metadata | Address validation, reference membership, both directions and delivery from producer bytes | The caller supplies concrete addresses. Traits merge and request/reply are deferred. |
| Messages, headers, examples and correlation IDs | Preserved as JSON | Supported | Typed payload and header schemas | Payload DTOs, content types, message keys and raw headers metadata | Producer export, duplicate IDs, packed client deserialization | Typed headers and correlation handling are deferred |
| Payload Schema Object and local `$ref` | Preserved as JSON | Inferred schema or explicit override | Requiredness, nullability, wire names, enums, collections and recursive objects | Objects, arrays, typed dictionaries, scalar types, string enums, nullable types and recursive object references | Official parser, generated compilation, invalid schemas and Orders round trip | Complex unions, boolean schemas and unsupported shape-changing keywords emit diagnostics |
| Extensions, security schemes and protocol bindings | Preserved as JSON | Extension methods and explicit JSON objects | — | Raw channel, operation and message bindings | Round-trip fixture and binding metadata compilation | Protocol semantics and security execution are deferred |
| Deterministic JSON | Ordinal object-key order, original array order and number spelling | — | — | — | Exact bytes, repeat serialization, duplicate member rejection | Whitespace and string escapes are normalized |
| Other AsyncAPI versions, YAML and external references | Deferred | Deferred | — | Deferred | Unsupported version fixture | Core handles JSON and never resolves references |

The Core reader checks the version, required metadata, section shapes and duplicate members. Producer builds also validate the references they create. Run an official parser on documents that use explicit JSON schemas, bindings, or other authored fields to check the entire document.

Generated DTOs preserve wire names and required-property presence. Optional properties use nullable CLR storage and omit nulls on serialization, so absent and explicit-null optional values share one representation. Enable `RespectNullableAnnotations` to enforce non-nullable reference members on deserialization. The string enum converter preserves declared output names and also accepts case-insensitive names and numeric values when reading. Constraints such as ranges and patterns remain the responsibility of schema validation. Numeric payloads map to CLR numeric types and their ranges.
