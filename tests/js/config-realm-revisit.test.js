// Copyright (C) 2024  Roland Breitschaft
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// GitHub #101. The Jellyfin dashboard AJAX-replaces the plugin config page on navigation, which
// re-evaluates Configuration/Web/config.js in the same JS realm that already has a live
// XtreamLibraryConfig binding. The previous top-level `const XtreamLibraryConfig = { ... }` shape
// threw SyntaxError on the second eval, leaving the freshly-injected btnAddProvider / btnRemoveProvider
// buttons unbound. The fix wraps the object literal in an IIFE that returns a cached
// globalThis.XtreamLibraryConfig on re-entry, and uses `var` for the outer binding so the
// declaration is idempotent in classic script scope.
//
// This test parses the script twice in the same vm context (one shared realm) and asserts that
// neither eval throws and that the resulting XtreamLibraryConfig is the same object reference on
// both sides - proof that the second eval took the IIFE cache branch instead of rebuilding the
// literal from scratch.

'use strict';

const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

const CONFIG_PATH = path.join(
    __dirname, '..', '..',
    'Jellyfin.Xtream.Library', 'Configuration', 'Web', 'config.js');

/**
 * Reads the script source, strips the final DOMContentLoaded bootstrap that would otherwise run
 * immediately under vm.runInContext (we want to observe parse + top-level execution only, not
 * touch a fake document). The bootstrap is `document.addEventListener('DOMContentLoaded', ...)`
 * or `if (document.readyState !== 'loading') initXtreamLibraryConfig();`. Either way it lives in
 * the bottom few lines of the file. Removing it does not change the parse behaviour we are
 * testing: the SyntaxError from a top-level const redeclaration fires before any of this code
 * runs.
 */
function scriptSource() {
    const raw = fs.readFileSync(CONFIG_PATH, 'utf8');
    const cut = raw.search(/\n\/\/ Initialize when DOM is ready/);
    return cut === -1 ? raw : raw.slice(0, cut);
}

test('config.js survives a second eval in the same realm', async (t) => {
    await t.test('a fresh parse does not throw', () => {
        // Sanity check: the script we are testing parses cleanly on its own.
        new vm.Script(scriptSource());
    });

    await t.test('two parses in the same context both succeed and yield the same object', () => {
        const sandbox = { globalThis: undefined };
        sandbox.globalThis = sandbox;
        vm.createContext(sandbox);

        // vm.runInContext returns the completion value of the script. The script's last construct
        // is a `var` declaration, which has no completion value, so the return is undefined on
        // both runs. Read the cached object off the sandbox instead.
        vm.runInContext(scriptSource(), sandbox);
        vm.runInContext(scriptSource(), sandbox);

        const cached = sandbox.XtreamLibraryConfig;
        assert.ok(cached && typeof cached === 'object', 'first eval did not publish XtreamLibraryConfig');
        assert.strictEqual(
            cached.pluginUniqueId, '63ba5fcd-c8ce-421a-83e8-ba0b11030d53',
            'cached object is not the plugin config object');
        assert.strictEqual(
            sandbox.globalThis.XtreamLibraryConfig, cached,
            'globalThis.XtreamLibraryConfig must point at the cached object');
        // Identity check: seed a fresh context with a sentinel object on globalThis.XtreamLibraryConfig
        // before eval. If the IIFE cache branch fires, eval returns the sentinel. If the cache is
        // broken, a fresh object is built and globalThis.XtreamLibraryConfig is replaced.
        const fresh = { globalThis: undefined };
        fresh.globalThis = fresh;
        vm.createContext(fresh);
        fresh.XtreamLibraryConfig = { sentinel: true };
        vm.runInContext(scriptSource(), fresh);
        assert.deepStrictEqual(
            fresh.XtreamLibraryConfig, { sentinel: true },
            'IIFE cache branch did not return the previously cached object');
    });

    await t.test('the outer binding is `var`, not `const`', () => {
        // Defensive structural check. The first non-comment line that introduces
        // XtreamLibraryConfig must be a `var` declaration; a `const` or `let` here is exactly what
        // makes the second eval throw SyntaxError.
        const source = scriptSource();
        const match = source.match(/^(\s*)(const|let|var)\s+XtreamLibraryConfig\b/m);
        assert.ok(match, 'XtreamLibraryConfig is not declared at top level');
        assert.strictEqual(
            match[2], 'var',
            'top-level XtreamLibraryConfig must be declared with `var` so redeclaration is a no-op');
    });
});
