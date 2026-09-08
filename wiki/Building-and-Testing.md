# Building and Testing

Requires the **.NET 10 SDK** and the `android`, `ios`, `maccatalyst` and `maui-windows` workloads.
`dotnet workload restore Mauime.slnx` installs what the solution needs.

```powershell
dotnet build Mauime.slnx
dotnet test
```

The solution is `Mauime.slnx`, the .NET 10 SDK's default XML solution format, not a classic `.sln`.
`TestAssemblies.FindRepositoryRoot` walks up looking for that exact file name, so renaming it breaks
the API baseline.

## Zero warnings, enforced

The build is clean with **`-warnaserror`** in Debug and Release, and CI builds that way:

```powershell
dotnet build Mauime.slnx --configuration Release -warnaserror
```

That also makes a new NuGet advisory (NU1902/NU1904) a build failure, which is the point. Never
reach for `NoWarn`.

The bar was unreachable until Phase 4. `legacy/WebHostPatch` pinned ASP.NET Core 2.2.0, whose
`Microsoft.AspNetCore.Server.Kestrel.Core` carries a critical advisory and whose
`Microsoft.AspNetCore.Server.IIS` carries a moderate one; deleting `legacy/` is what made it
possible.

**XML documentation is generated for the four `Mauime.*` libraries**, so CS1591 requires every
public member to be documented and the packages ship IntelliSense. It is keyed on project name
rather than `IsPackable`, because `Directory.Build.props` is imported before the project body sets
it.

## Target frameworks

The four libraries multi-target, through `$(MauimeTargetFrameworks)` in `Directory.Build.props`
rather than four copies of the same list:

| Framework | Built on |
|---|---|
| `net10.0` | everywhere |
| `net10.0-android` | everywhere |
| `net10.0-ios`, `net10.0-maccatalyst` | Windows and macOS |
| `net10.0-windows10.0.19041.0` | Windows |

The iOS and Mac Catalyst **library** slices compile on Windows; only building and signing an *app*
for them needs a Mac. `MultiTargetingTests` computes the expected set from the operating system for
that reason.

`net10.0` is deliberately in the list. It is the slice a non-platform project gets — verified by
referencing `Mauime.Nfc` from the test project, which resolves the `net10.0` assembly byte for byte
and never the Android one — and for `Mauime.Nfc` it is where "this platform has no implementation"
lives.

### `Microsoft.Maui.Core`, not `Controls` or `Essentials`

Each library references **`Microsoft.Maui.Core`** at `$(MauiVersion)`. That is the smallest thing
that works, established by building each option rather than by reading documentation:

| Reference | `MauiAppBuilder` | `ConfigureLifecycleEvents` | `Platform.CurrentActivity` |
|---|---|---|---|
| `Microsoft.Maui.Essentials` | no | no | yes |
| `Microsoft.Maui.Core` | **yes** | **yes** | **yes** |

Since .NET 8 the `UseMaui*` properties no longer add the package reference implicitly; MSBuild says
so with **MA002** if you leave it out.

`Mauime.Configuration` adds `Microsoft.Extensions.Configuration.Json` 10.0.0, pinned to match the
`Microsoft.Extensions.Configuration` that `Microsoft.Maui.Core` already resolves.

`Mauime.WebHostPatch` takes ASP.NET Core as **netstandard2.0 packages** — `Microsoft.AspNetCore.Server.Kestrel`
2.3.12 and `Microsoft.AspNetCore.Hosting` 2.3.11 — and **never the `Microsoft.AspNetCore.App`
framework reference**. There is no runtime pack for that framework on android, ios or maccatalyst,
so a framework reference compiles the library and then fails the consuming app with `NETSDK1082`.
A netstandard2.0 package is just an assembly and is copied in like any other, which is exactly why
`Xamarinme.WebHostPatch` worked on Xamarin.

### The Android resource designer is turned off

Android generates a public `Resource` class, under the project's root namespace, into every library
— including ones with no Android resources. It showed up in all four packages' public surface the
first time the slices were compared. `AndroidGenerateResourceDesigner=false` is set for android
target frameworks in `Directory.Build.props`. A library that ever does carry Android resources must
turn it back on for itself.

## `dotnet test` runs on Microsoft.Testing.Platform

