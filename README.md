# pefix

Make x64-only .NET DLLs load on macOS and Linux — without breaking the safe ones.

[![CI](https://github.com/FeathBow/pefix/actions/workflows/ci.yml/badge.svg)](https://github.com/FeathBow/pefix/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/pefix.svg)](https://www.nuget.org/packages/pefix)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

`pefix` is a single-binary CLI that diagnoses and rewrites the PE header of pure-IL .NET assemblies built as `PE32+ AMD64` (x64-only) so they load on any .NET runtime, including macOS arm64 and Linux x64. Mixed-mode, native dependencies, strong-named, and other categories outside the rewrite contract are refused with a reason.

## Install

As a .NET global tool:

    dotnet tool install -g pefix

Or build from source:

    git clone https://github.com/FeathBow/pefix
    cd pefix
    dotnet publish src/PeFix/PeFix.csproj -c Release -r osx-arm64 \
      --self-contained -p:PublishAot=true -o ./out

The resulting `./out/PeFix` is a self-contained native binary with no .NET runtime dependency. Replace `osx-arm64` with `linux-x64` or `win-x64` as needed.

## Quick start

Inspect a single assembly:

    pefix MyMod.dll

Output:

    pefix MyMod.dll
    
      Status:  FIXABLE
      Summary: This assembly uses a platform-specific header, but the managed code is portable and can be fixed.
      Action:  Run: pefix MyMod.dll --fix
    
      Details:
        PE Format:     PE32+ (AMD64)
        IL Only:       Yes
        ...
        Status:        fixable

Apply the fix:

    pefix MyMod.dll --fix

Output:

    pefix MyMod.dll --fix
    
      Result:  Patched MyMod.dll
      Backup:  MyMod.dll.bak
      Before:  PE32+ AMD64 -> not compatible with all platforms
      After:   PE32 I386 -> compatible with all platforms
      Verify:  Re-inspection passed. Assembly manifest was validated.

Scan a directory:

    pefix ./mods

Output:

    pefix mods
    
      Summary: Scanned 1 candidate files. 1 require attention.
      Action:  Run pefix <path> --fix for entries marked fixable or cautioned.
      Counts:  compatible: 0  fixable: 1  cautioned: 0  unsafe: 0  corrupt: 0
    
      Group: portability
        - X64OnlyManaged.dll [fixable]

Add `--json` to any command for machine-readable output. Run `pefix --help` for the full option list and exit codes.

## Status legend

Every inspection produces one of five statuses:

| Status      | Meaning                                                                 |
|-------------|-------------------------------------------------------------------------|
| compatible  | Already portable, no action needed.                                     |
| fixable     | Header can be rewritten in place.                                       |
| cautioned   | Could be rewritten but requires explicit consent (`--force`).           |
| unsafe      | Refused — rewriting would not produce a working assembly.               |
| corrupt     | Not a valid PE file or malformed beyond inspection.                     |

## What it fixes

`pefix` rewrites the PE header and CorFlags of pure-IL `PE32+ AMD64` assemblies so they match the layout the .NET loader expects across platforms.

The rewrite is byte-level. It does not touch metadata, embedded resources, or strong-name tokens, and it does not perform `ildasm`/`ilasm` round-tripping. After the rewrite, `pefix` re-inspects the file and validates the assembly manifest before reporting success.

## What it refuses

`pefix` will not rewrite the header in any of the following cases:

| Refusal               | Why                                                            |
|-----------------------|----------------------------------------------------------------|
| mixed-mode (C++/CLI)  | Contains native code; needs `ijwhost.dll` or VC++ runtime.    |
| reference assembly    | Not a runtime artifact; cannot be executed.                   |
| satellite assembly    | Localized resource container, not a code module.              |
| multi-module assembly | Not supported on .NET Core or .NET 5+.                        |
| corrupt / non-PE      | Not a parseable PE file.                                      |
| strong-named          | Cautioned. `--force` accepts that the strong-name will break. |
| native dependencies   | Cautioned. P/Invoke targets must be available on the host.    |

Direction is one-way: `pefix` rewrites `PE32+ AMD64` to `PE32 I386`. Already compatible assemblies are left untouched.

## Safety

Before any in-place rewrite `pefix` copies the original to `MyMod.dll.bak`. The new bytes are written via an atomic temp-file rename, so a failed write never leaves a half-written assembly. If a `.bak` already exists, `pefix` refuses rather than overwrite.
