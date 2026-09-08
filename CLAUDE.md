# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this
repository.

## What this repository is

A port of [Xamarinme](https://github.com/melihercan/Xamarinme) to .NET MAUI. Xamarin is retired, so
this is a port, not a framework bump: new repository, fresh git history, **new package IDs**
(`Melihercan.Mauime.*`), and a per-library question of whether the library should exist at all.

**Nothing is published yet.** The work is phased, one commit per phase on `master`, and each phase
needs a go-ahead. Phases 0 (characterization), 1 (the MAUI skeleton), 2 (`Mauime.Nfc`), 3 (the other
three libraries) and 4 (retiring `legacy/`, adding CI) are done. **All four libraries are ported and
the build is clean under `-warnaserror`.** What remains is the demo app, package metadata, and
publishing.

## Repository layout

- `Mauime.Nfc/`, `Mauime.Configuration/`, `Mauime.Hosting/`, `Mauime.WebHostPatch/` — the libraries,
  each multi-targeting `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` and
  `net10.0-windows10.0.19041.0`.
- `Mauime.Tests/` — xUnit v3, one project for everything.
- `DemoApp/` — one MAUI app with a tab per library. **In the solution, deliberately out of CI**,
  which is why `ci.yml` names the four library projects rather than building the solution.
- `.github/workflows/ci.yml` — build and test, with `-warnaserror` on the build step only.
- `wiki/` — [Home](wiki/Home.md), [Building and Testing](wiki/Building-and-Testing.md),
  [Design Notes](wiki/Design-Notes.md), [Modernization Log](wiki/Modernization-Log.md).

## The shape of a library, and why

Settled in Phase 1 by building each option, not by reading documentation. Do not change these
without the same kind of evidence:

- **`Microsoft.Maui.Core`, not `Microsoft.Maui.Controls` or `Microsoft.Maui.Essentials`.** Core has
  `MauiAppBuilder`, the `LifecycleEvents` builders and `Microsoft.Maui.ApplicationModel`; Essentials
  has none of the first two. Since .NET 8 the `UseMaui*` properties do not add the package reference
  implicitly (MA002).
- **`Mauime.WebHostPatch` takes ASP.NET Core as netstandard2.0 packages** (the 2.3.x servicing
  line), never the `Microsoft.AspNetCore.App` framework reference. That framework has no runtime
  pack for android, ios or maccatalyst, so referencing it compiles the library and then fails every
  consuming mobile app with NETSDK1082.
- **`AndroidGenerateResourceDesigner=false`** for android frameworks. Android otherwise generates a
  public `Resource` class into every library, resources or not, and it lands in the public surface.
- The framework list lives once, as `$(MauimeTargetFrameworks)` in `Directory.Build.props`.

`Directory.Build.targets` writes `Mauime.ReferencePaths.txt` beside every assembly, which the API
baseline depends on.

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

## Zero warnings, enforced

The build is clean with **`-warnaserror`** in Debug and Release, and `ci.yml` builds that way. That
also makes a new NuGet advisory (NU1902/NU1904) a build failure, which is the point. Never reach for
`NoWarn`.

This bar was unreachable until Phase 4: `legacy/WebHostPatch` pinned ASP.NET Core 2.2.0, which
carries two advisories, and deleting `legacy/` is what made it possible.

## CI

One job on **`windows-latest`**, and it must stay there. `Directory.Build.props` drops
`net10.0-ios`/`net10.0-maccatalyst` on Linux and `net10.0-windows10.0.19041.0` off Windows, so a
Linux runner would build two slices per library instead of five and `MultiTargetingTests` would pass
while covering less than half of what ships. A macOS job becomes necessary when the demo app needs
iOS/Mac Catalyst *app* builds — library slices compile on Windows.

`dotnet workload restore Mauime.slnx` reads the solution rather than hardcoding a workload list that
would drift from `Directory.Build.props`.

**Publishing is not set up.** It needs package versions and metadata, plus a NuGet Trusted
Publishing policy scoped to `Melihercan.Mauime.*` and bound to this repository — one policy per workflow file,
so keep publishing in a single `publish.yml`.

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

**`Mauime.WebHostPatch`** uses the ASP.NET Core **2.3.x netstandard2.0 packages**, which is the only
route that reaches Android and iOS. **Do not "modernise" it to `WebApplication` plus a framework
reference** — that was tried, it compiles, and it breaks every consuming mobile app. Both of
Xamarinme's forks are gone anyway: `InplaceStringBuilder` is fixed upstream in 2.3.11, and the
`CancelKeyPress` call lives in `RunAsync`, which this never calls — it starts and stops the host
explicitly, which is what an app wants. Its tests start a real Kestrel on loopback; keep them that
way.

## XML documentation is generated and enforced

`GenerateDocumentationFile` is on for the four `Mauime.*` libraries, so **CS1591 requires every
public member to be documented**. Blazorme had to leave this off; here the docs came with the code,
so keep them coming. Keyed on project name rather than `IsPackable`, because
`Directory.Build.props` is imported before the project body sets it.

## Packaging

`dotnet pack Mauime.slnx -c Release`. **`GeneratePackageOnBuild` is deliberately absent** — a plain
build must not drop a `.nupkg` into `bin/`, which is what Xamarinme did at versions that were never
published.

Shared identity (authors, copyright, licence, icon, readme, URLs) lives in `Directory.Build.props`
under the same project-name condition as documentation generation. `Version`, `Product`,
`Description`, `PackageTags` and `PackageReleaseNotes` stay per project so the four can move
independently. `AssemblyVersion`/`FileVersion` derive from `Version` — never pin them, which is the
Xamarinme.Configuration defect.

Versions are date-based: `<Version>26.09.08</Version>`, which NuGet normalises to `26.9.8`. When
publishing lands, **tag with the csproj spelling** (`v26.09.08`), because the version check compares
raw csproj text.

Verify a packaging change by **unzipping the `.nupkg`**, not by reading the build log.

## The Xamarinme packages are staying as they are

Not deprecated, by decision. `Blazorme.TestHost` is not a precedent for rescuing them: that worked
because it kept the **same package ID**, so consumers could resolve a newer working version of what
they already referenced. `Melihercan.Mauime.*` are new IDs, and NuGet has no redirect between IDs at any target
framework. Do not spend effort trying to reach those consumers through multi-targeting.

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

**`SimpleNameResolver`, not `PathAssemblyResolver`**: matching on version is more precision than a
metadata dump needs, and more than the workloads reliably offer — a reference assembly and the
runtime assembly it stands in for need not agree. Each slice resolves only against its own reference
paths, so its world stays self-consistent.

## The defect-test convention

`KnownDefectTests` holds one test per defect, and the prefix carries the status:

- **`DEFECT_`** — pinned as broken, waiting to be fixed.
- **`FIXED_`** — was a defect pin, **rewritten in place** when fixed, with a comment naming the pin
  it replaced. Rewrite these, never delete them, so a fix shows in the diff.
- **`LIMITATION_`** — not a bug in this code.

A red test elsewhere means behaviour changed; acceptable only if intended, in which case update the
test in the same commit.

Some pins read source text under `Mauime.Nfc/Platforms/` instead of running code, because a net10.0
test project cannot reference the Android or iOS slices. That is a labelled second choice, not the
default.

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

These are settled. Do not reopen them without being asked.

- Fresh git history; Xamarinme's 153 commits are not carried over.
- **All four libraries are ported.** The recommendation was `Mauime.Nfc` alone; the decision was to
  port all four. `Mauime.WebHostPatch` therefore became a Kestrel-in-MAUI convenience layer, not a
  fork of Microsoft code — both of the original patches' causes are gone on .NET 10.
- **`26.9.8`, date-based**, matching Blazorme and Utilme. Publishing it rules out ever shipping a
  `1.x`; that was weighed and accepted.
- **.NET 10 only.** No `net8.0`/`net9.0` slices. MAUI apps on older .NET cannot use these packages,
  deliberately — there are no consumers to strand, and `Mauime.WebHostPatch` would need ASP.NET Core
  8/9 rather than 10.
- **No deprecations on nuget.org.** The `Xamarinme.*` packages stay exactly as they are.
- **No PC/SC**, so Mac Catalyst and Windows throw `PlatformNotSupportedException` for NFC.
- Platform-agnostic unit tests plus the metadata baseline. No device test harness.

## Working agreements

- Phase 0 is characterization first, pinning current behaviour including defects. Then one phase per
  go-ahead. Do not run ahead.
- When a fix makes a defect test fail, **rewrite** that test rather than delete it.
- Zero build warnings is the bar, enforced by CI with `-warnaserror`.
- Commit to `master` directly, no branches. **Commit and push only when asked.**
- **Never add or upgrade NuGet packages without asking.**
- Verify with the real thing, not a proxy. A test that has never been seen to fail is not a proven
  test — break what it guards and watch it go red.
