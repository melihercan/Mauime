# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this
repository.

## What this repository is

A port of [Xamarinme](https://github.com/melihercan/Xamarinme) to .NET MAUI. Xamarin is retired, so
this is a port, not a framework bump: new repository, fresh git history, **new package IDs**
(`Mauime.*`), and a per-library question of whether the library should exist at all.

**Nothing is published yet.** The work is phased, one commit per phase on `master`, and each phase
needs a go-ahead. Phases 0 (characterization), 1 (the MAUI skeleton), 2 (`Mauime.Nfc`) and 3 (the
other three libraries) are done. **All four libraries are ported.** What remains is the demo app,
package metadata, and CI plus publishing — and `-warnaserror` cannot be turned on until `legacy/` is
deleted.

## Repository layout

- `Mauime.Nfc/`, `Mauime.Configuration/`, `Mauime.Hosting/`, `Mauime.WebHostPatch/` — the libraries,
  each multi-targeting `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` and
  `net10.0-windows10.0.19041.0`.
- `legacy/` — the Xamarin sources, imported verbatim so Phase 0 could pin real behaviour and every
  later deletion shows up as a diff. Transformed away as the port proceeds.
- `Mauime.Tests/` — xUnit v3, the Mauime libraries and the API baseline.
- `Legacy.Tests/` — characterization of `legacy/`. **Separate on purpose**: `legacy/WebHostPatch`'s
  forked `Microsoft.Extensions.Primitives`, stamped 5.9.0.0, occupies that filename in whatever
  output directory it reaches, so anything wanting the real 10.0.0 gets `FileNotFoundException`.
  Referencing `Mauime.Nfc` from the old single project broke thirty unrelated tests exactly that
  way. Do not merge them back; this project dies with `legacy/`.
- `wiki/` — [Home](wiki/Home.md), [Building and Testing](wiki/Building-and-Testing.md),
  [Design Notes](wiki/Design-Notes.md), [Modernization Log](wiki/Modernization-Log.md).

## The shape of a library, and why

Settled in Phase 1 by building each option, not by reading documentation. Do not change these
without the same kind of evidence:

- **`Microsoft.Maui.Core`, not `Microsoft.Maui.Controls` or `Microsoft.Maui.Essentials`.** Core has
  `MauiAppBuilder`, the `LifecycleEvents` builders and `Microsoft.Maui.ApplicationModel`; Essentials
  has none of the first two. Since .NET 8 the `UseMaui*` properties do not add the package reference
  implicitly (MA002).
- **`Mauime.WebHostPatch` takes no packages**, only a `Microsoft.AspNetCore.App` framework
  reference. `WebApplication.CreateSlimBuilder()` plus `UseKestrel` compiles clean on
  `net10.0-android` with nothing else — which is the Xamarin-era fork's whole reason for existing,
  gone.
- **`AndroidGenerateResourceDesigner=false`** for android frameworks. Android otherwise generates a
  public `Resource` class into every library, resources or not, and it lands in the public surface.
- The framework list lives once, as `$(MauimeTargetFrameworks)` in `Directory.Build.props`.

`legacy/` has its **own empty** `Directory.Build.props` and `Directory.Build.targets`. Discovery
stops at the first file found walking up, so the imported sources keep building exactly as they did.
Those shields are the only files added under `legacy/`; nothing imported has been edited.

## Commands

```powershell
dotnet build Mauime.slnx
dotnet test
```

The solution is `Mauime.slnx`, the .NET 10 SDK default, **not** a classic `.sln`.
`TestAssemblies.FindRepositoryRoot` walks up looking for that exact name.

`dotnet test` runs in **Microsoft.Testing.Platform** mode, opted in via `global.json`. xUnit v3 test
projects are executables and the .NET 10 SDK refuses to run them through VSTest, so that file is
required. **Do not pass `--nologo`** or other VSTest-era flags: MTP forwards unrecognised arguments
to the test executable, which exits 5 with "Zero tests ran" — a failure that looks like broken tests
but is a bad command line. Target one project with `--project <path>`, not a bare path.

**Build the solution before running the tests.** `PublicApiSurfaceTests` and `MultiTargetingTests`
read compiled assemblies off disk through `MetadataLoadContext` rather than referencing them.

## The build is not warning-free yet, and that is deliberate

`legacy/WebHostPatch` pins ASP.NET Core 2.2.0, which reports **NU1904 (critical)** for
`Microsoft.AspNetCore.Server.Kestrel.Core` and **NU1902 (moderate)** for
`Microsoft.AspNetCore.Server.IIS`. Four warnings.

**`-warnaserror` cannot be turned on until `legacy/` is gone**, which is why there is no CI workflow
yet. Do not silence these with `NoWarn` — `Xamarinme.WebHostPatch` 1.0.0 is on nuget.org with 30,366
downloads and every consumer inherits both advisories, which is a live question, not noise.
Everything else builds clean; keep it that way.

## legacy/Nfc is not in the solution

It cannot be built on this toolchain: `MSBuild.Sdk.Extras/3.0.23` needs desktop `msbuild.exe` for
`MonoAndroid10.0; Xamarin.iOS10; uap10.0.19041; Xamarin.Mac20`, and the `netstandard2.0` slice fails
on its own terms (`CrossNfc.cs(30,24): error CS0246`). Adding it to the solution breaks the build. It
therefore has **no runtime coverage** in Phase 0; its two defects are pinned as source assertions.

## Mauime.Nfc

Android and iOS are implemented; `net10.0`, Mac Catalyst and Windows share `UnsupportedNfc`, which
throws `PlatformNotSupportedException`. **Every implementation is `internal`** — that is what lets
`MultiTargetingTests` hold all five slices to one public surface, so keep it that way.

Platform sources are selected by **explicit `Compile Include` conditions** in the csproj. The
`Platforms/` folder convention is an app thing: in a class library those files compile into every
target framework, which was checked rather than assumed.

The NDEF codec is ours, replacing NdefLibrary 4.1.0 — the package has **no third-party
dependencies**. `NdefTests`' byte vectors were captured from NdefLibrary's own output, so they pin
Mauime against what Xamarinme actually wrote to tags; do not regenerate them from the code under
test.

**The Android and iOS implementations have no behavioural coverage** and cannot get any from a
net10.0 test project, which resolves the unsupported slice.
`LIMITATION_The_Android_and_iOS_implementations_have_no_behavioural_coverage` asserts that, so it
fails if the situation changes. Their fixes are held by source pins, which is a second choice and
labelled as one.

## The other three libraries

**`Mauime.Configuration`** wraps `Microsoft.Extensions.Configuration.Json`, which MAUI does not
reference. Referencing rather than vendoring is the deliberate opposite of `Mauime.Nfc`'s call:
NdefLibrary was dead, this is live and first-party. Only `AddEmbeddedResourceJson` is testable —
`AddAppPackageJson` needs MAUI's `FileSystem`, which throws on the net10.0 slice.

**`Mauime.Hosting`** exists only because `MauiHostEnvironment` always reports `Production`. It
**wraps** the platform environment; do not go back to assigning `EnvironmentName`, whose setter
throws `NotImplementedException` even though the interface declares it and the type reports
`CanWrite`.

**`Mauime.WebHostPatch`** has no NuGet dependencies and no forks — a `Microsoft.AspNetCore.App`
framework reference is all a web host needs on .NET 10. Its tests start a real Kestrel on loopback;
keep them that way, because the claim that no patch is needed is the whole point of the package.
Note `ListenLocalhost(0)` is refused by Kestrel — bind `IPAddress.Loopback` explicitly for a dynamic
port.

## XML documentation is generated and enforced

`GenerateDocumentationFile` is on for the four `Mauime.*` libraries, so **CS1591 requires every
public member to be documented**. Blazorme had to leave this off; here the docs came with the code,
so keep them coming. Keyed on project name rather than `IsPackable`, because
`Directory.Build.props` is imported before the project body sets it.

## The API baseline

`PublicApiSurfaceTests` enforces `Mauime.Tests/PublicApi.approved.txt`, which captures every public
type, member signature, generic arity, enum numeric value, `const` literal and default parameter
value.

The package IDs are new, so there are **no consumers and no additive-only guarantee** — this is not
Blazorme. The API is free to be fixed. The baseline exists so a change to the surface is a *decision*
rather than an accident. When a change is intentional, review the diff and copy
`PublicApi.received.txt` from the test output directory over `PublicApi.approved.txt` in the same
commit. **Never weaken the assertion.**

It reads metadata only, through `MetadataLoadContext`, because the libraries target MAUI platform
frameworks that a `net10.0` test project cannot reference. That is not theoretical: referencing
`Mauime.Nfc` from `Mauime.Tests` resolves the `net10.0` slice byte for byte, never the Android one.

**Each Mauime library is baselined from its `net10.0` slice only**, and `MultiTargetingTests` holds
the other four slices to it — same public surface, every framework. A platform-specific member added
on purpose means rewriting that test to name the exception, in the commit that adds it. Do not
weaken it into a subset check.

Two things make reading a platform slice work, both added in Phase 1:

- **One `MetadataLoadContext` per assembly.** An Android `System.Runtime` and an iOS `System.Runtime`
  cannot share a simple-name resolver; a shared context silently resolves types out of whichever
  pack was enumerated first.
- **`Mauime.ReferencePaths.txt`**, written beside every assembly by `Directory.Build.targets` from
  the `ReferencePath` item. A library build does not copy its dependencies, so `Mono.Android`,
  `Microsoft.iOS` and the rest are nowhere near `bin/`. `MultiTargetingTests` asserts the file
  exists for every slice, because while the libraries are empty nothing else would notice its loss.

**`SimpleNameResolver`, not `PathAssemblyResolver`**: `legacy/WebHostPatch` drops a fork of
`Microsoft.Extensions.Primitives` stamped 5.9.0.0 into every referencing output directory, shadowing
the real 5.0.0, so the exact version the metadata asks for is nowhere on disk. Matching on simple
name alone is the fix. For a legacy assembly the runtime directory is probed first; a Mauime slice
resolves only against its own reference paths, so its world stays self-consistent.

## The defect-test convention

`KnownDefectTests` holds one test per defect, and the prefix carries the status:

- **`DEFECT_`** — pinned as broken, waiting to be fixed.
- **`FIXED_`** — was a defect pin, **rewritten in place** when fixed, with a comment naming the pin
  it replaced. Rewrite these, never delete them, so a fix shows in the diff.
- **`LIMITATION_`** — not a bug in this code.

A red test elsewhere means behaviour changed; acceptable only if intended, in which case update the
test in the same commit.

Six pins assert against files under `legacy/` through the `LegacySource` helper instead of running
code. Two are about csproj declarations, which is where those defects live. The other four cannot be
reached: the `CancelKeyPress` throw is Mono-specific, exercising `OnProcessExit` would hang the run
on an unbounded `WaitOne()`, and `legacy/Nfc` does not compile. They are still real tests —
uncommenting the `CancelKeyPress` subscription turns one red, which was checked.

## Test stack

xUnit v3 4.0.0, NSubstitute 6.2.0, `System.Reflection.MetadataLoadContext` 10.0.0, and
**FluentAssertions pinned to 7.x** — 8.x requires payment for commercial use. Do not let a tool bump
it.

`TestAssets/**/*.json` are embedded resources, because `Xamarinme.Configuration` reads
`{Prefix}.appsettings.json` out of an assembly's manifest resources and there is no other way to
reach it.

## Verify by building, not by reading

Phase 1's four findings all came from a build: that Essentials lacks `MauiAppBuilder`, that Kestrel
needs no packages on android, that Android injects a public `Resource` class, and that iOS library
slices compile on Windows. The baseline's ability to read platform types was likewise proved with a
temporary type exposing `Android.Nfc.NfcAdapter`, `CoreNFC.NFCNdefReaderSession` and
`Windows.Devices.SmartCards.SmartCardReader`, then deleted. Keep doing that.

## Decisions already taken

- Fresh git history; Xamarinme's 153 commits are not carried over.
- **All four libraries are ported.** The recommendation was `Mauime.Nfc` alone; the decision was to
  port all four. `Mauime.WebHostPatch` therefore becomes a Kestrel-in-MAUI convenience layer, not a
  fork of Microsoft code — both of the original patches' causes are gone on .NET 10.
- No deprecations on nuget.org. (Taken before the Kestrel advisory was known; still open.)
- Platform-agnostic unit tests plus the metadata baseline. No device test harness.

## Working agreements

- Phase 0 is characterization first, pinning current behaviour including defects. Then one phase per
  go-ahead. Do not run ahead.
- When a fix makes a defect test fail, **rewrite** that test rather than delete it.
- Zero build warnings is the bar, enforced by CI with `-warnaserror` once `legacy/` is gone.
- Commit to `master` directly, no branches. **Commit and push only when asked.**
- **Never add or upgrade NuGet packages without asking.**
- Verify with the real thing, not a proxy. A test that has never been seen to fail is not a proven
  test — break what it guards and watch it go red.
