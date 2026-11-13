import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { Parser } from '@asyncapi/parser';

const root = fileURLToPath(new URL('../../', import.meta.url));
const parser = new Parser();

function exportDocument() {
    return execFileSync('dotnet', [
        'run', '--no-build', '--project', 'samples/Producer', '--', 'export',
    ], { cwd: root, encoding: 'utf8' });
}

async function validationErrors(json) {
    const diagnostics = await parser.validate(json);
    return diagnostics.filter(diagnostic => diagnostic.severity === 0);
}

test('The producer exports a valid, deterministic AsyncAPI 3.1.0 document', async () => {
    const json = exportDocument();
    const errors = await validationErrors(json);

    assert.deepEqual(errors, []);
    assert.equal(exportDocument(), json);

    const document = JSON.parse(json);
    const message = document.components.messages.OrderCreated;
    const schema = document.components.schemas.OrderCreated;
    assert.equal(document.operations.publishOrderCreated.action, 'send');
    assert.equal(message.payload.$ref, '#/components/schemas/OrderCreated');
    assert.deepEqual(message.headers.required, ['X-Correlation-Id']);
    assert.equal(message.examples[0].payload.order_id, '00000000-0000-0000-0000-000000000000');
    assert.equal(schema.properties.order_id.format, 'uuid');
    assert.equal(schema.properties.createdAt.format, 'date-time');
    assert.deepEqual(schema.properties.status.enum, ['Created', 'Confirmed']);
    assert.ok(schema.properties.note.type.includes('null'));
    assert.ok(schema.required.includes('source'));
    assert.ok(!schema.required.includes('note'));
    assert.ok(!('internalNote' in schema.properties));
    const replacement = schema.properties.items.items.properties.replacement;
    assert.equal(replacement.properties.replacement.$ref,
        '#/components/schemas/OrderCreated/properties/items/items/properties/replacement');
});

test('The official parser rejects an invalid operation action', async () => {
    const json = await readFile(new URL('invalid-action.asyncapi.json', import.meta.url), 'utf8');
    const errors = await validationErrors(json);

    assert.ok(errors.some(error => error.path.join('.') === 'operations.invalid.action'), JSON.stringify(errors));
});
