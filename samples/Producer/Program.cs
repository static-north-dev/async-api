using StaticNorth.AsyncApi.Core;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: Producer <path to AsyncAPI JSON>");
    return 1;
}

var input = await File.ReadAllBytesAsync(args[0]);
var document = AsyncApiJson.Read(input);

using (var output = Console.OpenStandardOutput())
{
    await output.WriteAsync(AsyncApiJson.Write(document));
}

return 0;
