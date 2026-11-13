# AsyncAPI standards coverage

M2 covers code-first authoring and JSON export for AsyncAPI 3.1.0. Each later milestone can extend these rows as its fixtures and compilation tests pass.

| Spec feature | Core model | Fluent builder | C# schema inference | Client codegen | Tests | Known limits |
| --- | --- | --- | --- | --- | --- | --- |
| Root version, `info.title`, `info.version`, tags and servers | Typed info and named JSON sections | Supported | — | — | Required fields, version rejection, official parser on producer export | UTF-8 JSON, exact 3.1.0 |
| Channels, parameters, operations and traits | Named JSON sections | Supported | — | M3 | Invalid action fixture, address validation, local reference and membership tests | Parameters and traits accept explicit JSON objects |
| Messages, headers, examples and correlation IDs | Preserved as JSON | Supported | Typed payload and header schemas | M3 | Producer export, duplicate IDs, explicit schema snapshots | Header examples accept explicit JSON objects |
| Payload Schema Object and local `$ref` | Preserved as JSON | Inferred schema or explicit override | Requiredness, nullability, wire names, enums, collections and recursive objects | M3 | Official parser and schema assertions on the Orders export | Custom converters, opaque types and polymorphism need explicit schemas. Serializer metadata limits nullability inference for generic elements. |
| Extensions, security schemes and protocol bindings | Preserved as JSON | Extension methods and explicit JSON objects | — | Deferred | Round-trip fixture and official parser on root extension and security scheme | Protocol semantics and full validation of authored JSON fields are outside the builder |
| Deterministic JSON | Ordinal object-key order, original array order and number spelling | — | — | — | Exact bytes, repeat serialization, duplicate member rejection | Whitespace and string escapes are normalized |
| Other AsyncAPI versions, YAML and external references | Deferred | Deferred | — | Deferred | Unsupported version fixture | Core handles JSON and never resolves references |

The Core reader checks the version, required metadata, section shapes and duplicate members. Producer builds also validate the references they create. Run an official parser on documents that use explicit JSON schemas, bindings, or other authored fields to check the entire document.
