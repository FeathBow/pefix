# pefix

Dependency Doctor for .NET plugin and Unity/BepInEx mod folders, with one safe automatic header fix.

[![CI](https://github.com/FeathBow/pefix/actions/workflows/ci.yml/badge.svg)](https://github.com/FeathBow/pefix/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/pefix.svg)](https://www.nuget.org/packages/pefix)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

`pefix` is a single-binary CLI that runs static loadability preflights over .NET plugin folders before a host tries to load them. It inspects PE header layout, target framework, ReadyToRun, trimming, single-file bundles, platform restrictions, mixed-mode native code, reference assemblies, BepInEx plugin metadata, managed dependency closure, duplicate providers, version conflicts, and hard BepInEx plugin dependency rules. `pefix scan ./BepInEx/plugins --profile unity-bepinex` selects Unity/BepInEx host assumptions while keeping plugin-folder artifact rules explicit. The automatic rewrite contract is still only the byte-level PE header fix for pure-IL `PE32+` assemblies; BepInEx dependency, casing, version, duplicate GUID, and unresolved chain findings are assisted fixes with evidence and verification commands.

Static loadability boundary: a passing report means no supported static issue was found under the selected profiles. It does not certify runtime load success, simulate BepInEx, download packages, install DLLs, or mutate mod-manager profiles.

The stable integration surface is the CLI, JSON output, reason codes, issue codes, repair classes, and exit codes.

## Install

As a .NET global tool:

    dotnet tool install -g pefix

Run once with .NET 10 SDK, using the same arguments as `pefix`:

    dotnet tool exec pefix -- inspect MyMod.dll

Or build from source:

    git clone https://github.com/FeathBow/pefix
    cd pefix
    dotnet publish src/PeFix/PeFix.csproj -c Release -r osx-arm64 \
      --self-contained -p:PublishAot=true -o ./out

The resulting `./out/pefix` is a self-contained native binary with no .NET runtime dependency. Replace `osx-arm64` with `linux-x64` or `win-x64` as needed.

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
      Action:  Run pefix fix <path> --apply for entries marked fixable.
      Counts:  compatible: 1  fixable: 1  cautioned: 0  unsafe: 1  corrupt: 0  issues: 0

      Blocking Issues: none found under supported static checks.
      Static Boundary: Runtime load success is not certified.

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
      Summary: 3 entry assemblies, 12 transitive references, 1 unresolved leaf, 0 cycles.
      Action:  Add the missing dependencies to the scanned directory or restore their packages.

      Unresolved chains:
        PluginA.dll
          → ModLib.dll v1.0.0.0        [resolved]
            → CoreUtils.dll v1.0.0.0   [resolved]
              → GameplayNet.dll v1.0.0.0  [MISSING]

Unity, BepInEx, Harmony, and other host- or loader-provided assemblies are filtered as provided leaves, so references such as `UnityEngine.CoreModule` do not appear as unresolved chains.

Check a BepInEx plugin directory:

    pefix scan ./BepInEx/plugins --profile unity-bepinex

When `scan` sees `[BepInPlugin]` and `[BepInDependency]` metadata, it reports plugin GUIDs, helper libraries, hard dependencies, duplicate plugin GUIDs, version mismatches, and plugin `AssemblyRef` chains with unresolved leaves. Blocking issues are printed before the audit groups so the report can be pasted into a support thread:

    Blocking Issues (1):
      - [bep_missing] need.hard: test.miss requires BepInEx plugin need.hard, but no matching plugin GUID was found.
        files: F27_bep_miss.dll
        repair: assisted_fix
        next: Install the missing BepInEx plugin dependency into the scanned plugins directory.
        verify: pefix scan <path> --json
        risk: Plugin ABI compatibility and runtime chainloader success are not proven.

    Other supported issue codes include `bep_casing`, `bep_version_mismatch`, `bep_dup_guid`, and `plugin_unresolved_chain`.

BepInEx support is static. `pefix` does not run the game, simulate the chainloader, download packages, or install DLLs.

Add `--json` to any command for machine-readable output. JSON responses include `schema_version`; file results carry stable `reason_code` values, and directory issues carry stable issue codes. By default, `scan --json` exits `0` after writing a report even when directory integrity fails; use explicit fail gates for CI.

<details>
<summary>Machine output details</summary>

Embedded file results that can also be produced on their own keep their own `schema_version`, such as `scan.results[]`, `snstrip.results[]`, `fix.before`, `fix.after`, and refusal `before`. Directory scan issue codes include `missing_ref`, `dup_provider`, `asm_conflict`, `bep_missing`, `bep_casing`, `bep_version_mismatch`, `bep_dup_guid`, and `plugin_unresolved_chain`.

- `inspect` exits `0` only for `compatible` by default; non-compatible inspection results exit `1` after printing the report. Other report commands use `0` for command execution success unless an explicit gate or refusal applies.
- JSON `gate` reports directory integrity through `gate.integrity`, `gate.issue_count`, and `gate.issue_codes`.
- `closure --json` reports `entry_assemblies`, `unresolved_chains`, `cycle_chains`, `total_refs_walked`, `provided_leaves`, and the compatible `framework_leaves`; `provided_leaves` counts all selected Host Profile provided leaves, while `framework_leaves` keeps the previous framework-only contract. `closure --fail-on-unresolved` is the explicit process gate for unresolved leaves.
- `inspect` results expose `repair_class` and `repair_hint`; scan issues add `next_steps`, `verify_command`, and `unverified_risks`.
- `scan --profile unity-bepinex --json` includes `profiles.host=unity-bepinex` and `profiles.artifact=plugin-folder`; per-file BepInEx states include `plugin`, `helper_library`, `blocked_missing_bep_dependency`, `blocked_guid_case_mismatch`, `blocked_bep_version_mismatch`, and `risk_unresolved_assembly_chain`.
- `repair_class` separates automatic mutation from diagnostics: `auto_fix` is the `pefix fix` status contract, `guided_fix` requires explicit user-supplied mutation intent, `assisted_fix` emits evidence for external repair, and `diagnostic_only` never mutates artifacts.
- ReadyToRun, trimmable, and single-file bundle findings are `diagnostic_only`; `--apply --force` does not turn them into fixes.
- Guided-fix JSON exposes `repair_class`, `unverified_risks`, and exact mutation `targets`.
- `snstrip.outcome` reports the command result. Single-file values include `dry_run`, `patched`, `unsigned`, and `dep_refused`; directory values include `dry_run`, `patched`, `refused`, and `unchanged`.
- Directory `snstrip --json` reports dependency files with rewrite targets at top level through `deps_patched` and `deps`; use top-level `dry_run` / `outcome` to distinguish planned rewrites from applied rewrites.
- For CI gates, use `--fail-on <status>` for file status thresholds, `--fail-on-conflict` for version conflicts, and `closure --fail-on-unresolved` for unresolved chains.

</details>

## Status legend

Every inspection produces one of five statuses:

| Status     | Meaning                                                       |
| ---------- | ------------------------------------------------------------- |
| compatible | Already portable, no action needed.                           |
| fixable    | Header can be rewritten by `pefix fix`.                       |
| cautioned  | Requires reading `repair_class`: `non_portable` may be a guided header rewrite with `--apply --force`; ReadyToRun, trimming, and bundle findings are diagnostic-only. |
| unsafe     | Refused. Rewriting would not produce a working assembly.      |
| corrupt    | Not a valid PE file or malformed beyond inspection.           |

Each result also carries a stable `reason_code` printed in text and JSON output. Run `pefix --help` for the full option list and exit codes.

## What it fixes

`pefix fix` rewrites non-portable pure-IL `PE32+` managed headers and CorFlags so they match the layout the .NET loader expects across platforms.

The rewrite is byte-level. It does not touch metadata, embedded resources, or strong-name tokens, and it does not perform `ildasm`/`ilasm` round-tripping. After the rewrite, `pefix` re-inspects the file and validates the assembly manifest before reporting success.

Other mutation commands, such as `snstrip`, `redir`, and `publicize` (`publicise` alias), are Guided-fix paths only when they report mutation targets. No-op outcomes such as `snstrip.outcome=unsigned` are diagnostic-only. With fully specified CLI flags, mutating guided commands are Explicit Guided-fix paths, not part of the `fixable` status contract.

## What it refuses

`pefix fix` will not perform the automatic header rewrite in any of the following cases:

| Refusal               | Why                                                           |
| --------------------- | ------------------------------------------------------------- |
| mixed-mode (C++/CLI)  | Contains native code; needs `ijwhost.dll` or VC++ runtime.    |
| reference assembly    | Not a runtime artifact; cannot be executed.                   |
| satellite assembly    | Localized resource container, not a code module.              |
| multi-module assembly | Not supported on .NET Core or .NET 5+.                        |
| corrupt / non-PE      | Not a parseable PE file.                                      |
| strong-named          | Cautioned. `--apply --force` accepts that the strong-name will break. |
| native dependencies   | Cautioned. P/Invoke targets must be available on the host.    |

Direction is one-way: `pefix` rewrites `PE32+` managed headers to `PE32 I386`. Already compatible assemblies are left untouched.

## Safety

Before an in-place rewrite with backups enabled, `pefix` copies the original to `MyMod.dll.bak`. Single-file writes are staged and verified before commit. Guided mutation batch writes use best-effort rollback and are not a true atomic transaction; `pefix fix <dir>` still processes files one at a time. If a `.bak` already exists, apply commands refuse rather than overwrite.
