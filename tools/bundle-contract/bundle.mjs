import { readFile, realpath, stat } from 'node:fs/promises';
import { dirname, extname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { JSON_SCHEMA, load } from 'js-yaml';

const extension = 'x-staticnorth-bundled';
const nativeSchemaFormats = new Set([
    'application/vnd.aai.asyncapi;version=3.1.0',
    'application/vnd.aai.asyncapi+json;version=3.1.0',
    'application/vnd.aai.asyncapi+yaml;version=3.1.0',
]);
const rootSections = { channels: 'channel', operations: 'operation', servers: 'server' };
const componentSections = {
    ...rootSections, schemas: 'schema', messages: 'message', securitySchemes: 'security',
    serverVariables: 'reference', parameters: 'parameter', correlationIds: 'reference', replies: 'reply',
    replyAddresses: 'reference', externalDocs: 'reference', tags: 'tag', operationTraits: 'operationTrait',
    messageTraits: 'messageTrait', serverBindings: 'reference', channelBindings: 'reference',
    operationBindings: 'reference', messageBindings: 'reference',
};
const maps = sections => Object.fromEntries(Object.entries(sections).map(([key, kind]) => [key, `map:${kind}`]));
const metadata = { tags: 'list:tag', externalDocs: 'reference' };
const operationTrait = { ...metadata, security: 'list:security', bindings: 'reference' };
const messageTrait = { ...metadata, bindings: 'reference', headers: 'schema', correlationId: 'reference' };
const fields = {
    document: { ...maps(rootSections), info: 'info', components: 'components' },
    info: metadata,
    components: maps(componentSections),
    server: { ...metadata, bindings: 'reference', security: 'list:security', variables: 'map:reference' },
    security: metadata,
    tag: { externalDocs: 'reference' },
    channel: { ...metadata, bindings: 'reference', messages: 'map:message', parameters: 'map:parameter', servers: 'list:reference' },
    operation: { ...operationTrait, channel: 'link', messages: 'list:link', reply: 'reply', traits: 'list:operationTrait' },
    operationTrait,
    reply: { address: 'reference', channel: 'link', messages: 'list:link' },
    message: { ...messageTrait, payload: 'schema', traits: 'list:messageTrait' },
    messageTrait,
    schema: {
        properties: 'map:schema', patternProperties: 'map:schema', dependencies: 'map:schema',
        definitions: 'map:schema', $defs: 'map:schema', items: 'schema', additionalItems: 'schema',
        additionalProperties: 'schema', propertyNames: 'schema', contains: 'schema', not: 'schema',
        if: 'schema', then: 'schema', else: 'schema', allOf: 'list:schema', anyOf: 'list:schema', oneOf: 'list:schema',
        externalDocs: 'reference',
    },
};

function pointer(segments) {
    return '#' + segments.map(segment => '/' + encodeURIComponent(segment.replaceAll('~', '~0').replaceAll('/', '~1'))).join('');
}

function location(file, segments) {
    return pathToFileURL(file).href + pointer(segments);
}

function referenceTo(value, file, path) {
    if (typeof value !== 'string' || value.length === 0) {
        throw new Error(`${location(file, path)}: expected a nonempty $ref string`);
    }

    const url = new URL(value, pathToFileURL(file));
    if (url.protocol !== 'file:' || url.hostname || url.search) {
        throw new Error(`${location(file, path)}: only local file references are supported: ${value}`);
    }

    const fragment = decodeURIComponent(url.hash.slice(1));
    if ((fragment && !fragment.startsWith('/')) || /~(?![01])/u.test(fragment)) {
        throw new Error(`${location(file, path)}: invalid JSON Pointer in '${value}'`);
    }

    const segments = fragment ? fragment.slice(1).split('/').map(segment => segment.replaceAll('~1', '/').replaceAll('~0', '~')) : [];
    return { file: fileURLToPath(url), segments };
}

function within(directory, file) {
    const path = relative(directory, file);
    return path !== '..' && !path.startsWith('..' + sep) && !isAbsolute(path);
}

function hasReference(value) {
    return value !== null && typeof value === 'object' && !Array.isArray(value) && Object.hasOwn(value, '$ref');
}

function childKind(kind, key) {
    if (kind?.startsWith('map:')) {
        return kind.slice(4);
    }

    const children = fields[kind];
    return children && Object.hasOwn(children, key) ? children[key] : undefined;
}

export async function bundleContract(input) {
    const rootFile = resolve(input);
    const directory = dirname(rootFile);
    const realDirectory = await realpath(directory);
    const files = new Map();
    const imports = new Map();
    const placements = new Map();
    const dependencies = [];
    const active = new Set();
    let remainingNodes = 1_000_000;

    async function loadFile(file) {
        if (!files.has(file)) {
            if (!within(directory, file) || !['.json', '.yaml', '.yml'].includes(extname(file).toLowerCase())) {
                throw new Error(`Expected a JSON or YAML file inside the contract directory: ${file}`);
            }

            const actual = await realpath(file);
            if (!within(realDirectory, actual)) {
                throw new Error(`Reference leaves the contract directory: ${file}`);
            }

            const info = await stat(actual);
            if (!info.isFile() || info.size > 4 * 1024 * 1024) {
                throw new Error(`Expected a regular contract file no larger than 4 MiB: ${file}`);
            }

            const source = await readFile(actual, 'utf8');
            if (extname(file).toLowerCase() === '.json') {
                JSON.parse(source);
            }

            const value = load(source, { schema: JSON_SCHEMA, filename: file });
            files.set(file, { actual, value });
        }

        return files.get(file).value;
    }

    async function targetOf(reference, origin, path, preservePlacement = false) {
        const seen = new Set();
        let target = referenceTo(reference, origin, path);

        while (target.file !== rootFile) {
            const id = location(target.file, target.segments);
            if (seen.has(id)) {
                throw new Error(`Cyclic reference aliases: ${id}`);
            }

            seen.add(id);
            let value = await loadFile(target.file);
            for (const segment of target.segments) {
                if (value === null || typeof value !== 'object' || !Object.hasOwn(value, segment) ||
                    (Array.isArray(value) && !/^(0|[1-9]\d*)$/u.test(segment))) {
                    throw new Error(`Unresolved reference '${reference}' from ${location(origin, path)}: ${id}`);
                }

                value = value[segment];
            }

            if (!hasReference(value) || (preservePlacement && placement(target))) {
                return { ...target, id, value };
            }

            target = referenceTo(value.$ref, target.file, target.segments);
        }

        return target;
    }

    async function reserve(map, prefix) {
        for (const key of Object.keys(map ?? {}).sort()) {
            const slot = [...prefix, key];
            if (hasReference(map[key]) && !String(map[key].$ref).startsWith('#')) {
                const target = await targetOf(map[key].$ref, rootFile, slot);
                if (target.file !== rootFile && !placements.has(target.id)) {
                    placements.set(target.id, slot);
                    imports.set(pointer(slot), target);
                }
            }
        }
    }

    function placement(target) {
        for (let depth = target.segments.length; depth >= 0; depth--) {
            const prefix = location(target.file, target.segments.slice(0, depth));
            const path = placements.get(prefix);
            if (path) {
                return [...path, ...target.segments.slice(depth)];
            }
        }

        return undefined;
    }

    async function visit(value, file, sourcePath, outputPath, kind) {
        if (--remainingNodes < 0) {
            throw new Error('Contract exceeds the bundling limit of one million JSON values');
        }

        if (typeof value === 'number' && (!Number.isFinite(value) || (Number.isInteger(value) && !Number.isSafeInteger(value)))) {
            throw new Error(`${location(file, sourcePath)}: number is outside the supported JSON precision`);
        }

        if (value === null || typeof value !== 'object') {
            return value;
        }

        const multiFormat = kind === 'schema' && Object.hasOwn(value, 'schemaFormat');
        let bundledReference;
        if (kind && kind !== 'document' && kind !== 'info' && kind !== 'components' &&
            !kind.includes(':') && !multiFormat && hasReference(value)) {
            const imported = file === rootFile ? imports.get(pointer(outputPath)) : undefined;
            if (imported) {
                return visit(imported.value, imported.file, imported.segments, outputPath, kind);
            }

            const target = await targetOf(value.$ref, file, sourcePath, kind === 'link');
            let destination = target.file === rootFile ? target.segments : placement(target);
            if (!destination) {
                if (kind === 'link' && outputPath[0] === 'operations') {
                    throw new Error(`${location(file, sourcePath)}: operation reference has no named destination in the root document`);
                }

                destination = [extension, String(dependencies.length)];
                placements.set(target.id, destination);
                dependencies.push({ target, destination, kind });
            }

            bundledReference = pointer(destination);
            kind = undefined;
        }

        if (active.has(value)) {
            throw new Error(`${location(file, sourcePath)}: cyclic YAML alias; use $ref for recursion`);
        }

        active.add(value);
        try {
            if (Array.isArray(value)) {
                const result = [];
                const itemKind = kind?.startsWith('list:') ? kind.slice(5) : kind === 'schema' ? 'schema' : undefined;
                for (const [index, item] of value.entries()) {
                    const key = String(index);
                    result.push(await visit(item, file, [...sourcePath, key], [...outputPath, key], itemKind));
                }

                return result;
            }

            const result = Object.create(null);
            for (const key of Object.keys(value).sort()) {
                if (key === '$ref' && bundledReference !== undefined) {
                    result[key] = bundledReference;
                } else {
                    const next = multiFormat && key === 'schema' && nativeSchemaFormats.has(value.schemaFormat)
                        ? 'schema'
                        : childKind(kind, key);
                    result[key] = await visit(value[key], file, [...sourcePath, key], [...outputPath, key], next);
                }
            }

            return result;
        } finally {
            active.delete(value);
        }
    }

    const root = await loadFile(rootFile);
    if (root?.asyncapi !== '3.1.0') {
        throw new Error('Expected an AsyncAPI 3.1.0 document');
    }

    // Reserve named locations before following users of those definitions.
    for (const key of Object.keys(rootSections).sort()) {
        await reserve(root[key], [key]);
    }

    for (const key of Object.keys(componentSections).sort()) {
        await reserve(root.components?.[key], ['components', key]);
    }

    const document = await visit(root, rootFile, [], [], 'document');
    if (dependencies.length > 0) {
        if (Object.hasOwn(document, extension)) {
            throw new Error(`Cannot store bundled references: '${extension}' is already present`);
        }

        const bundled = [];
        for (const { target, destination, kind } of dependencies) {
            bundled.push(await visit(target.value, target.file, target.segments, destination, kind));
        }

        document[extension] = bundled;
    }

    const ordered = Object.fromEntries(Object.keys(document).sort().map(key => [key, document[key]]));
    return { json: JSON.stringify(ordered, null, 2) + '\n', inputs: [...files.values()].map(file => file.actual) };
}