`Mauime.Tests` uses **xUnit v3**, whose test projects are executables. The .NET 10 SDK refuses to run
those through the legacy VSTest path, so the repository opts into MTP mode via `global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Deleting that file breaks the suite with *"Testing with VSTest target is no longer supported"*.

- **Do not pass `--nologo`** or other VSTest-era flags. MTP forwards unrecognised arguments to the
  test executable, which rejects them with exit code 5 and *"Zero tests ran"* — a failure that looks
  like broken tests but is really a bad command line.
- Target one project with **`--project <path>`**, not a bare path argument.

### Build the solution before running the tests

`PublicApiSurfaceTests` and `MultiTargetingTests` read **compiled assemblies off disk** through
`MetadataLoadContext` rather than referencing them, so every slice must have been built in the same
configuration first. `dotnet test` at solution level does this for you. The lookup is
configuration-aware, so a stale `Release` build cannot shadow a fresh `Debug` one by having a newer
timestamp.

### FluentAssertions is pinned to 7.x

Version 8 moved to a licence that requires payment for commercial use; 7.x is the last Apache-2.0
release. The pin is deliberate — do not let a tool bump it.

## `Directory.Build.props` and `.targets`

The repository-wide settings live at the root: nullable and implicit usings, the target-framework
list, the platform minimums, the Android designer switch and documentation generation.

`Directory.Build.targets` writes **`Mauime.ReferencePaths.txt`** next to every built assembly,
holding the `ReferencePath` item — exactly what the compiler was handed. The API baseline needs it: a
library build does not copy its dependencies, so `Mono.Android`, `Microsoft.iOS` and the rest are
nowhere near `bin/`, and guessing at the workload's reference-assembly folders would be a proxy for
the truth. `MultiTargetingTests` asserts the file exists for every slice.

## The test suite

117 tests in one project, all passing, in Debug and Release.

| File | Covers |
|---|---|
| `NdefTests` | The NDEF codec, against byte vectors captured from NdefLibrary 4.1.0's own output. |
| `NfcRegistrationTests` | `UseMauimeNfc`, and the platform-neutral implementation's refusals. |
| `ConfigurationTests` | `Mauime.Configuration`'s embedded-resource path: key flattening, the environment overlay, missing-file handling, argument validation. |
| `HostingTests` | That MAUI's own host environment always says `Production`, and that `Mauime.Hosting` changes it by wrapping rather than assigning. |
| `WebHostTests` | `IMauimeWebHost`, by starting a real Kestrel on loopback and making real requests to it. |
| `PublicApiSurfaceTests` | The whole public surface against `PublicApi.approved.txt`. |
| `MultiTargetingTests` | That every library is built for every expected framework, that every platform slice exposes the same surface as the `net10.0` one, and that every slice records its reference paths. |
| `KnownDefectTests` | The Xamarinme defects the port fixed, rewritten from the pins that recorded them. |
| `PublicApiDumper` / `TestAssemblies` / `SimpleNameResolver` | The machinery: metadata-only reflection, configuration-aware assembly lookup. |

`Legacy.Tests` is gone with `legacy/`. It characterized the Xamarinme code before the port, and it
had to be a separate project because `legacy/WebHostPatch`'s forked
`Microsoft.Extensions.Primitives` — stamped 5.9.0.0 — occupied that assembly's filename in any output
directory it reached, so anything wanting the real 10.0.0 failed with `FileNotFoundException`. Its
history is the record of what the old code did.

### What is not covered, and why

Three gaps, all because a `net10.0` test project resolves the `net10.0` slice:

- **`Mauime.Nfc`'s Android and iOS implementations.** Held by the API baseline,
  `MultiTargetingTests` and source pins.
  `LIMITATION_The_Android_and_iOS_implementations_have_no_behavioural_coverage` asserts the gap, so
  it fails if it ever closes.
- **`Mauime.Configuration.AddAppPackageJson`.** MAUI's `FileSystem` on that slice is the
  reference-assembly stub and throws. Both it and the embedded-resource path funnel into the same
  layering code, so what is uncovered is the four lines that open the asset.
- **The Android `OnNewIntent` wiring**, for the same reason.

Closing any of these needs a device-test harness, which was deliberately not taken on.

## The defect-test convention

`KnownDefectTests` holds one test per defect, and the prefix carries the status:

- **`DEFECT_`** — pinned as broken, waiting to be fixed.
- **`FIXED_`** — was a defect pin, **rewritten in place** when fixed, with a comment naming the pin
  it replaced. Rewrite these, never delete them, so a fix shows up as a diff rather than as silence.
- **`LIMITATION_`** — not a bug in this code.

A red test that is not one of these means behaviour changed. That is only acceptable if the change
was intended, in which case update the test in the same commit and say so.

Some pins read source text under `Mauime.Nfc/Platforms/` instead of running code. That is a
deliberate second choice, used only where the behaviour cannot be reached from a net10.0 test
project at all.

## Proving a test can fail

A test that has never been seen to fail is not a proven test. Every test in the repository was
checked by breaking the thing it guards:

| Mutation | Result |
|---|---|
| Add an invented type to `PublicApi.approved.txt` | 1 failure |
| Add a platform-specific member to one slice of `Mauime.Nfc` | 2 failures — baseline and multi-targeting |
| Delete one target framework's build output | 1 failure |
| Delete one slice's `Mauime.ReferencePaths.txt` | 1 failure |
| Swap message-begin for message-end in the NDEF serializer | 10 failures |
| Ask for an immutable `PendingIntent` again | 1 failure |
| Let the unsupported platform succeed silently | 1 failure |
| Layer the configuration overlay before the base file | 1 failure |
| Ignore the requested environment name | 7 failures |
| Report the configured port instead of the bound one | 4 failures |

The suite is then run 30 times in a row against the restored tree, with no flakes.

## Packaging

```powershell
dotnet pack Mauime.slnx -c Release
```

`GeneratePackageOnBuild` is deliberately **absent**, so a plain build drops nothing into `bin/`.
Xamarinme's projects set it, which is why building that repository quietly produced
`Xamarinme.Configuration.1.0.3.nupkg` and friends — versions that were never published and did not
match what was on nuget.org.

Shared identity — authors, copyright, licence, icon, readme, URLs — lives in
`Directory.Build.props`, under the same project-name condition as documentation generation. What
differs per package — `Version`, `Product`, `Description`, `PackageTags`, `PackageReleaseNotes` —
stays in the csproj, so the four can be versioned and described independently.

`AssemblyVersion` and `FileVersion` are left to derive from `Version`. Xamarinme.Configuration
pinned `AssemblyVersion` at 1.0.0 while shipping 1.0.3, so every 1.0.x assembly presented the same
identity to the binder.

### Versions are date-based

The projects declare `<Version>26.09.08</Version>` and **NuGet normalises that to `26.9.8`**, which
is what the file is called. It matches Blazorme and Utilme.

When publishing lands, **tag with the csproj spelling** — `v26.09.08`, not `v26.9.7`-style
normalised text — because the version check compares against the raw csproj string.

### What is in a package

Each one carries five slices, each with its XML documentation, plus the README and the icon:

```
lib/net10.0/                        lib/net10.0-maccatalyst26.0/
lib/net10.0-android36.0/            lib/net10.0-windows10.0.19041/
lib/net10.0-ios26.0/
me.png   README.md
```

Note the **platform versions in those folder names**. They come from the SDK's defaults, and NuGet
requires a consumer's platform version to be at least the package's, so a project pinned to
`net10.0-android35.0` would not resolve the Android asset. Consumers on the plain `net10.0-android`
of the .NET 10 SDK are fine.

Dependencies are what they should be: `Microsoft.Maui.Core` everywhere, plus
`Microsoft.Extensions.Configuration.Json` for `Mauime.Configuration` and the ASP.NET Core 2.3.x
packages for `Mauime.WebHostPatch`.

**This was checked by unzipping the four `.nupkg` files**, not by reading the build log — the id,
version, icon, readme, copyright, tags, dependency groups, every slice, every XML file, and the icon
compared byte for byte against `doc/me.png`.

## The demo app

`DemoApp/` is one MAUI app with a tab per library — Syncfusion's `SfTabView`, ReactiveUI view
models, and `MauiProgram.cs` as the point of the whole thing: every library's setup in one short
file.

```powershell
dotnet build DemoApp/DemoApp.csproj -f net10.0-windows10.0.19041.0
```

It is **in the solution and out of CI**, which is why `ci.yml` names the four library projects
instead of building `Mauime.slnx`. It also sets `IsPackable=false`: an app is not a package, and
without it `dotnet pack Mauime.slnx` fails with NU5026 trying to pack the demo.

Two things about it are worth knowing before touching it:

- **ReactiveUI 24 must be initialized explicitly.** `RxAppBuilder.CreateReactiveUIBuilder()`
  → `.WithPlatformServices()` → `.WithCoreServices()` → `.BuildApp()`, before anything touches
  `WhenAnyValue`. Without it the app fail-fasts at startup with no output. Note the order:
  ReactiveUI's own error message suggests core-then-platform, which does not compile, because
  `WithCoreServices` returns Splat's `IAppBuilder`.
- **It uses `ReactiveUI`, not `ReactiveUI.Maui`.** The MAUI package wants
  `Microsoft.Maui.Controls` 10.0.100 and the installed workload's `$(MauiVersion)` is 10.0.20, so
  taking it would drag MAUI up across the repository for a demo. It also uses ReactiveUI's own
  operators rather than Rx.NET: version 24 dropped `System.Reactive` and reimplemented them, and the
  two sets collide on every `Select`, `Merge` and `Subscribe`.

Building it is not running it. The Windows head has been launched and watched to start cleanly; the
Android, iOS and Mac Catalyst heads are compile-verified only.

## CI

`.github/workflows/ci.yml` builds and tests on every push and pull request to `master`, and can be
run by hand.

It runs on **`windows-latest`**, and should stay there. `Directory.Build.props` drops
`net10.0-ios`/`net10.0-maccatalyst` on Linux and `net10.0-windows10.0.19041.0` off Windows, so a
Linux runner would build two slices per library instead of five — and `MultiTargetingTests` would
pass while covering less than half of what ships. A macOS job becomes necessary when the demo app
needs iOS or Mac Catalyst *app* builds; library slices compile here.

The build step passes `-warnaserror`. The test step runs after it deliberately, because the API
baseline reads assemblies the build produces.

**The workflow has not run on a runner yet.** There is no way to execute GitHub Actions from a
development machine, so what has been verified is its command sequence, run locally in order on
Windows — `dotnet workload restore Mauime.slnx`, `dotnet restore`, `dotnet build … -warnaserror`,
`dotnet test` — which passed clean. Treat the first CI run as the real test of the file.

Publishing is not set up. It needs the package versions and metadata that have not been written, and
a NuGet Trusted Publishing policy scoped to `Melihercan.Mauime.*` and bound to this repository. Keep publishing
in a single `publish.yml`: a policy binds to one workflow file.
