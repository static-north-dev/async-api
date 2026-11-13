import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { cp, mkdtemp, readFile, rm, symlink, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { Parser } from '@asyncapi/parser';

const cli = fileURLToPath(new URL('../../tools/bundle-contract/cli.mjs', import.meta.url));
const fixture = fileURLToPath(new URL('fixtures/bundling/split/', import.meta.url));

function bundle(input, output) {
    const result = spawnSync(process.execPath, [cli, input, output], { encoding: 'utf8' });
    assert.ifError(result.error);
    return result;
}

test('The split YAML bundles reproducibly and both contracts pass the official parser', async t => {
    const directory = await mkdtemp(join(tmpdir(), 'asyncapi-bundle-'));
    t.after(() => rm(directory, { recursive: true, force: true }));
    const copied = join(directory, 'copied');
    await cp(fixture, copied, { recursive: true });
    const output = join(directory, 'orders.asyncapi.json');
    const secondOutput = join(directory, 'second.asyncapi.json');

    const first = bundle(join(fixture, 'orders.asyncapi.yaml'), output);
    assert.equal(first.status, 0, first.stderr);
    const second = bundle(join(copied, 'orders.asyncapi.yaml'), secondOutput);
    assert.equal(second.status, 0, second.stderr);
    const bytes = await readFile(output);
    assert.deepEqual(bytes, await readFile(secondOutput));

    const rebundled = bundle(output, secondOutput);
    assert.equal(rebundled.status, 0, rebundled.stderr);
    assert.deepEqual(bytes, await readFile(secondOutput));

    const parser = new Parser();
    const single = await readFile(new URL('fixtures/bundling/single/orders.asyncapi.json', import.meta.url), 'utf8');
    for (const source of [bytes.toString('utf8'), single]) {
        const { document, diagnostics } = await parser.parse(source);
        assert.deepEqual(diagnostics.filter(diagnostic => diagnostic.severity === 0), []);
        assert.ok(document);
    }

    const contract = JSON.parse(bytes);
    const order = contract.components.schemas.OrderCreated;
    assert.deepEqual(order.required, ['order_id', 'detail']);
    assert.equal(order.properties.next.$ref, '#/components/schemas/OrderCreated');
    assert.equal(order.properties.detail.$ref, '#/x-staticnorth-bundled/0');
    assert.equal(order.properties.previousDetail.$ref, order.properties.detail.$ref);
    assert.equal(contract.operations.acceptOrder.messages[0].$ref, '#/channels/orders/messages/orderCreated');
});

test('Bundling preserves literal $ref data in examples, extensions, and schema property names', async t => {
    const directory = await mkdtemp(join(tmpdir(), 'asyncapi-literals-'));
    t.after(() => rm(directory, { recursive: true, force: true }));
    const input = join(directory, 'orders.yaml');
    const output = join(directory, 'orders.asyncapi.json');
    const literal = { $ref: 'https://example.invalid/example' };
    const contract = {
        asyncapi: '3.1.0',
        info: { title: 'Examples', version: '1' },
        'x-literal': literal,
        components: {
            messages: { Example: { examples: [{ payload: literal }] } },
            schemas: {
                Example: {
                    type: 'object',
                    default: literal,
                    properties: { $ref: { type: 'string' }, examples: { $ref: './text.yaml' } },
                },
                Native: {
                    schemaFormat: 'application/vnd.aai.asyncapi+yaml;version=3.1.0',
                    schema: { $ref: './text.yaml' },
                },
                Custom: {
                    schemaFormat: 'application/example',
                    schema: literal,
                },
            },
        },
    };
    await writeFile(input, JSON.stringify(contract));
    await writeFile(join(directory, 'text.yaml'), 'type: string\n');
    const result = bundle(input, output);
    assert.equal(result.status, 0, result.stderr);
    contract.components.schemas.Example.properties.examples.$ref = '#/x-staticnorth-bundled/0';
    contract.components.schemas.Native.schema.$ref = '#/x-staticnorth-bundled/0';
    contract['x-staticnorth-bundled'] = [{ type: 'string' }];
    assert.deepEqual(JSON.parse(await readFile(output, 'utf8')), contract);
});

test('Invalid or nonlocal references fail without replacing the previous bundle', async t => {
    const directory = await mkdtemp(join(tmpdir(), 'asyncapi-invalid-bundle-'));
    t.after(() => rm(directory, { recursive: true, force: true }));
    const inputs = join(directory, 'inputs');
    await cp(fixture, inputs, { recursive: true });
    const input = join(inputs, 'orders.asyncapi.yaml');
    const output = join(directory, 'orders.asyncapi.json');
    await writeFile(output, 'previous bundle');
    await writeFile(join(directory, 'outside.yaml'), 'type: string\n');
    await symlink(join(directory, 'outside.yaml'), join(inputs, 'outside.yaml'));
    await writeFile(join(inputs, 'alias.yaml'), "a: { $ref: '#/b' }\nb: { $ref: '#/a' }\n");

    for (const [reference, expected] of [
        ['./schemas/order.json#/Missing', /unresolved reference/i],
        ['./schemas/order.json#/Order~2', /invalid JSON Pointer/i],
        ['https://example.invalid/schema.yaml', /only local file references/i],
        ['../outside.yaml', /inside the contract directory/i],
        ['./outside.yaml', /leaves the contract directory/i],
        ['./alias.yaml#/a', /cyclic reference aliases/i],
    ]) {
        await writeFile(input, `asyncapi: 3.1.0\ncomponents:\n  schemas:\n    Order: { $ref: '${reference}' }\n`);
        const result = bundle(input, output);
        assert.equal(result.status, 1, result.stderr);
        assert.match(result.stderr, expected);
        assert.equal(await readFile(output, 'utf8'), 'previous bundle');
    }
});
