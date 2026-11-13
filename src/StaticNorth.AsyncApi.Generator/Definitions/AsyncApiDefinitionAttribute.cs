namespace StaticNorth.AsyncApi.Generator;

/// <summary>Marks a definition class for discovery by the AsyncAPI producer generator.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AsyncApiDefinitionAttribute : Attribute;
