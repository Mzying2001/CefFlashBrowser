const { readFile, writeFile } = require('node:fs/promises');
const { join } = require('node:path');
const buildCoreJs = require('core-js-builder');
const { minify } = require('terser');

async function build() {
    const bundle = await buildCoreJs({ modules: 'core-js/stable', targets: 'chrome 87' });
    const { code } = await minify(`
        (function () {
            'use strict';
            const marker = Symbol.for('CefFlashBrowser.core-js');
            if (globalThis[marker]) return;

            Object.defineProperty(globalThis, marker, { value: true });
            try {
                ${bundle}
            } catch (error) {
                console.warn('CefFlashBrowser could not install core-js:', error);
            }
        })();
    `);

    const { version } = require('core-js/package.json');
    const license = await readFile(require.resolve('core-js/LICENSE'), 'utf8');
    const header = `/*! core-js ${version} (stable, Chrome 87) | MIT | https://core-js.io */`;
    await writeFile(join(__dirname, 'core-js.min.js'), `${header}\n${code}\n`);
    await writeFile(join(__dirname, 'LICENSE.core-js.txt'), license);
}

build().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
