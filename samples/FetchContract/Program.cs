using StaticNorth.AsyncApi.Client.Acquisition;

try
{
    if (args is ["offline", var path, var checksum])
    {
        await SpecificationFetcher.ReadOfflineAsync(path, checksum);
        return 0;
    }

    if (args.Length is 3 or 4 && args[0] == "refresh" && Uri.TryCreate(args[1], UriKind.Absolute, out var source))
    {
        var token = Environment.GetEnvironmentVariable("ASYNCAPI_SPEC_TOKEN");
        var authentication = string.IsNullOrEmpty(token) ? null : new EnvironmentTokenProvider(token);
        using var fetcher = new SpecificationFetcher();
        await fetcher.RefreshAsync(source, args[2], authentication, args.Length == 4 ? args[3] : null);
        return 0;
    }
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine("Could not acquire the AsyncAPI contract. Check the source, destination, credentials, and checksum.");
    return 1;
}

Console.Error.WriteLine("Usage: FetchContract refresh <url> <destination> [sha256] | offline <path> <sha256>");
return 2;
