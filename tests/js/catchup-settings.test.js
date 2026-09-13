// Copyright (C) 2024  Roland Breitschaft
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// GitHub #108. The global settings are read and written inline inside loadConfig/saveConfig, which
// are ApiClient promise chains rather than named functions, so the harness cannot drive them the
// way it drives updateActiveProviderFromUI. Asserting against a local copy of those lines would
// test the copy, not the page, so this checks the one thing that is both real and checkable: that
// every id the script binds actually exists in the markup.
//
// That is not a nicety. Both handlers reach ids unguarded, so one binding without a control throws
// and leaves the whole config page blank, which is what #101 looked like from the outside.

'use strict';

const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');

const WEB = path.join(__dirname, '..', '..', 'Jellyfin.Xtream.Library', 'Configuration', 'Web');
const JS = fs.readFileSync(path.join(WEB, 'config.js'), 'utf8');
const HTML = fs.readFileSync(path.join(WEB, 'config.html'), 'utf8');

const CATCHUP_IDS = [
    'chkEnableCatchup',
    'txtCatchupDays',
    'chkShowCatchupInJellyfin',
    'txtCatchupTimeShiftMinutes',
];

test('every catch-up control the script binds exists in the page', () => {
    for (const id of CATCHUP_IDS) {
        assert.ok(JS.includes(`getElementById('${id}')`), `config.js never binds ${id}`);
        assert.ok(HTML.includes(`id="${id}"`), `config.html has no control with id ${id}`);
    }
});

test('the catch-up settings are both read and written', () => {
    for (const id of CATCHUP_IDS) {
        const uses = JS.split(`getElementById('${id}')`).length - 1;
        assert.ok(uses >= 2, `${id} is bound ${uses} time(s); it needs a load and a save`);
    }
});

test('the time correction does not lose a negative value to a falsy default', () => {
    // `parseInt(...) || 0` turns -0 into 0 harmlessly but also turns any falsy parse into the
    // default, and the shape that actually bites is `config.X || 0` on load, which would discard a
    // stored 0 and re-read it as 0 while quietly masking a real value. Pin the guarded forms.
    assert.match(JS, /config\.CatchupTimeShiftMinutes != null \? config\.CatchupTimeShiftMinutes : 0/);
    assert.match(JS, /isNaN\(catchupShift\) \? 0 : catchupShift/);
});

test('the catch-up day limit agrees with what the server accepts', () => {
    // PluginConfiguration clamps to 1-30. The form said 14 until #108, so the page was refusing
    // values the server would have taken.
    assert.match(HTML, /id="txtCatchupDays"[\s\S]{0,200}max="30"/);
    assert.doesNotMatch(HTML, /id="txtCatchupDays"[\s\S]{0,200}max="14"/);
});

test('the Jellyfin catch-up toggle explains that it needs the other one too', () => {
    // Two settings for one feature is the cost of not changing what upgrading means for existing
    // users. It only works if the page says so.
    const section = HTML.slice(HTML.indexOf('id="chkShowCatchupInJellyfin"'));
    assert.match(section.slice(0, 1600), /Both have to be on/);
});
