<div align="center">
  <h1>pefix</h1>
  <p><em>Static loadability preflight for .NET publish, plugin, and Unity/BepInEx folders.</em></p>
</div>

<p align="center">
  <a href="https://github.com/FeathBow/pefix/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/FeathBow/pefix/ci.yml?style=flat-square&label=CI" alt="CI"></a>
  <a href="https://www.nuget.org/packages/pefix"><img src="https://img.shields.io/nuget/v/pefix?style=flat-square" alt="NuGet"></a>
  <img src="https://img.shields.io/badge/built%20on-.NET%2010-512BD4?style=flat-square" alt="Built on .NET 10">
  <img src="https://img.shields.io/badge/AOT-single%20native%20binary-512BD4?style=flat-square" alt="AOT single native binary">
  <img src="https://img.shields.io/badge/platforms-Linux%20%7C%20macOS%20%7C%20Windows-blue?style=flat-square" alt="Platforms Linux macOS Windows">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License: MIT"></a>
</p>

<p align="center">
  <a href="#quick-start">Quick start</a> &middot;
  <a href="#what-it-catches">What it catches</a> &middot;
  <a href="#why-pefix">Why pefix</a> &middot;
  <a href="#ci-gate">CI gate</a> &middot;
  <a href="#use-with-ai-coding-agents">AI agents</a> &middot;
  <a href="#unity--bepinex">Unity / BepInEx</a> &middot;
  <a href="#install-from-source">Build from source</a>
</p>

`pefix` is a single-binary CLI that runs static loadability preflights over folders of .NET assemblies before a host tries to load them. It resolves every assembly, type, member, field, and interface reference against the providers actually present in the folder, without `Assembly.Load`.

Each failure that would otherwise surface at runtime, such as a `MissingMethodException` after a package upgrade or a plugin silently skipped by its loader, is reported as a named issue with the files involved and the next step. The member, field, type, and implementation checks carry zero false positives across the .NET shared framework, so `--fail-on-issue` is safe in front of a deploy. When a publish output carries a `*.deps.json` manifest, pefix reads it so shared-framework assemblies (for example ASP.NET Core) are recognized as provided rather than reported missing, and so framework-dependent and self-contained publishes both gate cleanly.

`pefix` runs as a `dotnet` global tool or as a self-contained native binary for macOS ARM64, Linux x64, and Windows x64 with no .NET runtime dependency. It targets modern .NET: it analyzes folders of .NET Core, .NET 5 and later, and netstandard assemblies, and Unity/BepInEx mod folders on both the Mono and IL2CPP runtimes. Because it reads metadata statically it can inspect an assembly built for any target framework, but its loadability gate models the modern .NET folder-deployment world, not the .NET Framework GAC; an assembly targeting .NET Framework (net48 and earlier) is reported as incompatible with modern .NET rather than analyzed as a Framework app.

## Quick start

    dotnet tool install -g pefix

Gate a publish output or deployed `bin/`:

    pefix scan ./publish --profile publish-dir --fail-on-issue

Check a Unity/BepInEx plugin folder:

    pefix scan ./BepInEx/plugins --profile unity-bepinex

A scan with issues prints:

    Issues (1):
      - [missing_member] SharedLib: Method 'Shared.Api.Foo' (2 args) not found in SharedLib.dll; consumed by PluginA.dll.
        files: PluginA.dll, SharedLib.dll
        repair: assisted_fix
        next: Install provider and consumer assemblies built against the same API surface.
        risk: Full signature compatibility, reflection usage, and runtime load success are not proven.
      Verify: pefix scan <path> --json

The `repair:` line carries one of four repair classes:

| Repair class | Meaning |
| ------------ | ------- |
| `auto_fix` | the `pefix fix` header rewrite |
| `guided_fix` | mutation only with explicit user intent |
| `assisted_fix` | evidence for an external repair |
| `diagnostic_only` | never mutates |

## What it catches

Gate issues fail `--fail-on-issue`; advisory issues are reported but never gate.

