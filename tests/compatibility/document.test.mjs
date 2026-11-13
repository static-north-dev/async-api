import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { Parser } from '@asyncapi/parser';

const root = fileURLToPath(new URL('../../', import.meta.url));
const parser = new Parser();

function readAndWrite(fixture) {
    return execFileSync('dotnet', [
        'run', '--no-build', '--project', 'samples/Producer', '--', fixture,
    ], { cwd: root, encoding: 'utf8' });
}

async function validationErrors(json) {
    const diagnostics = await parser.validate(json);
    return diagnostics.filter(diagnostic => diagnostic.severity === 0);
}

test('Core emits a valid AsyncAPI 3.1.0 document', async () => {
    const json = readAndWrite('samples/Contracts/orders.asyncapi.json');
    const errors = await validationErrors(json);

    assert.deepEqual(errors, []);
});

test('The official parser rejects an invalid operation action in Core output', async () => {
    const json = readAndWrite('tests/compatibility/invalid-action.asyncapi.json');
    const errors = await validationErrors(json);

    assert.ok(errors.some(error => error.path.join('.') === 'operations.invalid.action'), JSON.stringify(errors));
});
