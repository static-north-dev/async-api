import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const configuration = process.env.ASYNCAPI_TEST_CONFIGURATION ?? 'Debug';
const prefix = 'StaticNorth.AsyncApi.';
const analyzer = `analyzers/dotnet/cs/${prefix}Generator.Analyzers.dll`;

function dotnet(cwd, ...args) {
    return execFileSync('dotnet', args, { cwd, encoding: 'utf8' });
}

function verifyConsumer(directory, feed, cache, version, references) {
    mkdirSync(directory);
    const project = 'Smoke.csproj';
    const packageReferences = references.map(name =>
        `    <PackageReference Include="${prefix}${name}" Version="${version}" />`);
    const clientOnly = references.length === 1 && references[0] === 'Client';
    const combined = references.length === 2;
    const contractItem = clientOnly
        ? '    <AdditionalFiles Include="orders.asyncapi.json" AsyncApiNamespace="Smoke.Contracts" AsyncApiModelPrefix="Wire" AsyncApiGenerate="subscribers" />'
        : combined ? '    <AdditionalFiles Include="orders.asyncapi.json" AsyncApiNamespace="Smoke.Contracts" AsyncApiModelPrefix="Wire" />' : '';

    writeFileSync(join(directory, project), `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
${packageReferences.join('\n')}
${contractItem}
  </ItemGroup>
</Project>
`);
    const source = clientOnly || combined ? `
using Smoke.Contracts;
using StaticNorth.AsyncApi.Client.Transport;
using StaticNorth.AsyncApi.Core.Transport;
${combined ? 'using StaticNorth.AsyncApi.Generated;\nusing StaticNorth.AsyncApi.Generator;' : ''}

var transport = new InMemoryAsyncApiTransport();
IPublishOrderCreatedSubscriber subscriber = new PublishOrderCreatedSubscriber(transport, "orders.eu");
WireOrderCreated? received = null;
await using var subscription = await subscriber.SubscribeOrderCreatedAsync((message, _) =>
{
    received = message;
    return System.Threading.Tasks.ValueTask.CompletedTask;
});
var bytes = await System.IO.File.ReadAllBytesAsync("order-created.json");
var route = new AsyncApiMessageRoute("orders.eu", "orderCreated", "producer", "application/json");
await transport.PublishAsync(new AsyncApiEnvelope(route, bytes));
var order = received ?? throw new System.InvalidOperationException("No order was delivered.");
System.Console.WriteLine($"{order.OrderId}|{order.Status}|{order.Items[0].Sku}|{order.Source}");
${combined ? `
var builder = new AsyncApiDocumentBuilder(document => document.Info("Smoke", "1.0.0"));
AsyncApiDefinitions.Register(builder);
var document = builder.Build();
if (document.Components is null)
{
    throw new System.InvalidOperationException("The producer definition was not registered.");
}

IAcceptOrderPublisher publisher = new AcceptOrderPublisher(transport, "orders.eu");
received = null;
await publisher.PublishOrderCreatedAsync(order);
System.Console.WriteLine(received?.OrderId == order.OrderId ? "publisher delivered" : "publisher failed");

[AsyncApiDefinition]
internal sealed class SmokeDefinition : IAsyncApiMessageDefinition<string>
{
    public void Configure(IMessageBuilder<string> message)
    {
        message.Name("smoke");
    }
}
` : ''}
` : `
using StaticNorth.AsyncApi.Core;
using StaticNorth.AsyncApi.Generated;
using StaticNorth.AsyncApi.Generator;

var builder = new AsyncApiDocumentBuilder(document => document.Info("Smoke", "1.0.0"));
AsyncApiDefinitions.Register(builder);
System.Console.WriteLine(System.Text.Encoding.UTF8.GetString(AsyncApiJson.Write(builder.Build())));

[AsyncApiDefinition]
internal sealed class SmokeDefinition : IAsyncApiMessageDefinition<string>
{
    public void Configure(IMessageBuilder<string> message)
    {
        message.Name("smoke");
    }
}
`;
    writeFileSync(join(directory, 'Program.cs'), source);

    if (clientOnly || combined) {
        const json = dotnet(root, 'run', '--no-build', '--configuration', configuration, '--project', 'samples/Producer', '--', 'export');
        const contract = JSON.parse(json);
        contract.operations.acceptOrder = {
            action: 'receive', channel: { $ref: clientOnly ? 'external.json#/channels/orders' : '#/channels/orders' },
        };
        writeFileSync(join(directory, 'orders.asyncapi.json'), JSON.stringify(contract));
        const payload = dotnet(root, 'run', '--no-build', '--configuration', configuration, '--project', 'samples/Producer', '--', 'payload');
        writeFileSync(join(directory, 'order-created.json'), payload);
    }
    dotnet(directory, 'restore', project, '--source', feed, '--packages', cache);
    dotnet(directory, 'build', project, '--no-restore', '--disable-build-servers');

    const assets = JSON.parse(readFileSync(join(directory, 'obj', 'project.assets.json'), 'utf8'));
    const libraries = assets.targets['net11.0'];
    const expectedPackages = references.includes('Client') ? ['Client', 'Core', 'Generator'] : ['Core', 'Generator'];
    assert.deepEqual(Object.keys(libraries).sort(), expectedPackages.map(name => `${prefix}${name}/${version}`));
    assert.deepEqual(libraries[`${prefix}Core/${version}`].dependencies ?? {}, {});
    assert.deepEqual(libraries[`${prefix}Generator/${version}`].dependencies, { [`${prefix}Core`]: version });

    const buildItems = JSON.parse(dotnet(directory, 'msbuild', project, '-target:ResolveReferences', '-getItem:Analyzer'));
    const analyzers = buildItems.Items.Analyzer.map(item => item.Identity.replaceAll('\\', '/'));
    assert.equal(analyzers.filter(path => path.endsWith(`/${analyzer}`)).length, 1);

    assert.deepEqual(assets.libraries[`${prefix}Generator/${version}`].files.filter(path => path.endsWith('.dll')), [
        analyzer,
        `lib/net11.0/${prefix}Generator.dll`,
    ]);

    if (references.includes('Client')) {
        assert.deepEqual(assets.libraries[`${prefix}Client/${version}`].files.filter(path => path.endsWith('.dll')), [
            `lib/net11.0/${prefix}Client.dll`,
        ]);
        assert.deepEqual(libraries[`${prefix}Client/${version}`].dependencies, {
            [`${prefix}Core`]: version,
            [`${prefix}Generator`]: version,
        });
    }

    const runtimeFiles = readdirSync(join(directory, 'bin', 'Debug', 'net11.0'));
    assert.equal(runtimeFiles.includes(`${prefix}Generator.dll`), references.includes('Generator'));
    assert.ok(!runtimeFiles.includes(`${prefix}Generator.Analyzers.dll`));

    if (!clientOnly && !combined) {
        const output = dotnet(directory, 'bin/Debug/net11.0/Smoke.dll');
        const document = JSON.parse(output);
        assert.equal(document.components.messages.smoke.name, 'smoke');
        const runtimeConfig = JSON.parse(readFileSync(join(directory, 'bin/Debug/net11.0/Smoke.runtimeconfig.json'), 'utf8'));
        assert.equal(runtimeConfig.runtimeOptions.framework.name, 'Microsoft.NETCore.App');

        const webProject = readFileSync(join(directory, project), 'utf8')
            .replace('Microsoft.NET.Sdk', 'Microsoft.NET.Sdk.Web')
            .replace('<Nullable>enable</Nullable>', '<Nullable>enable</Nullable><AsyncApiEnableAspNetCore>true</AsyncApiEnableAspNetCore>');
        writeFileSync(join(directory, project), webProject);
        writeFileSync(join(directory, 'Program.cs'), `
using Microsoft.AspNetCore.Builder;
using StaticNorth.AsyncApi.Generator;

var host = WebApplication.CreateBuilder(args);
host.Services.AddAsyncApi(_ => new AsyncApiDocumentBuilder(info => info.Info("Packed", "1.0.0")));
var app = host.Build();
app.MapAsyncApi().RequireAuthorization("InternalApi");
var snapshot = new AsyncApiDocumentSnapshot(new AsyncApiDocumentBuilder(info => info.Info("Original", "1.0.0")).Build());
app.MapAsyncApiSnapshot(snapshot, "/snapshot.json").RequireAuthorization("InternalApi");
var before = snapshot.GetResponse();
snapshot.Update(new AsyncApiDocumentBuilder(info => info.Info("Updated", "1.0.0")).Build());
var after = snapshot.GetResponse();
if (ReferenceEquals(before, after) || !ReferenceEquals(after, snapshot.GetResponse()) ||
    StaticNorth.AsyncApi.Core.AsyncApiJson.Read(before.FileContents).Info.Title != "Original" ||
    StaticNorth.AsyncApi.Core.AsyncApiJson.Read(after.FileContents).Info.Title != "Updated")
{
    throw new System.InvalidOperationException("The cached response did not update atomically.");
}
`);
        dotnet(directory, 'restore', project, '--source', feed, '--packages', cache);
        dotnet(directory, 'build', project, '--no-restore', '--disable-build-servers');
        dotnet(directory, 'bin/Debug/net11.0/Smoke.dll');
    }

    if (clientOnly || combined) {
        const output = dotnet(directory, 'bin/Debug/net11.0/Smoke.dll');
        const delivery = 'dc572c8c-131a-40d5-8d37-091b02971883|Created|ABC-123|checkout';
        assert.deepEqual(output.trim().split(/\r?\n/), combined ? [delivery, 'publisher delivered'] : [delivery]);
    }
}

test('The producer and client packages share one compiler analyzer', () => {
    const directory = mkdtempSync(join(tmpdir(), 'asyncapi-pack-'));
    const feed = join(directory, 'feed');
    const cache = join(directory, 'packages');

    try {
        dotnet(root, 'pack', 'StaticNorth.AsyncApi.slnx', '--configuration', configuration, '--no-build', '--no-restore',
            '--disable-build-servers', '-m:1', '--output', feed);
        const packages = readdirSync(feed).sort();
        const version = packages.find(name => name.startsWith(`${prefix}Core.`))
            ?.slice(`${prefix}Core.`.length, -'.nupkg'.length);
        assert.ok(version);
        assert.deepEqual(packages, ['Client', 'Core', 'Generator'].map(name => `${prefix}${name}.${version}.nupkg`));

        writeFileSync(join(directory, 'global.json'), readFileSync(join(root, 'global.json')));
        verifyConsumer(join(directory, 'producer'), feed, cache, version, ['Generator']);
        verifyConsumer(join(directory, 'client'), feed, cache, version, ['Client']);
        verifyConsumer(join(directory, 'combined'), feed, cache, version, ['Client', 'Generator']);
    } finally {
        rmSync(directory, { recursive: true, force: true });
    }
});