| Issue code | Prevented runtime failure | Grade |
| ---------- | ------------------------- | ----- |
| `missing_ref` | FileNotFoundException at load | gate |
| `asm_conflict` | FileLoadException from version mismatch | gate |
| `dup_provider` | nondeterministic provider selection | gate |
| `missing_type` | TypeLoadException | gate |
| `missing_member` | MissingMethodException | gate |
| `missing_field` | MissingFieldException | gate |
| `missing_impl` | TypeLoadException from an unimplemented interface member | gate |
| `inaccessible_member` | MemberAccessException on internal/private access | gate in `publish-dir`, advisory elsewhere |
| `reflection_missing` | FileNotFoundException from a literal reflection load | gate in `publish-dir`, advisory elsewhere |
| `missing_native` | DllNotFoundException or native architecture mismatch | advisory |
| `bep_missing` | plugin skipped: hard dependency GUID absent | gate |
| `bep_casing` | plugin skipped: dependency GUID casing mismatch | gate |
| `bep_version_mismatch` | plugin skipped: dependency below declared version | gate |
| `bep_dup_guid` | chainloader GUID conflict | gate |
| `bep_loader_mismatch` | plugins built for the wrong BepInEx generation or runtime flavor | gate |
| `bep_il2cpp_api` | PlatformNotSupportedException from System.Reflection.Emit on IL2CPP | gate |
| `plugin_unresolved_chain` | plugin load failure deep in its dependency chain | gate |

`inaccessible_member` honors `InternalsVisibleTo` and `IgnoresAccessChecksTo`. `missing_native` recognizes platform naming variants (`foo.dll`, `libfoo.so`, `libfoo.dylib`) and well-known OS libraries.

## Why pefix

pefix is a deploy-time gate, not a build-time check. It resolves down to the member, field, type, and interface level, not just assembly versions, so it catches a `MissingMethodException` or `TypeLoadException` that a version-only check misses. Those checks carry **zero false positives** across the .NET shared framework, so `--fail-on-issue` is safe as a hard gate in front of a deploy. And it ships as a single native binary that needs no .NET runtime, so the same gate runs in CI, in an AI agent loop, and on a locked-down or air-gapped runner.

## CI gate

    - name: Gate publish output
      run: |
        dotnet tool install -g pefix
        pefix scan ./publish --profile publish-dir --fail-on-issue

Or use the Marketplace action, which downloads the native binary and needs no .NET SDK step:

    - uses: FeathBow/pefix@v1
      with:
        path: ./publish
        profile: publish-dir
        fail-on-issue: true

To adopt pefix on a folder with known, accepted issues, gate on new issues only:

    pefix scan ./publish --profile publish-dir --baseline pefix-baseline.txt --write-baseline
    pefix scan ./publish --profile publish-dir --baseline pefix-baseline.txt

The baseline is a sorted text file of accepted issue lines (`code|subject|file`). Later scans exit `1` only when a blocking issue is not in the baseline; baselined issues are still reported, and stale entries are listed without failing the gate.

Other gates: `--fail-on <status>` fails when any file reaches the given status from the status legend (`pefix scan ./mods --fail-on cautioned`), `--fail-on-conflict` fails on `asm_conflict` version conflicts, and `closure --fail-on-unresolved` fails on unresolved dependency chains. By default `scan --json` exits `0` even when integrity fails; gates are always explicit.

## Use with AI coding agents

`pefix` fits an agent loop. Its output is deterministic, and every issue ships machine-readable fields an agent can act on without a human: `code`, `files`, `next_steps`, `verify_command`, and `repair_class`. When an agent assembles or deploys .NET binaries it did not just compile from source (a published output, a plugin folder, a vendored `bin/`), it can verify loadability before running anything:

    pefix scan ./publish --profile publish-dir --json --fail-on-issue

The agent reads `gate.integrity` and `issues[]`; on a failure it follows each issue's `next_steps`, then re-runs the issue's `verify_command`. This catches what the agent's compile loop does not: a dependency missing from the deployed output, a version conflict among assembled binaries, or a member, type, or interface a present provider no longer ships (a `MissingMethodException` or `TypeLoadException` at load). For source the agent compiles itself, `dotnet build` already reports a missing member; pefix is the gate for the binary artifact the agent ships. The integration surface is the CLI plus the stable JSON contract; there is no server to run.

## Unity / BepInEx

Profiles tell the scan what the host provides and which loader is installed. Supported profiles: `unity-bepinex` (generation and flavor detected from the scanned tree), `unity-bepinex5`, `unity-bepinex6-mono`, `unity-bepinex6-il2cpp`, plus `dotnet-plugin` and `publish-dir` for general .NET folders.

    pefix scan ./BepInEx/plugins --profile unity-bepinex6-il2cpp

