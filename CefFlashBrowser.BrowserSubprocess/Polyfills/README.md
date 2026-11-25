# Chromium 87 polyfills

The renderer embeds `core-js.min.js`. To regenerate it and its license, run `npm ci --ignore-scripts` and `npm run build` in this directory. Commit the generated files with any dependency or generator changes. MSBuild uses the checked-in bundle directly.
