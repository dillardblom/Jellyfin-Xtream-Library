// Copyright (C) 2024  Roland Breitschaft
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// GitHub #99. ProviderConfig.Name and the dropdown that renders it both existed already; what was
// missing was a form field, so the name could never be anything but the default the UI invented
// when the provider was created. With several accounts configured the dropdown read
// "Provider 1 / Provider 2 / Provider 3" and told you nothing about which was which.
//
// Two halves are worth pinning: what the form writes back, and what the dropdown draws when the
// field is left empty.

'use strict';

const test = require('node:test');
const assert = require('node:assert');
const { loadConfig, element, withDocument } = require('./helpers/config-harness');

/**
 * updateActiveProviderFromUI reads 30-odd element ids and would throw on any it cannot find.
 * Only the name matters here, so back the lookup with a proxy that invents an empty stub for
 * every other id. New fields on the form therefore cannot break this test.
 */
function fields(overrides) {
    const cache = {};
    return new Proxy({}, {
        get(_target, id) {
            if (typeof id !== 'string') {
                return undefined;
            }

            if (!(id in cache)) {
                cache[id] = element(Object.prototype.hasOwnProperty.call(overrides, id) ? overrides[id] : {});
            }

            return cache[id];
        },
    });
}

/** Runs updateActiveProviderFromUI against the given field values and returns the saved provider. */
function readBack(overrides) {
    const config = loadConfig();
    config.providers = [{}];
    config.activeProviderIndex = 0;

    const restore = withDocument(fields(overrides));
    try {
        config.updateActiveProviderFromUI();
    } finally {
        restore();
    }

    return config.providers[0];
}

/** A <select> stub that records the options renderProviderSelector appends to it. */
function selectStub() {
    const appended = [];
    return {
        element: element({ appendChild: (opt) => appended.push(opt) }),
        labels: () => appended.map((opt) => opt.textContent),
    };
}

/** Returns the option labels renderProviderSelector draws for the given providers. */
function dropdownLabels(providers) {
    const config = loadConfig();
    config.providers = providers;
    config.activeProviderIndex = 0;

    const sel = selectStub();
    const restore = withDocument({ selActiveProvider: sel.element });
    global.document.createElement = () => ({ value: '', textContent: '' });
    try {
        config.renderProviderSelector();
    } finally {
        restore();
    }

    return sel.labels();
}

test('the name typed into the form is what gets saved', async (t) => {
    await t.test('a plain name round-trips', () => {
        assert.strictEqual(readBack({ txtName: { value: 'Backup - ISP2' } }).Name, 'Backup - ISP2');
    });

    await t.test('surrounding whitespace is stripped, like every other field', () => {
        assert.strictEqual(readBack({ txtName: { value: '  Primary\t' } }).Name, 'Primary');
    });

    await t.test('a whitespace-only name is stored as empty, not as spaces', () => {
        // Otherwise the dropdown would draw a blank entry instead of falling back to the position.
        assert.strictEqual(readBack({ txtName: { value: '   ' } }).Name, '');
    });
});

test('the dropdown shows the name, and the position when there is none', async (t) => {
    await t.test('a named provider is drawn by its name', () => {
        assert.deepStrictEqual(
            dropdownLabels([{ Name: 'Primary', IsEnabled: true }, { Name: 'Backup', IsEnabled: true }]),
            ['Primary', 'Backup']);
    });

    await t.test('an empty name falls back to the position in the list', () => {
        assert.deepStrictEqual(
            dropdownLabels([{ Name: '', IsEnabled: true }, { Name: '', IsEnabled: true }]),
            ['Provider 1', 'Provider 2']);
    });

    await t.test('a provider written by an older UI carries no Name at all', () => {
        assert.deepStrictEqual(
            dropdownLabels([{ IsEnabled: true }, { Name: 'Backup', IsEnabled: true }]),
            ['Provider 1', 'Backup']);
    });

    await t.test('the disabled marker survives a custom name', () => {
        assert.deepStrictEqual(
            dropdownLabels([{ Name: 'Primary', IsEnabled: false }]),
            ['Primary (disabled)']);
    });
});