When `scan` sees `[BepInPlugin]` and `[BepInDependency]` metadata, it reports plugin GUIDs, helper libraries, hard dependencies, duplicate GUIDs, version mismatches, unresolved plugin `AssemblyRef` chains, and loader-target mismatches. Loader-target checks distinguish BepInEx 5 vs 6 and Mono vs IL2CPP by examining assembly references, without a BepInEx install, so the common case of valid plugins installed on the wrong loader is caught. Under an IL2CPP loader, plugins that reference `System.Reflection.Emit` are flagged before the runtime throws. Unity, BepInEx, and Harmony assemblies are treated as host-provided, so `UnityEngine.CoreModule` never appears as a missing reference.

## Going deeper

Inspect one assembly and fix its header:

    pefix inspect MyMod.dll
    pefix fix MyMod.dll          # dry-run by default
    pefix fix MyMod.dll --apply  # writes MyMod.dll.bak first

      Status:  PATCHED
      Summary: PE header patched to AnyCPU.
      Action:  Backup written to MyMod.dll.bak.

Trace the transitive dependency closure of a folder:

    pefix closure ./mods

      Unresolved chains:
        PluginA.dll
          → ModLib.dll v1.0.0.0        [resolved]
            → CoreUtils.dll v1.0.0.0   [resolved]
              → GameplayNet.dll v1.0.0.0  [MISSING]

Add `--tree` for the full transitive tree, where every node is tagged `[resolved]`, `[MISSING]`, `[cycle]`, or `[provided]` and host- or loader-provided leaves are not expanded:

    pefix closure ./mods --tree

      Dependency tree:
        PluginA.dll v1.2.0.0                      [resolved]
          → UnityEngine.CoreModule.dll v0.0.0.0   [provided]
          → ModLib.dll v1.0.0.0                    [resolved]
            → CoreUtils.dll v1.0.0.0               [resolved]
              → GameplayNet.dll v1.0.0.0           [MISSING]

Add `--orphans` to list assemblies no other scanned assembly references, as a starting point for trimming over-stuffed deployment folders; entry points, BepInEx plugins, satellite assemblies, and reflection-loaded assemblies are excluded, and the listing never gates.

Add `--dgml` to emit the dependency graph as DGML (Directed Graph Markup Language), the format Visual Studio opens as a dependency graph:

    pefix closure ./mods --dgml > deps.dgml

Add `--references` to `scan` for a full reference inventory, and `--json` to any command for machine-readable output with stable `schema_version`, reason codes, and issue codes. Three further mutation commands are guided fixes that print exact mutation targets and never run implicitly: `snstrip` strips strong-name signing, `redir` rewrites an `AssemblyRef` version in place, and `publicize` lifts member visibility for compile-time access.

`pefix pinvoke <path>` lists an assembly or folder's P/Invoke declarations grouped by native module (`DeclaringType.Method -> entry point`), a read-only inventory of the native imports that `missing_native` checks for presence. Add `--json` for the machine-readable form.

## Status legend

Every inspection produces one of five statuses:

| Status     | Meaning                                                       |
| ---------- | ------------------------------------------------------------- |
| compatible | Already portable, no action needed.                           |
| fixable    | Header can be rewritten by `pefix fix`.                       |
| cautioned  | Requires reading `repair_class`: `non_portable` may be a guided header rewrite with `--apply --force`; ReadyToRun, trimming, bundle, and native-binary findings are diagnostic-only. |
| unsafe     | Refused. Rewriting would not produce a working assembly.      |
| corrupt    | Not a valid PE file or malformed beyond inspection.           |

Each result also carries a stable `reason_code` printed in text and JSON output. Run `pefix --help` for the full option list and exit codes.

## What it fixes

`pefix fix` rewrites non-portable pure-IL `PE32+` managed headers and CorFlags so they match the layout the .NET loader expects across platforms.

The rewrite is byte-level. It does not touch metadata, embedded resources, or strong-name tokens, and it does not perform `ildasm`/`ilasm` round-tripping. After the rewrite, `pefix` re-inspects the file and validates the assembly manifest before reporting success.

Other mutation commands, such as `snstrip`, `redir`, and `publicize` (`publicise` alias), are guided-fix paths only when they report mutation targets. No-op outcomes such as `snstrip.outcome=unsigned` are diagnostic-only. With fully specified CLI flags, mutating guided commands are explicit guided-fix paths, not part of the `fixable` status contract.

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

## What a pass does not mean

A passing report means no supported static issue was found under the selected profiles. It does not certify runtime load success, simulate the BepInEx chainloader, observe host library search paths or runtime resolvers, model the .NET Framework GAC or app.config binding redirects, download packages, install DLLs, or mutate mod-manager profiles. The automatic rewrite contract is only the byte-level PE header fix; every other finding is assisted evidence for an external repair. The stable integration surface is the CLI, JSON output, reason codes, issue codes, repair classes, and exit codes.

