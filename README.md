# pefix

Static portability and load-failure diagnostics for .NET assemblies, with one safe header fix.

[![CI](https://github.com/FeathBow/pefix/actions/workflows/ci.yml/badge.svg)](https://github.com/FeathBow/pefix/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/pefix.svg)](https://www.nuget.org/packages/pefix)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

`pefix` is a single-binary CLI that inspects .NET assemblies for portability and load-failure causes: PE header layout, target framework, ReadyToRun, trimming, single-file bundles, platform restrictions, mixed-mode native code, reference assemblies, BepInEx plugin metadata, and more. Directory scans also surface missing managed references, duplicate providers, version conflicts, and missing hard BepInEx plugin dependencies. `pefix closure` traces transitive `AssemblyRef` chains. The single rewrite contract is the byte-level PE header fix for pure-IL `PE32+ AMD64` assemblies; everything else is reported with stable reason and issue codes plus a remediation hint.

## Install

As a .NET global tool:

    dotnet tool install -g pefix

Run once with .NET 10 SDK, using the same arguments as `pefix`:

    dotnet tool exec pefix --yes -- MyMod.dll

Or build from source:

    git clone https://github.com/FeathBow/pefix
    cd pefix
    dotnet publish src/PeFix/PeFix.csproj -c Release -r osx-arm64 \
      --self-contained -p:PublishAot=true -o ./out

The resulting `./out/PeFix` is a self-contained native binary with no .NET runtime dependency. Replace `osx-arm64` with `linux-x64` or `win-x64` as needed.

## Quick start

Inspect a single assembly:

    pefix inspect MyMod.dll

Output:

    pefix MyMod.dll

      Status:  FIXABLE
      Summary: This assembly uses a platform-specific header, but the managed code is portable and can be fixed.
      Action:  Run: pefix fix MyMod.dll --apply

      Details:
        PE Format:      PE32+ (AMD64)
        IL Only:        Yes
        ...
        Status:         fixable

Preview a fix (dry-run by default):

    pefix fix MyMod.dll

Apply the fix:

    pefix fix MyMod.dll --apply

Output:

    pefix MyMod.dll fix

      Status:  PATCHED
      Summary: PE header patched to AnyCPU.
      Action:  Backup written to MyMod.dll.bak.

      Details:
        PE Format:        PE32+ (AMD64)
        Status Before:    not compatible
        Status After:     compatible
        Backup:           MyMod.dll.bak
        Verify:           re-inspection passed

Scan a directory:

    pefix scan ./mods

Output:

    pefix mods

      Summary: Scanned 3 candidate files. 2 require attention.
      Action:  Run pefix fix <path> --apply for entries marked fixable or cautioned.
      Counts:  compatible: 1  fixable: 1  cautioned: 0  unsafe: 1  corrupt: 0  issues: 0

      Group: portability
        - Compatible.dll [compatible] reason=portable action=none
        - X64OnlyManaged.dll [fixable] reason=non_portable action=fix
          why: This assembly uses a platform-specific header, but the managed code is portable and can be fixed.

      Group: ref_assembly
        - Reference.dll [unsafe] reason=ref_assembly action=blocked
          why: Reference assembly, not a runtime assembly.

Trace transitive dependency closure:

    pefix closure ./mods

Output:

    pefix mods closure

      Status:  UNRESOLVED
      Summary: 3 entry assemblies, 12 transitive references, 2 unresolved leaves, 0 cycles.
      Action:  Add the missing dependencies to the scanned directory or restore their packages.

      Unresolved chains:
        PluginA.dll
          -> ModLib.dll v1.0.0.0        [resolved]
            -> CoreUtils.dll v1.0.0.0   [resolved]
              -> UnityEngine.CoreModule.dll v0.0.0.0  [MISSING]

Check a BepInEx plugin directory:

    pefix scan ./BepInEx/plugins

When `scan` sees `[BepInPlugin]` and `[BepInDependency]` metadata, it reports plugin GUIDs and hard dependencies. Missing hard dependencies appear as directory issues:

    BepInEx deps (1):
      - test.miss requires BepInEx plugin need.hard
        Install the missing BepInEx plugin dependency into the scanned plugins directory.

BepInEx support is static. `pefix` does not run the game, simulate the chainloader, download packages, or install DLLs.

Add `--json` to any command for machine-readable output.

## Machine output

`inspect --json`, `scan --json`, and `closure --json` include `schema_version`. File results carry stable `reason_code` values. Directory scan issues carry stable codes such as `missing_ref`, `dup_provider`, `asm_conflict`, and `bep_missing`.

- Process exit reports command execution success, or an explicit CLI gate.
- JSON `gate` reports directory integrity through `gate.integrity`, `gate.issue_count`, and `gate.issue_codes`.

By default, `scan --json` exits `0` after writing a report even when `gate.integrity` is `fail`. Use `--fail-on <status>` for file status thresholds, `--fail-on-conflict` for version conflicts, and `closure --fail-on-unresolved` for unresolved chains.

## Status legend

Every inspection produces one of five statuses:

| Status     | Meaning                                                       |
| ---------- | ------------------------------------------------------------- |
| compatible | Already portable, no action needed.                           |
| fixable    | Header can be rewritten in place.                             |
| cautioned  | Could be rewritten but requires explicit consent (`--apply --force`). |
| unsafe     | Refused. Rewriting would not produce a working assembly.      |
| corrupt    | Not a valid PE file or malformed beyond inspection.           |

Each result also carries a stable `reason_code` printed in text and JSON output. Run `pefix --help` for the full option list and exit codes.

## What it fixes

`pefix` rewrites the PE header and CorFlags of pure-IL `PE32+ AMD64` assemblies so they match the layout the .NET loader expects across platforms.

The rewrite is byte-level. It does not touch metadata, embedded resources, or strong-name tokens, and it does not perform `ildasm`/`ilasm` round-tripping. After the rewrite, `pefix` re-inspects the file and validates the assembly manifest before reporting success.

## What it refuses

`pefix` will not rewrite the header in any of the following cases:

| Refusal               | Why                                                           |
| --------------------- | ------------------------------------------------------------- |
| mixed-mode (C++/CLI)  | Contains native code; needs `ijwhost.dll` or VC++ runtime.    |
| reference assembly    | Not a runtime artifact; cannot be executed.                   |
| satellite assembly    | Localized resource container, not a code module.              |
| multi-module assembly | Not supported on .NET Core or .NET 5+.                        |
| corrupt / non-PE      | Not a parseable PE file.                                      |
| strong-named          | Cautioned. `--apply --force` accepts that the strong-name will break. |
| native dependencies   | Cautioned. P/Invoke targets must be available on the host.    |

Direction is one-way: `pefix` rewrites `PE32+ AMD64` to `PE32 I386`. Already compatible assemblies are left untouched.

## Safety

Before any in-place rewrite `pefix` copies the original to `MyMod.dll.bak`. The new bytes are written via an atomic temp-file rename, so a failed write never leaves a half-written assembly. If a `.bak` already exists, `pefix` refuses rather than overwrite.
