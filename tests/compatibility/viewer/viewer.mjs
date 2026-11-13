import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const configuration = process.env.ASYNCAPI_TEST_CONFIGURATION ?? 'Debug';
const producer = process.env.ASYNCAPI_WEB_PRODUCER_DLL ?? `samples/WebProducer/bin/${configuration}/net11.0/WebProducer.dll`;

test('The viewer renders the protected AsyncAPI 3.1 document from the configured route', { timeout: 30_000 }, async () => {
    const token = 'viewer-test-token';
    const path = '/internal/orders.json';
    const child = spawn('dotnet', [producer, '--urls', 'http://127.0.0.1:0'], {
        cwd: root,
        env: { ...process.env, ASYNCAPI_SPEC_TOKEN: token, AsyncApi__Path: path },
    });
    const closed = once(child, 'close');
    let browser;
    let page;
    const pageErrors = [];
    try {
        let output = '';
        const origin = await Promise.race([
            new Promise(resolve => {
                child.stdout.on('data', chunk => {
                    output += chunk;
                    const match = output.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);
                    if (match) {
                        resolve(match[1]);
                    }
                });
            }),
            closed.then(([code]) => { throw new Error(`Web producer exited with ${code}`); }),
            new Promise((_, reject) => {
                const timeout = setTimeout(() => reject(new Error('Web producer did not start.')), 5000);
                timeout.unref();
            }),
        ]);
        browser = await chromium.launch();
        page = await browser.newPage();
        page.setDefaultTimeout(5000);
        const requests = [];
        page.on('pageerror', error => pageErrors.push(error.message));
        page.on('console', message => {
            if (message.type() === 'error' && !message.text().includes('Failed to load resource: the server responded with a status of 401')) {
                pageErrors.push(message.text());
            }
        });
        await page.route('**/*', async route => {
            const request = route.request();
            const url = new URL(request.url());
            const headers = await request.allHeaders();
            requests.push({ origin: url.origin, path: url.pathname, authorization: headers.authorization });
            if (url.origin === origin) {
                await route.continue();
            } else {
                await route.abort();
            }
        });

        const viewer = await page.goto(`${origin}/asyncapi-ui`);
        assert.equal(viewer.status(), 200);
        assert.ok(viewer.headers()['content-security-policy'].includes("connect-src 'self'"));
        for (const name of ['LICENSE.txt', 'index.js.LICENSE.txt']) {
            const notice = await page.request.get(`${origin}/asyncapi-ui/assets/${name}`);
            assert.equal(notice.status(), 200);
            assert.ok((await notice.text()).length > 0);
        }
        const unknown = await page.request.get(`${origin}/asyncapi-ui/assets/package.json`);
        assert.equal(unknown.status(), 404);
        const input = page.getByLabel('Bearer token');
        const submit = page.getByRole('button', { name: 'View documentation' });
        await page.getByText('This document requires a bearer token.').waitFor();
        assert.equal(await page.getByText('Web Orders').count(), 0);

        await input.fill('incorrect-token');
        await submit.click();
        await page.getByText('The bearer token was rejected.').waitFor();
        assert.equal(await input.inputValue(), '');
        assert.equal(await page.getByText('Web Orders').count(), 0);

        await input.fill(token);
        const loaded = page.waitForResponse(response => response.url() === origin + path && response.ok());
        await submit.click();
        const specification = await loaded;
        assert.equal((await specification.json()).asyncapi, '3.1.0');
        assert.equal(specification.headers()['cache-control'], 'no-store');

        const documentation = page.locator('#asyncapi-document');
        await documentation.getByText('Web Orders', { exact: false }).first().waitFor();
        await documentation.getByText('Publishes the new order identifier.').first().waitFor();
        await documentation.getByText('order.created', { exact: true }).first().waitFor();
        assert.ok((await documentation.textContent()).includes('order-123'));
        assert.deepEqual(pageErrors, []);
        assert.deepEqual(requests.filter(request => request.path === path).map(request => request.authorization), [
            undefined,
            'Bearer incorrect-token',
            `Bearer ${token}`,
        ]);
        assert.ok(requests.every(request => request.origin === origin && (!request.authorization || request.path === path)));
        assert.deepEqual(await page.evaluate(() => [localStorage.length, sessionStorage.length]), [0, 0]);
    } catch (error) {
        const content = await page?.locator('body').innerText();
        throw new Error(`Viewer failed. Browser errors: ${pageErrors.join('\n')}\nPage: ${content}`, { cause: error });
    } finally {
        await browser?.close();
        child.kill();
        await closed;
    }
});
