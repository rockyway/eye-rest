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
  const content = readFileSync(CSPROJ, "utf8");
  const match = content.match(/<Version>([^<]+)<\/Version>/);
  if (!match) {
    console.error("ERROR: Could not read <Version> from", CSPROJ);
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

function collectFiles() {
  const files = [];
  const distDir = join(PROJECT_ROOT, "dist");
  const publishDir = join(PROJECT_ROOT, "publish");

  // macOS zip (prefer dist/ from bundle script, fall back to publish/)
  const macZipDist = join(distDir, "EyeRest-macOS-arm64.zip");
  const macZipPub = join(publishDir, "EyeRest-macOS-arm64.zip");
  if (existsSync(macZipDist)) {
    files.push({ path: macZipDist, name: "EyeRest-macOS-arm64.zip", type: "application/zip" });
  } else if (existsSync(macZipPub)) {
    files.push({ path: macZipPub, name: "EyeRest-macOS-arm64.zip", type: "application/zip" });
  }

  // Windows zip
  const winZip = join(publishDir, "EyeRest-Windows-x64.zip");
  if (existsSync(winZip)) {
    files.push({ path: winZip, name: "EyeRest-Windows-x64.zip", type: "application/zip" });
  }

  // Windows exe (standalone)
  const winExe = join(
    PROJECT_ROOT, "EyeRest.UI", "bin", "Release",
    "net8.0-windows10.0.19041.0", "win-x64", "publish", "EyeRest.exe"
  );
  if (existsSync(winExe)) {
    files.push({ path: winExe, name: "EyeRest.exe", type: "application/octet-stream" });
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
const files = collectFiles();
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
