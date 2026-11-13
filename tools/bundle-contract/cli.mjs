import { randomUUID } from 'node:crypto';
import { realpath, rename, rm, writeFile } from 'node:fs/promises';
import { basename, dirname, join, resolve } from 'node:path';
import { bundleContract } from './bundle.mjs';

try {
    const [input, output, ...extra] = process.argv.slice(2);
    if (!input || !output?.endsWith('.asyncapi.json') || extra.length > 0) {
        throw new Error('Usage: node tools/bundle-contract/cli.mjs <input.yaml|input.json> <output.asyncapi.json>');
    }

    const target = resolve(output);
    const { json, inputs } = await bundleContract(input);
    const directory = await realpath(dirname(target));
    if (inputs.includes(join(directory, basename(target)))) {
        throw new Error('The output file must be separate from the contract inputs');
    }

    const temporary = join(directory, `.asyncapi-${randomUUID()}.tmp`);
    try {
        await writeFile(temporary, json, { flag: 'wx' });
        await rename(temporary, target);
    } finally {
        await rm(temporary, { force: true });
    }
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}
