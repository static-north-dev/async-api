using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StaticNorth.AsyncApi.Tests;

namespace StaticNorth.AsyncApi.Client.Tests;

public sealed class ClientGeneratorTests
{
    [Test]
    public async Task GeneratesCompilableClientContractsWithLocalReferences()
    {
        const string json = """
            {
              "asyncapi":"3.1.0", "info":{"title":"Orders","version":"1"},
              "channels":{"orders/~":{"address":"orders.events","messages":{"order-created":{"$ref":"#/components/messages/OrderCreated"}},"bindings":{"nats":{"subject":"orders.events"}}}},
              "operations":{
                "publishOrder":{"action":"send","channel":{"$ref":"#/channels/orders~1~0"}},
                "acceptOrder":{"action":"receive","channel":{"$ref":"#/channels/orders~1~0"},"messages":[{"$ref":"#/channels/orders~1~0/messages/order-created"}]}
              },
              "components":{
                "messages":{"OrderCreated":{"payload":{"$ref":"#/components/schemas/NullableOrder"}}},
                "schemas":{
                  "NullableOrder":{"anyOf":[{"$ref":"#/components/schemas/OrderCreated"},{"type":"null"}]},
                  "OrderCreated":{"type":"object","required":["order-id","status"],"properties":{
                  "order-id":{"type":"string","format":"uuid"}, "status":{"enum":["open","in-progress"]},
                  "next":{"$ref":"#/components/schemas/NullableOrder"},
                  "items":{"type":"array","items":{"type":"integer"}}
                }}}
              }
            }
            """;
        var result = Generate(new ContractFile("orders.asyncapi.json", json), out var output);
        var source = result.GeneratedTrees
            .Single()
            .ToString();

        await Assert
            .That(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            .IsEmpty();
        await Assert
            .That(source.Contains("IPublishOrderSubscriber", StringComparison.Ordinal) &&
                  source.Contains("IAcceptOrderPublisher", StringComparison.Ordinal) &&
                  source.Contains("OrderCreated? Next", StringComparison.Ordinal) &&
                  source.Contains("ChannelBindingsJson", StringComparison.Ordinal) &&
                  source.Contains("JsonPropertyName(\"order-id\")", StringComparison.Ordinal) &&
                  source.Contains("JsonStringEnumMemberName(\"in-progress\")", StringComparison.Ordinal))
            .IsTrue();
    }

    [Test]
    public async Task ReportsCollidingDocumentSymbols()
    {
        const string json = """
            {"asyncapi":"3.1.0","info":{"title":"Orders","version":"1"},
             "channels":{"orders":{}},"operations":{"send":{"action":"send","channel":{"$ref":"#/channels/orders"}}}}
            """;
        var result = Generate(out _, new ContractFile("first/orders.asyncapi.json", json),
            new ContractFile("second/orders.asyncapi.json", json));
        var diagnostic = result.Diagnostics.Single();

        await Assert
            .That(diagnostic.Id)
            .IsEqualTo("SNA2003");
        await Assert
            .That(diagnostic.Location.GetLineSpan().Path)
            .IsEqualTo("second/orders.asyncapi.json");
    }

    [Test]
    public async Task ReusesOutputForUnrelatedEditsAndDetectsNewTypeConflicts()
    {
        const string json = """
            {"asyncapi":"3.1.0","info":{"title":"Orders","version":"1"},
             "channels":{"orders":{}},"operations":{"send":{"action":"send","channel":{"$ref":"#/channels/orders"}}}}
            """;
        var compilation = CSharpCompilation.Create("Consumer");
        var generator = GeneratorTestAssembly.Create("ClientGenerator");
        var driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            [new ContractFile("orders.asyncapi.json", json)],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        var initial = driver.RunGenerators(compilation);
        var unrelated = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace Other; class Edit {}"));
        var edited = initial.RunGenerators(unrelated);
        var result = edited.GetRunResult();
        var generatorResult = result.Results.Single();
        var step = generatorResult.TrackedSteps["ClientDocuments"].Single();

        await Assert
            .That(step.Outputs.Single().Reason)
            .IsEqualTo(IncrementalStepRunReason.Cached);

        var conflicting = unrelated.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace AsyncApi.Generated.Orders; class SendMetadata {}"));
        var updated = edited
            .RunGenerators(conflicting)
            .GetRunResult();

        await Assert
            .That(updated.Diagnostics.Single().Id)
            .IsEqualTo("SNA2003");
        await Assert
            .That(updated.GeneratedTrees)
            .IsEmpty();
    }

    [Test]
    [Arguments("a/parent.asyncapi.json", "b/child.asyncapi.json")]
    [Arguments("b/parent.asyncapi.json", "a/child.asyncapi.json")]
    public async Task ReportsGeneratedTypeAndNamespaceConflictsInBothOrders(string parentPath, string childPath)
    {
        const string json = """
            {"asyncapi":"3.1.0","info":{"title":"Orders","version":"1"},
             "channels":{"orders":{}},"operations":{"send":{"action":"send","channel":{"$ref":"#/channels/orders"}}}}
            """;
        var generator = GeneratorTestAssembly.Create("ClientGenerator");
        var driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            [new ContractFile(parentPath, json), new ContractFile(childPath, json)],
            optionsProvider: new NamespaceOptionsProvider(parentPath, childPath));
        var compilation = CSharpCompilation.Create("Consumer");
        var result = driver
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert
            .That(result.Diagnostics.Single().Id)
            .IsEqualTo("SNA2003");
        await Assert
            .That(result.Diagnostics.Single().Location.GetLineSpan().Path)
            .IsEqualTo(string.CompareOrdinal(parentPath, childPath) < 0 ? childPath : parentPath);
    }

