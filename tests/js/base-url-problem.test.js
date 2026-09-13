// Copyright (C) 2024  Roland Breitschaft
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// GitHub #100. Test Connection showed one fixed sentence for every bad Base URL and threw the
// exception away, so a field that looked correct on screen and failed anyway left you in devtools
// calling new URL() by hand.
//
// Repeating the exception, which is what the report asked for, would not have helped: its message
// is the string "Invalid URL" and nothing else, whatever the cause. Worse, the three causes the
// report guessed at do not throw at all. "provider.com:8000" parses as a scheme called
// "provider.com", an invisible space is stripped out of the host, and a Cyrillic letter is turned
// into punycode. Each of those produced the "must include protocol" message or no message at all.
//
// So what is pinned here is the value being echoed back, the protocol being checked after the
// parse rather than guessed at, and the host being compared before and after.

'use strict';

const test = require('node:test');
const assert = require('node:assert');
const { loadConfig } = require('./helpers/config-harness');

const config = loadConfig();

/** describeBaseUrlProblem needs no DOM. */
function describe(value) {
    return config.describeBaseUrlProblem(value);
}

test('a URL the browser refuses is reported with the value that failed', async (t) => {
    await t.test('the value is echoed back, since the exception message never varies', () => {
        const problem = describe('http://ho st:8000');
        assert.strictEqual(problem.fatal, true);
        assert.match(problem.message, /http:\/\/ho st:8000/);
    });

    await t.test('an empty value asks for one instead of blaming the parser', () => {
        assert.match(describe('').message, /Enter a Base URL/);
    });

    await t.test('smart quotes pasted around the URL are visible in the message', () => {
        const problem = describe('“http://host:8000”');
        assert.strictEqual(problem.fatal, true);
        assert.match(problem.message, /“http:\/\/host:8000”/);
    });
});

test('a missing protocol is caught, which it was not before', async (t) => {
    await t.test('a bare host:port parses as its own scheme and has to be rejected', () => {
        // new URL('provider.com:8000') succeeds with protocol "provider.com:", so the old check
        // let this through to the server and then reported a connection failure instead.
        const problem = describe('provider.com:8000');
        assert.strictEqual(problem.fatal, true);
        assert.match(problem.message, /has to start with http:\/\/ or https:\/\//);
        assert.match(problem.message, /provider\.com/);
    });

    await t.test('a non-web scheme is named rather than described as malformed', () => {
        assert.match(describe('ftp://host:8000').message, /"ftp"/);
    });
});

test('a host that is not what was typed is reported but does not block the test', async (t) => {
    await t.test('an invisible character silently disappears from the host', () => {
        const problem = describe('https://ho​st:8080');
        assert.strictEqual(problem.fatal, false);
        assert.match(problem.message, /reads as "host"/);
    });

    await t.test('a lookalike letter becomes punycode', () => {
        const problem = describe('https://hоst:8080');
        assert.strictEqual(problem.fatal, false);
        assert.match(problem.message, /xn--/);
    });

    await t.test('an ordinary URL has nothing to report', () => {
        assert.strictEqual(describe('http://provider.example:9191'), null);
        assert.strictEqual(describe('https://provider.example.com:8080'), null);
        assert.strictEqual(describe('http://[::1]:8096'), null);
    });

    await t.test('an uppercase protocol and host are not a mismatch', () => {
        assert.strictEqual(describe('HTTP://Provider.Example.COM:8080'), null);
    });

    await t.test('a user in front of the host is not mistaken for the host', () => {
        assert.strictEqual(describe('http://someone@intranet:8080'), null);
    });
});

test('the message is escaped before it reaches the status line', () => {
    // The value is echoed back into innerHTML, so a Base URL carrying markup would otherwise run
    // it on the config page.
    const restore = (() => {
        const previous = global.document;
        global.document = {
            createElement: () => ({
                set textContent(text) {
                    this.innerHTML = String(text)
                        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
                },
                innerHTML: '',
            }),
        };
        return () => {
            if (previous === undefined) {
                delete global.document;
            } else {
                global.document = previous;
            }
        };
    })();

    try {
        const problem = describe('http://a<img src=x onerror=alert(1)>');
        assert.strictEqual(problem.fatal, true);
        const rendered = config.escapeHtml(problem.message);
        assert.ok(!rendered.includes('<img'), 'markup survived escaping: ' + rendered);
        assert.match(rendered, /&lt;img/);
    } finally {
        restore();
    }
});
