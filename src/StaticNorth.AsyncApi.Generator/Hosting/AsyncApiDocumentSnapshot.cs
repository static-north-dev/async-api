using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using StaticNorth.AsyncApi.Core;

namespace StaticNorth.AsyncApi.Generator;

internal sealed class AsyncApiDocumentSnapshot(AsyncApiDocument document)
{
    private FileContentHttpResult _response = Serialize(document);

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
