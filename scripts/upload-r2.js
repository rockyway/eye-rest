#!/usr/bin/env node

/**
 * Upload release binaries to Cloudflare R2
 * Writes to both v{VERSION}/ and latest/
 * Queries real bucket size from Cloudflare and blocks upload if approaching 9.5 GB
 *
 * Usage: node scripts/upload-r2.js
 *        bun scripts/upload-r2.js
 */

import { execSync } from "child_process";
import { readFileSync, statSync, existsSync } from "fs";
import { join, dirname } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT = join(__dirname, "..");
const CSPROJ = join(PROJECT_ROOT, "EyeRest.UI", "EyeRest.UI.csproj");
const BUCKET = "eyerest-downloads";
const MAX_BUCKET_BYTES = 9.5 * 1024 * 1024 * 1024; // 9.5 GB

function formatSize(bytes) {
  if (bytes >= 1024 * 1024 * 1024)
    return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
  return `${Math.round(bytes / 1024 / 1024)} MB`;
}

function parseSizeString(sizeStr) {
  // Parse "350 MB", "1.2 GB", "500 KB" etc. to bytes
  const match = sizeStr.trim().match(/([\d.]+)\s*(KB|MB|GB|TB|B)/i);
  if (!match) return 0;
  const value = parseFloat(match[1]);
  const unit = match[2].toUpperCase();
  const multipliers = { B: 1, KB: 1024, MB: 1024 ** 2, GB: 1024 ** 3, TB: 1024 ** 4 };
  return Math.round(value * (multipliers[unit] || 1));
}

function readVersion() {
  // Explicit arg wins. The csproj carries no <Version> — it is supplied via -p:Version at
  // publish time (see Directory.Build.props), so the csproj read is only a legacy fallback.
  const argVersion = process.argv[2];
  if (argVersion) return argVersion.replace(/^v/, "");

  const content = readFileSync(CSPROJ, "utf8");
  const match = content.match(/<Version>([^<]+)<\/Version>/);
  if (!match) {
    console.error(`ERROR: no <Version> in ${CSPROJ} — pass it explicitly: bun scripts/upload-r2.js 1.6.0`);
    process.exit(1);
  }
  return match[1];
}

function getBucketSize() {
  try {
    const output = execSync(
      `wrangler r2 bucket info ${BUCKET} --json`,
      { stdio: "pipe", env: { ...process.env, CLOUDFLARE_API_TOKEN: "" } }
    ).toString();
    const info = JSON.parse(output);
    return {
      bytes: parseSizeString(info.bucket_size || "0"),
      objects: parseInt(info.object_count || "0", 10),
      source: "cloudflare",
    };
  } catch {
    return { bytes: 0, objects: 0, source: "unavailable" };
  }
}

function wranglerUpload(key, filePath, contentType) {
  const cmd = [
    "wrangler", "r2", "object", "put",
    `${BUCKET}/${key}`,
    "--file", `"${filePath}"`,
    "--content-type", contentType,
    "--remote",
  ].join(" ");

  execSync(cmd, {
    stdio: "pipe",
    env: { ...process.env, CLOUDFLARE_API_TOKEN: "" },
  });
}