## Install from source

    git clone https://github.com/FeathBow/pefix
    cd pefix
    dotnet publish src/PeFix/PeFix.csproj -c Release -r osx-arm64 \
      --self-contained -p:PublishAot=true -o ./out

The resulting `./out/pefix` is a self-contained native binary with no .NET runtime dependency. Replace `osx-arm64` with `linux-x64` or `win-x64` as needed. To run once with the .NET 10 SDK instead: `dotnet tool exec pefix -- inspect MyMod.dll`.

<details>
<summary>Machine output details</summary>

Embedded file results that can also be produced on their own keep their own `schema_version`, such as `scan.results[]`, `snstrip.results[]`, `fix.before`, `fix.after`, and refusal `before`. Directory scan issue codes include `missing_ref`, `missing_member`, `missing_field`, `missing_type`, `missing_impl`, `inaccessible_member`, `missing_native`, `dup_provider`, `asm_conflict`, `bep_missing`, `bep_casing`, `bep_version_mismatch`, `bep_dup_guid`, `bep_loader_mismatch`, `bep_il2cpp_api`, and `plugin_unresolved_chain`.

- `inspect` exits `0` only for `compatible` by default; non-compatible inspection results exit `1` after printing the report. Other report commands use `0` for command execution success unless an explicit gate or refusal applies.
- JSON `gate` reports directory integrity through `gate.integrity`, `gate.issue_count`, `gate.issue_codes`, `gate.blocking_file_count`, and `gate.blocking_file_reasons`. Directory issues and unsafe/corrupt file diagnostics both fail integrity; issue codes and file reason codes stay separate.
- `closure --json` reports `entry_assemblies`, `unresolved_chains`, `cycle_chains`, `total_refs_walked`, `provided_leaves`, and the compatible `framework_leaves`; `provided_leaves` counts all selected Host Profile provided leaves, while `framework_leaves` keeps the previous framework-only contract. `closure --fail-on-unresolved` is the explicit process gate for unresolved leaves. `closure --tree` adds a top-level `tree` array of entry roots with recursive `children`; each node carries `assembly`, `version`, and `kind` (`resolved`, `missing`, `cycle`, or `provided`). `closure --orphans` adds a top-level `orphans` array of relative file paths; the listing is advisory and does not affect exit codes. `closure --dgml` writes the dependency graph as DGML to stdout instead of the text or JSON report and never gates.
- `inspect` results expose `repair_class` and `repair_hint`; scan issues add `next_steps`, `verify_command`, and `unverified_risks`. A `reflection_missing` issue raised from a static constructor carries `in_static_ctor: true`, marking it as a `TypeInitializationException` that makes the type unusable rather than a catchable per-call failure.
- `scan --profile unity-bepinex --json` includes `profiles.host=unity-bepinex` and `profiles.artifact=plugin-folder`; per-file BepInEx states include `plugin`, `helper_library`, `blocked_missing_bep_dependency`, `blocked_guid_case_mismatch`, `blocked_bep_version_mismatch`, and `risk_unresolved_assembly_chain`.
- `repair_class` separates automatic mutation from diagnostics: `auto_fix` is the `pefix fix` status contract, `guided_fix` requires explicit user-supplied mutation intent, `assisted_fix` emits evidence for external repair, and `diagnostic_only` never mutates artifacts.
- ReadyToRun, trimmable, and single-file bundle findings are `diagnostic_only`; `--apply --force` does not turn them into fixes.
- Guided-fix JSON exposes `repair_class`, `unverified_risks`, and exact mutation `targets`.
- `snstrip.outcome` reports the command result. Single-file values include `dry_run`, `patched`, `unsigned`, and `dep_refused`; directory values include `dry_run`, `patched`, `refused`, and `unchanged`.
- Directory `snstrip --json` reports dependency files with rewrite targets at top level through `deps_patched` and `deps`; use top-level `dry_run` / `outcome` to distinguish planned rewrites from applied rewrites.
- `scan --references` adds a top-level `references` array; `scan --baseline <file> --json` adds a top-level `baseline` object with `path`, `matched`, `new`, and `stale`; the gate exits `1` only when `new` is non-empty. `--write-baseline` rewrites the baseline file and does not gate. None of these change the default JSON shape.

</details>
