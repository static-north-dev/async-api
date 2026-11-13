namespace StaticNorth.AsyncApi.Generator;

/// <summary>Defines a operation in a code-first AsyncAPI document.</summary>
public interface IAsyncApiOperationDefinition
{
    /// <summary>Configures the operation during document construction.</summary>
    /// <param name="operation">The builder for the operation.</param>
    void Configure(IOperationBuilder operation);
}
