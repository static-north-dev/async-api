using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using StaticNorth.AsyncApi.Core;

namespace StaticNorth.AsyncApi.Generator;

/// <summary>Holds a serialized AsyncAPI document for repeated HTTP responses.</summary>
/// <param name="document">The initial document to serialize.</param>
internal sealed class AsyncApiDocumentSnapshot(AsyncApiDocument document)
{
    private FileContentHttpResult _response = Serialize(document);

    /// <summary>Serializes a new document and atomically replaces the snapshot for future responses.</summary>
    /// <param name="document">The updated document.</param>
    public void Update(AsyncApiDocument document)
    {
        var response = Serialize(document);
        Volatile.Write(ref _response, response);
    }

    internal FileContentHttpResult GetResponse()
    {
        return Volatile.Read(ref _response);
    }

    private static FileContentHttpResult Serialize(AsyncApiDocument document)
    {
        return TypedResults.Bytes(AsyncApiJson.Write(document), "application/json");
    }
}