    [Test]
    [Arguments("{\"type\":[\"string\",\"integer\"]}", "SNA2002")]
    [Arguments("{\"$ref\":\"external.json#/example\"}", "SNA2002")]
    [Arguments("{\"$ref\":\"#/missing\"}", "SNA2001")]
    [Arguments("{\"$ref\":\"#/invalid%ZZ\"}", "SNA2001")]
    [Arguments("{\"type\":\"array\",\"items\":{\"$ref\":\"#/channels/orders/messages/item/payload\"}}", "SNA2002")]
    public async Task RejectsUnsupportedOrInvalidPayloads(string payload, string diagnosticId)
    {
        var json = $$$$$"""
            {
              "asyncapi":"3.1.0", "info":{"title":"Orders","version":"1"},
              "channels":{"orders":{"messages":{"item":{"payload":{{{{{payload}}}}}}}}},
              "operations":{"send":{"action":"send","channel":{"$ref":"#/channels/orders"}}}
            }
            """;
        var result = Generate(new ContractFile("orders.asyncapi.json", json), out _);

        await Assert
            .That(result.Diagnostics.Single().Id)
            .IsEqualTo(diagnosticId);
        await Assert
            .That(result.GeneratedTrees)
            .IsEmpty();
    }

    [Test]
    [Arguments("[{\"$ref\":\"#/channels/other/messages/item\"}]", "SNA2001")]
    [Arguments("[]", null)]
    public async Task UsesTheExactOperationChannelMessageKeys(string selection, string? diagnosticId)
    {
        var json = $$$$$"""
            {
              "asyncapi":"3.1.0", "info":{"title":"Orders","version":"1"},
              "channels":{
                "orders":{"messages":{"item":{"payload":{"type":"string"}}}},
                "other":{"messages":{"item":{"payload":{"type":"string"}}}}
              },
              "operations":{"send":{"action":"send","channel":{"$ref":"#/channels/orders"},"messages":{{{{{selection}}}}}}}
            }
            """;
        var result = Generate(new ContractFile("orders.asyncapi.json", json), out _);

        if (diagnosticId is not null)
        {
            await Assert
                .That(result.Diagnostics.Single().Id)
                .IsEqualTo(diagnosticId);
            return;
        }

        var source = result.GeneratedTrees
            .Single()
            .ToString();
        await Assert
            .That(source.Contains("SubscribeItemAsync", StringComparison.Ordinal))
            .IsFalse();
    }

    [Test]
    [Arguments("{", "SNA2001")]
    [Arguments("{\"asyncapi\":\"3.0.0\"}", "SNA2002")]
    [Arguments("{\"asyncapi\":\"3.1.0\",\"asyncapi\":\"3.1.0\"}", "SNA2001")]
    public async Task ReportsInvalidContractsOnTheAdditionalFile(string json, string diagnosticId)
    {
        var result = Generate(new ContractFile("broken.asyncapi.json", json), out _);
        var diagnostic = result.Diagnostics.Single();

        await Assert
            .That(diagnostic.Id)
            .IsEqualTo(diagnosticId);
        await Assert
            .That(diagnostic.Location.GetLineSpan().Path)
            .IsEqualTo("broken.asyncapi.json");
        await Assert
            .That(result.GeneratedTrees)
            .IsEmpty();
    }

    [Test]
    public async Task RunsAgainstACompilationWithoutAdditionalFiles()
    {
        var compilation = CSharpCompilation.Create("Consumer");
        var generator = GeneratorTestAssembly.Create("ClientGenerator");
        var driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
        var result = driver
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert
            .That(result.Diagnostics)
            .IsEmpty();
        await Assert
            .That(result.GeneratedTrees)
            .IsEmpty();
    }

    private static GeneratorDriverRunResult Generate(AdditionalText file, out Compilation output)
    {
        return Generate(out output, file);
    }

    private static GeneratorDriverRunResult Generate(out Compilation output, params AdditionalText[] files)
    {
        var platformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var references = platformAssemblies
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Consumer", references: references, options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var generator = GeneratorTestAssembly.Create("ClientGenerator");
        var driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], files);

        return driver
            .RunGeneratorsAndUpdateCompilation(compilation, out output, out _)
            .GetRunResult();
    }
}
