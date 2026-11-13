import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { once } from 'node:events';
import { createServer } from 'node:http';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const configuration = process.env.ASYNCAPI_TEST_CONFIGURATION ?? 'Debug';

test('The web producer uses the host policy on a custom route', { timeout: 30_000 }, async () => {
    const token = 'local-test-token';
    const child = spawn('dotnet', [`samples/WebProducer/bin/${configuration}/net11.0/WebProducer.dll`, '--urls', 'http://127.0.0.1:0'], {
        cwd: root,
        env: { ...process.env, ASYNCAPI_SPEC_TOKEN: token, AsyncApi__Path: '/internal/contract.json' },
    });
    const closed = once(child, 'close');
    let output = '';
    try {
        const address = await Promise.race([
            new Promise(resolve => {
                child.stdout.on('data', chunk => {
                    output += chunk;
                    const match = output.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);
                    if (match) resolve(match[1]);
                });
            }),
            closed.then(([code]) => { throw new Error(`Web producer exited with ${code}`); }),
        ]);
        const url = new URL('/internal/contract.json', address);
        const anonymous = await fetch(url, { signal: AbortSignal.timeout(5000) });
        assert.equal(anonymous.status, 401);
        const authenticated = await fetch(url, {
            headers: { Authorization: `Bearer ${token}` },
            signal: AbortSignal.timeout(5000),
        });
        assert.equal(authenticated.status, 200);
        assert.equal(authenticated.headers.get('content-type'), 'application/json');
        assert.equal(authenticated.headers.get('cache-control'), 'no-store');
        assert.equal((await authenticated.json()).components.messages['order.created'].name, 'order.created');
        assert.equal((await fetch(new URL('/asyncapi.json', address), { signal: AbortSignal.timeout(5000) })).status, 404);
        assert.equal((await fetch(new URL('/asyncapi-ui', address), { signal: AbortSignal.timeout(5000) })).status, 404);
    } finally {
        child.kill();
        await closed;
    }
});

async function fetchContract(...args) {
    const child = spawn('dotnet', [`samples/FetchContract/bin/${configuration}/net11.0/FetchContract.dll`, ...args], {
        cwd: root,
        env: { ...process.env, ASYNCAPI_SPEC_TOKEN: '' },
        stdio: 'ignore',
    });
    const [code] = await once(child, 'close');
    return code;
}

test('Fetch CLI pins bytes offline and preserves its file on redirects and oversized responses', { timeout: 30_000 }, async () => {
    const body = '{"asyncapi":"3.1.0","info":{"title":"Remote","version":"1"}}';
    const checksum = createHash('sha256').update(body).digest('hex');
    let redirected = false;
    const server = createServer((request, response) => {
        if (request.url === '/redirect') {
            response.writeHead(302, { Location: '/target' });
        } else if (request.url === '/target') {
            redirected = true;
        } else if (request.url === '/oversized') {
            response.write(Buffer.alloc(4 * 1024 * 1024));
            response.end('x');
            return;
        }
        response.end(body);
    });
    const directory = await mkdtemp(join(tmpdir(), 'asyncapi-fetch-'));
    try {
        server.listen(0, '127.0.0.1');
        await once(server, 'listening');
        const base = `http://127.0.0.1:${server.address().port}`;
        const path = join(directory, 'orders.asyncapi.json');
        assert.equal(await fetchContract('refresh', `${base}/contract`, path, checksum), 0);
        assert.equal(await fetchContract('refresh', `${base}/contract`, path, '0'.repeat(64)), 1);
        assert.equal(await fetchContract('refresh', `${base}/redirect`, path), 1);
        assert.equal(await fetchContract('refresh', `${base}/oversized`, path), 1);
        assert.equal(redirected, false);
        assert.equal(await readFile(path, 'utf8'), body);
        await new Promise(resolve => server.close(resolve));
        assert.equal(await fetchContract('offline', path, checksum), 0);
        assert.equal(await fetchContract('offline', path, '0'.repeat(64)), 1);
    } finally {
        server.close();
        await rm(directory, { recursive: true, force: true });
    }
});
