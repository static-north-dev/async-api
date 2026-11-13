import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
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
    const contractItem = clientOnly
        ? '    <AdditionalFiles Include="orders.asyncapi.json" AsyncApiNamespace="Smoke.Contracts" AsyncApiModelPrefix="Wire" AsyncApiGenerate="subscribers" />'
        : '';

    writeFileSync(join(directory, project), `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
${packageReferences.join('\n')}
${contractItem}
  </ItemGroup>
</Project>
`);
    const source = clientOnly ? `
using Smoke.Contracts;

var json = System.IO.File.ReadAllText("orders.asyncapi.json");
using var document = System.Text.Json.JsonDocument.Parse(json);
var example = document.RootElement.GetProperty("components").GetProperty("messages").GetProperty("OrderCreated").GetProperty("examples")[0].GetProperty("payload");
var options = new System.Text.Json.JsonSerializerOptions { RespectNullableAnnotations = true };
var message = System.Text.Json.JsonSerializer.Deserialize<WireOrderCreated>(example.GetRawText(), options)!;
System.Console.WriteLine($"{typeof(IPublishOrderCreatedSubscriber).Name}|{PublishOrderCreatedMetadata.ChannelAddress}|{message.Status}|{message.Items[0].Sku}");
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

    if (clientOnly) {
        const json = dotnet(root, 'run', '--no-build', '--project', 'samples/Producer', '--', 'export');
        const contract = JSON.parse(json);
        contract.operations.unsupportedPublish = { action: 'receive', channel: { $ref: 'external.json#/channels/orders' } };
        writeFileSync(join(directory, 'orders.asyncapi.json'), JSON.stringify(contract));
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

    if (references.includes('Generator')) {
        const output = dotnet(directory, 'bin/Debug/net11.0/Smoke.dll');
        const document = JSON.parse(output);
        assert.equal(document.components.messages.smoke.name, 'smoke');
    }

    if (clientOnly) {
        const output = dotnet(directory, 'bin/Debug/net11.0/Smoke.dll');
        assert.equal(output.trim(), 'IPublishOrderCreatedSubscriber|orders.{region}|Created|ABC-123');
    }
}

test('The producer and client packages share one compiler analyzer', () => {
    const directory = mkdtempSync(join(tmpdir(), 'asyncapi-pack-'));
    const feed = join(directory, 'feed');
    const cache = join(directory, 'packages');

    try {
        dotnet(root, 'pack', 'StaticNorth.AsyncApi.slnx', '--configuration', 'Debug', '--no-build', '--no-restore',
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
