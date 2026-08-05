# 010 — `vpk` SIGKILLed on the macOS build host (wrong .NET runtime)

**Hit:** 2026-08-05, building macOS artifacts for v1.6.1. Six consecutive failures.

**Fixed by:** running vpk on a **real .NET 9** runtime instead of rolling it forward onto .NET 10
(`scripts/publish-velopack-mac.sh`).

---

## Symptom

`vpk pack` dies with `Killed: 9` (SIGKILL, exit 137):

```
./scripts/publish-velopack-mac.sh: line 89: 61413 Killed: 9   vpk "${VPK_ARGS[@]}"
```

The stage varies between runs — that is the distinguishing feature:

| attempt | died at |
|---|---|
| 1 | post-process (after notarize + delta + installer had all succeeded) |
| 3, 4 | "Preparing to Notarize" |
| 5, 6 | "Code signing application bundle recursively" |

SIGKILL is not catchable, so **no crash report is written for vpk** and there is nothing to read.

## Root cause

vpk is a **net9** tool. The build host had runtimes **8 and 10 but no 9**, and the script forced
it to run anyway:

```bash
# Allow vpk (net9 tool) to run on .NET 10 without needing .NET 9 installed
export DOTNET_ROLL_FORWARD=LatestMajor
```

Running vpk under that unsupported major roll-forward is what killed it. A real .NET 9 runtime was
already installed via Homebrew (`/opt/homebrew/Cellar/dotnet@9/9.0.114/libexec`, runtime 9.0.13)
and simply unused. `rephlo-desktop`, which builds reliably on the **same Mac**, differs in exactly
this one respect — it points `DOTNET_ROOT` at that Homebrew .NET 9.

With the real runtime, the build completed in 49 s, first try, including notarization.

## The misleading evidence

The only crash report produced was **notarytool**, aborting here:

```
Foundation  -[NSConcreteFileHandle writeData:]
Foundation  _NSFileHandleRaiseOperationExceptionWhileReading
libc++abi   __cxa_throw → abort()      # SIGABRT, "Abort trap: 6"
```

That is `EPIPE`: notarytool writing into a pipe whose reader (vpk) had just been killed. It is a
**symptom, not the cause**, and it pulls the investigation toward notarization and keychain state,
which is where a lot of time went. If vpk dies with SIGKILL, suspect the runtime, not the child
process that reports an error.

## Ruled out (do not re-investigate these)

- **Memory** — `sudo purge` freed 12.9 GB (free pages 63 K → 790 K); failed identically after.
- **Keychain / notary credentials** — an `xcrun notarytool history` preflight passed in the same
  session immediately before the build, and Apple **Accepted** submission
  `2026-08-05T17:20:43Z` while vpk died locally anyway.
- **Stale host state** — failed the same way 3 minutes after a full reboot with 7.5 GB free.
- **Delta base** — `EyeRest-1.6.0-osx-full.nupkg` was present; the 1.6.0 → 1.6.1 delta built fine
  (265 files) in the run that got furthest.
- **Disk** — 14 GiB free; `/tmp/velopack` only 228 MB.

## Gotchas found while fixing it

- **Scope `DOTNET_ROOT` to vpk only.** Exporting it globally also redirects `dotnet publish` onto a
  different SDK, changing how the shipped binary is built. The script uses an `env` prefix on the
  vpk invocation instead.
- **Use `LatestPatch`, not `Disable`.** vpk's runtimeconfig asks for `9.0.0`; Homebrew ships
  `9.0.13`. `Disable` blocks even the patch bump and fails with *"You must install or update .NET
  to run this application"* while listing the runtime it just refused.
- **Restore the tool under the SDK that will run it.** Each dotnet install has its own local-tool
  store, so `dotnet tool restore` with the system SDK leaves the .NET 9 one saying *'Run "dotnet
  tool restore" to make the "vpk" command available.'*
- **`setsid` does not exist on macOS** (it is a Linux utility). A `nohup setsid …` attempt to
  detach the build failed instantly and silently.

## Aftermath for v1.6.1

The first publish shipped Windows + Linux at 1.6.1 with the osx channel carrying the previous
1.6.0 feed. Once this was fixed, macOS 1.6.1 was built and merged onto the same tag — which
required `gh release delete-asset` first, since vpk cannot overwrite an existing release file.
The unnotarized `.pkg` was removed from `assets.osx.json` before upload, as in prior releases.
