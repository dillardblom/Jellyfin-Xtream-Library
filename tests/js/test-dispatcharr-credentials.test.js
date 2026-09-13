// Copyright (C) 2024  Roland Breitschaft
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// GitHub #114. Test Dispatcharr sent only ?providerIndex= and no body, so the server tested the
// saved configuration. Typing a password and pressing Test before saving therefore tested the
// previous one, and the failure was the same red "authentication failed" a genuinely wrong
// password gets, with nothing on screen to say that saving would change the answer.
//
// Its neighbour testConnection had always sent the form values. What is pinned here is that this
// one now does too, and specifically that an emptied Dispatcharr URL travels as "" rather than
// being left out: the server reads a missing field as "use the saved URL" and an empty one as
// "use the Xtream host", which are different addresses.
//
// This is the request being built, not the button being clicked. There is no DOM here, so a green
// run says nothing about what the page renders.

'use strict';

const test = require('node:test');
const assert = require('node:assert');
const { loadConfig, element, withDocument } = require('./helpers/config-harness');

/**
 * Backs getElementById with a proxy that invents an empty stub for any id not named, so unrelated
 * fields appearing on the form cannot break these tests. Mirrors provider-credentials-trim.
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

/**
 * The browser globals config.js reaches for, plus a fetch that records what it was handed.
 *
 * Async because the stubs have to outlive the fetch promise chain: testDispatcharr writes its
 * result from a .then, which runs after the synchronous call returns and still reads `document`.
 */
async function runTestDispatcharr(overrides, response) {
    const config = loadConfig();
    config.activeProviderIndex = 0;

    const calls = [];
    const previousFetch = global.fetch;
    const previousApiClient = global.ApiClient;

    global.fetch = (url, init) => {
        calls.push({ url, init });
        return Promise.resolve({
            json: () => Promise.resolve(response || { Success: true, Message: 'ok' }),
        });
    };
    global.ApiClient = {
        getUrl: (path) => '/' + path,
        accessToken: () => 'token',
    };

    const elements = fields(overrides);
    const restore = withDocument(elements);

    // escapeHtml builds a detached div and reads back innerHTML. The harness has no DOM, so this
    // stands in for one. It proves the message is routed through escapeHtml, which is what the old
    // code skipped; the browser's own escaping is not what is under test.
    global.document.createElement = () => ({
        textContent: '',
        get innerHTML() {
            return String(this.textContent)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;');
        },
        set innerHTML(_value) { /* written only by the code under test, never read back */ },
    });

    try {
        config.testDispatcharr();

        // Twice: one turn for response.json(), one for the handler that writes the status span.
        await flush();
        await flush();
    } finally {
        restore();
        global.fetch = previousFetch;
        global.ApiClient = previousApiClient;
    }

    return { calls, statusSpan: elements.dispatcharrStatus };
}

/** Lets the fetch promise chain run to completion. */
function flush() {
    return new Promise((resolve) => setImmediate(resolve));
}

const TYPED = {
    txtBaseUrl: { value: 'http://xtream.example.com:8080' },
    txtDispatcharrBaseUrl: { value: 'http://dispatcharr.example.com:9191' },
    txtDispatcharrApiUser: { value: 'typed-admin' },
    txtDispatcharrApiPass: { value: 'typed-pass' },
};

test('the test sends what is on the form, not what was saved', async (t) => {
    await t.test('a body is sent at all, which it was not before', async () => {
        const { calls } = await runTestDispatcharr(TYPED);

        assert.strictEqual(calls.length, 1);
        assert.ok(calls[0].init.body, 'the request must carry a body');
    });

    await t.test('the typed credentials are the ones sent', async () => {
        const body = JSON.parse((await runTestDispatcharr(TYPED)).calls[0].init.body);

        assert.strictEqual(body.ApiUser, 'typed-admin');
        assert.strictEqual(body.ApiPass, 'typed-pass');
        assert.strictEqual(body.DispatcharrBaseUrl, 'http://dispatcharr.example.com:9191');
        assert.strictEqual(body.BaseUrl, 'http://xtream.example.com:8080');
    });

    await t.test('the provider index still travels in the query string', async () => {
        const { calls } = await runTestDispatcharr(TYPED);

        assert.match(calls[0].url, /providerIndex=0/);
    });
});

test('an emptied Dispatcharr URL is sent, not omitted', async (t) => {
    // The server reads a missing field as "use the saved URL" and an empty one as "use the Xtream
    // host". Dropping the key would restore the URL the user just deleted.
    await t.test('the key is present with an empty value', async () => {
        const body = JSON.parse((await runTestDispatcharr(
            Object.assign({}, TYPED, { txtDispatcharrBaseUrl: { value: '' } }))).calls[0].init.body);

        assert.ok('DispatcharrBaseUrl' in body, 'the key must be present');
        assert.strictEqual(body.DispatcharrBaseUrl, '');
    });

    await t.test('an emptied password is sent as empty too', async () => {
        const body = JSON.parse((await runTestDispatcharr(
            Object.assign({}, TYPED, { txtDispatcharrApiPass: { value: '' } }))).calls[0].init.body);

        assert.ok('ApiPass' in body);
        assert.strictEqual(body.ApiPass, '');
    });
});

test('the values are trimmed the way testConnection trims them', async (t) => {
    await t.test('whitespace around a pasted credential is stripped', async () => {
        const body = JSON.parse((await runTestDispatcharr(Object.assign({}, TYPED, {
            txtDispatcharrApiUser: { value: '  typed-admin ' },
            txtDispatcharrApiPass: { value: 'typed-pass\t' },
        }))).calls[0].init.body);

        assert.strictEqual(body.ApiUser, 'typed-admin');
        assert.strictEqual(body.ApiPass, 'typed-pass');
    });

    await t.test('a trailing slash on either URL is dropped', async () => {
        const body = JSON.parse((await runTestDispatcharr(Object.assign({}, TYPED, {
            txtBaseUrl: { value: 'http://xtream.example.com:8080/' },
            txtDispatcharrBaseUrl: { value: 'http://dispatcharr.example.com:9191/' },
        }))).calls[0].init.body);

        assert.strictEqual(body.BaseUrl, 'http://xtream.example.com:8080');
        assert.strictEqual(body.DispatcharrBaseUrl, 'http://dispatcharr.example.com:9191');
    });
});

test('the reply is escaped before it reaches innerHTML', async (t) => {
    // Its neighbour escaped and this one did not, and the message repeats server exception text
    // which repeats the URL the user typed.
    await t.test('a failure message carrying markup comes out escaped', async () => {
        const { statusSpan } = await runTestDispatcharr(TYPED, {
            Success: false,
            Message: 'Connection failed: <img src=x onerror=alert(1)>',
        });

        assert.ok(!statusSpan.innerHTML.includes('<img'), 'markup must not survive into innerHTML');
        assert.match(statusSpan.innerHTML, /&lt;img/);
    });

    await t.test('a success message is escaped as well', async () => {
        const { statusSpan } = await runTestDispatcharr(TYPED, {
            Success: true,
            Message: 'Connected <b>ok</b>',
        });

        assert.ok(!statusSpan.innerHTML.includes('<b>'));
        assert.match(statusSpan.innerHTML, /&lt;b&gt;/);
    });
});