function collectFiles(version) {
  const files = [];
  const distDir = join(PROJECT_ROOT, "dist");
  const publishDir = join(PROJECT_ROOT, "publish");
  const velopackDir = join(PROJECT_ROOT, "releases");

  // ── Velopack artifacts (releases/) — the CURRENT pipeline ──────────────────
  // These are byte-identical to the GitHub release assets and are mirrored under the SAME
  // names, so dl.eyerest.net/latest/<asset> is a drop-in fallback for
  // github.com/rockyway/eye-rest/releases/latest/download/<asset> for users who can't reach
  // GitHub. Only the three assets the download page offers are mirrored — the bucket has a
  // 9.5 GB cap and every file is written twice (v{version}/ and latest/).
  const velopackMirror = [
    { file: "EyeRest-win-Setup.exe", type: "application/octet-stream" },
    { file: "EyeRest-osx-Portable.zip", type: "application/zip" },
    { file: "EyeRest.AppImage", type: "application/octet-stream" },
  ];
  for (const { file, type } of velopackMirror) {
    const p = join(velopackDir, file);
    if (existsSync(p)) files.push({ path: p, name: file, type });
  }

  // macOS zip — publish-release.sh emits a versioned name in dist/.
  // Fall back to legacy unversioned names in dist/ then publish/.
  // The public R2 object name stays unversioned so download URLs are stable
  // (files are namespaced under v{version}/ and latest/ when uploaded).
  const macCandidates = [
    join(distDir, `BlinkTwiceEyeRest-v${version}-macOS-arm64.zip`),
    join(distDir, "BlinkTwiceEyeRest-macOS-arm64.zip"),
    join(publishDir, "BlinkTwiceEyeRest-macOS-arm64.zip"),
  ];
  const macZip = macCandidates.find(existsSync);
  if (macZip) {
    files.push({ path: macZip, name: "BlinkTwiceEyeRest-macOS-arm64.zip", type: "application/zip" });
  }

  // Windows zip — publish-release.sh emits the versioned portable name in dist/.
  // Fall back to the legacy publish/ name.
  const winCandidates = [
    join(distDir, `BlinkTwiceEyeRest-v${version}-windows-x64-portable.zip`),
    join(publishDir, "BlinkTwiceEyeRest-Windows-x64.zip"),
  ];
  const winZip = winCandidates.find(existsSync);
  if (winZip) {
    files.push({ path: winZip, name: "BlinkTwiceEyeRest-Windows-x64.zip", type: "application/zip" });
  }

  // Windows exe (standalone)
  const winExe = join(
    PROJECT_ROOT, "EyeRest.UI", "bin", "Release",
    "net8.0-windows10.0.19041.0", "win-x64", "publish", "BlinkTwiceEyeRest.exe"
  );
  if (existsSync(winExe)) {
    files.push({ path: winExe, name: "BlinkTwiceEyeRest.exe", type: "application/octet-stream" });
  }

  // Linux AppImage — emitted by publish-velopack-linux.sh into releases/.
  // The public R2 object name stays unversioned so download URLs are stable.
  const releasesDir = join(PROJECT_ROOT, "releases");
  const linuxCandidates = [
    join(releasesDir, `BlinkTwiceEyeRest-${version}-linux-x64.AppImage`),
    join(releasesDir, "BlinkTwiceEyeRest-linux-x64.AppImage"),
  ];
  const linuxAppImage = linuxCandidates.find(existsSync);
  if (linuxAppImage) {
    files.push({ path: linuxAppImage, name: "BlinkTwiceEyeRest-linux-x64.AppImage", type: "application/octet-stream" });
  }

  return files;
}

// ── Main ──────────────────────────────────────

const version = readVersion();
console.log("=== R2 Upload ===");
console.log(`  Version: v${version}`);
console.log(`  Bucket:  ${BUCKET}`);
console.log();

// Query real bucket size from Cloudflare
console.log("Checking bucket size...");
const bucket = getBucketSize();

if (bucket.source === "cloudflare") {
  console.log(`  Bucket size: ${formatSize(bucket.bytes)} (${bucket.objects} objects)`);
} else {
  console.log("  WARNING: Could not query bucket size from Cloudflare");
  console.log("  Proceeding with upload size check only");
}
console.log(`  Limit:       ${formatSize(MAX_BUCKET_BYTES)}`);

// Collect files
const files = collectFiles(version);
if (files.length === 0) {
  console.error("ERROR: No release binaries found. Run the build/bundle scripts first.");
  process.exit(1);
}

// Calculate upload size
// latest/ files overwrite existing, so only versioned copies add net new storage
let uploadNetBytes = 0;
let uploadTotalBytes = 0;
console.log();
console.log("Files to upload:");
for (const f of files) {
  const size = statSync(f.path).size;
  f.size = size;
  uploadTotalBytes += size * 2;
  uploadNetBytes += size; // only versioned copy is net new (latest/ overwrites)
  console.log(`  ${f.name} (${formatSize(size)})`);
}

const projected = bucket.bytes + uploadNetBytes;
console.log();
console.log(`  Net new storage:  ${formatSize(uploadNetBytes)}`);
console.log(`  Projected total:  ${formatSize(projected)} / ${formatSize(MAX_BUCKET_BYTES)}`);

if (projected > MAX_BUCKET_BYTES) {
  console.log();
  console.error("ERROR: Upload would exceed 9.5 GB bucket limit!");
  console.error(`  Current:   ${formatSize(bucket.bytes)}`);
  console.error(`  Adding:    ${formatSize(uploadNetBytes)}`);
  console.error(`  Projected: ${formatSize(projected)}`);
  console.error();
  console.error("Delete old versions to free space:");
  console.error(`  wrangler r2 object delete ${BUCKET}/v<old-version>/<file> --remote`);
  process.exit(1);
}

console.log();

// Upload
for (const f of files) {
  console.log(`Uploading ${f.name}...`);

  process.stdout.write(`  -> v${version}/${f.name} ... `);
  wranglerUpload(`v${version}/${f.name}`, f.path, f.type);
  console.log("done");

  process.stdout.write(`  -> latest/${f.name} ... `);
  wranglerUpload(`latest/${f.name}`, f.path, f.type);
  console.log("done");

  console.log();
}

console.log("Done! Files available at:");
console.log(`  https://dl.eyerest.net/v${version}/`);
console.log(`  https://dl.eyerest.net/latest/`);
