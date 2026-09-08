# Building and Testing

Requires the **.NET 10 SDK** and the `android`, `ios`, `maccatalyst` and `maui-windows` workloads.

```powershell
dotnet build Mauime.slnx
dotnet test
```

The solution is `Mauime.slnx`, the .NET 10 SDK's default XML solution format, not a classic `.sln`.
`TestAssemblies.FindRepositoryRoot` walks up looking for that exact file name, so renaming it breaks
the API baseline.

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
for them needs a Mac. CI on Linux will produce two slices per library, not five, which is why
`MultiTargetingTests` computes the expected set from the operating system rather than hardcoding it.

`net10.0` is deliberately in the list. It is the slice a non-platform project gets — verified by
referencing `Mauime.Nfc` from the `net10.0` test project, which resolves the `net10.0` assembly byte
for byte and never the Android one — and for `Mauime.Nfc` it is where "this platform has no
implementation" will live. `Xamarinme.Nfc` used `netstandard2.0` for the same role.

### `Microsoft.Maui.Core`, not `Controls` or `Essentials`

Each library references **`Microsoft.Maui.Core`** at `$(MauiVersion)`. That is the smallest thing
that works, established by building each option rather than by reading documentation:

| Reference | `MauiAppBuilder` | `ConfigureLifecycleEvents` | `Platform.CurrentActivity` |
|---|---|---|---|
| `Microsoft.Maui.Essentials` | no | no | yes |
| `Microsoft.Maui.Core` | **yes** | **yes** | **yes** |

Since .NET 8 the `UseMaui*` properties no longer add the package reference implicitly; MSBuild says
so with **MA002** if you leave it out.

`Mauime.WebHostPatch` additionally declares a `Microsoft.AspNetCore.App` framework reference and
**no packages at all**. `WebApplication.CreateSlimBuilder()` plus `UseKestrel` compiles clean against
`net10.0-android` with nothing else, which is the whole reason the Xamarin-era fork is not being
carried over.

### The Android resource designer is turned off

Android generates a public `Resource` class, under the project's root namespace, into every library
— including ones with no Android resources. It showed up in all four packages' public surface the
first time the slices were compared: the same shape of wart as Razor's generated `_Imports` classes
in Blazorme, except that this one has a knob. `AndroidGenerateResourceDesigner=false` is set for
android target frameworks in `Directory.Build.props`. A library that ever does carry Android
resources must turn it back on for itself.

## The build is not warning-free yet, and cannot be

`legacy/WebHostPatch` pins `Microsoft.AspNetCore` 2.2.0 and `Microsoft.AspNetCore.Server.Kestrel`
2.2.0, and restoring those today reports:

- **NU1904**, critical — `Microsoft.AspNetCore.Server.Kestrel.Core` 2.2.0, [GHSA-5rrx-jjjq-q2r5](https://github.com/advisories/GHSA-5rrx-jjjq-q2r5)
- **NU1902**, moderate — `Microsoft.AspNetCore.Server.IIS` 2.2.0, [GHSA-prrf-397v-83xh](https://github.com/advisories/GHSA-prrf-397v-83xh)

Four warnings, deliberately left visible. **`-warnaserror` cannot be turned on until `legacy/` is
gone**, which is why there is no CI workflow yet. Do not silence them with `NoWarn`: they are the
strongest single argument about what to do with `Xamarinme.WebHostPatch` on nuget.org, whose 30,366
downloads all inherit both advisories.

Everything else builds clean, in Debug and Release. **The four Mauime libraries build clean across
all five target frameworks**; keep it that way.

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
`MetadataLoadContext` rather than referencing them, so every library must have been built in the
same configuration first. `dotnet test` at solution level does this for you. The lookup is
configuration-aware, so a stale `Release` build cannot shadow a fresh `Debug` one by having a newer
timestamp.

### FluentAssertions is pinned to 7.x

Version 8 moved to a licence that requires payment for commercial use; 7.x is the last Apache-2.0
release. The pin is deliberate — do not let a tool bump it.

## `Directory.Build.props` and `.targets`, and the shields under `legacy/`

The repository-wide settings live at the root. `legacy/` has its **own empty**
`Directory.Build.props` and `Directory.Build.targets`, because discovery stops at the first file
found walking up: the imported Xamarin sources keep building exactly as they did. Enabling
`Nullable` across `legacy/`, for instance, would bury the four advisory warnings that matter under
dozens that do not. Those two shields are the only files added under `legacy/`; nothing imported has
been edited.

`Directory.Build.targets` writes **`Mauime.ReferencePaths.txt`** next to every built assembly,
holding the `ReferencePath` item — exactly what the compiler was handed. The API baseline needs it: a
library build does not copy its dependencies, so `Mono.Android`, `Microsoft.iOS` and the rest are
nowhere near `bin/`, and guessing at the workload's reference-assembly folders would be a proxy for
the truth. `MultiTargetingTests` asserts the file exists for every slice, because while the
libraries are still empty its absence would not fail anything else.

## What `legacy/` is

The Xamarin sources, imported verbatim so that Phase 0 could pin real behaviour and every later
deletion shows up as a diff. Four projects are in the solution:

| Project | In the solution? |
|---|---|
| `legacy/Configuration` | Yes — builds clean, referenced by the tests |
| `legacy/Hosting` | Yes — builds clean, referenced by the tests |
| `legacy/WebHostPatch` | Yes — builds, with the four advisory warnings above |
| `legacy/Microsoft.Extensions.Primitives.Patch` | Yes — WebHostPatch depends on it |
| `legacy/Nfc` | **No** |

`legacy/Nfc` is excluded because it cannot be built at all on this toolchain. It uses
`MSBuild.Sdk.Extras/3.0.23` with `MonoAndroid10.0; Xamarin.iOS10; uap10.0.19041; Xamarin.Mac20`,
which need desktop `msbuild.exe`, and its `netstandard2.0` slice fails on its own terms:

```
legacy/Nfc/CrossNfc.cs(30,24): error CS0246: The type or namespace name 'Nfc' could not be found
```

Adding it to the solution would make the whole build fail. It has no runtime coverage for the same
reason.

Building the solution also drops `Xamarinme.Configuration.1.0.3.nupkg`,
`Xamarinme.Hosting.1.0.4.nupkg` and `Xamarinme.WebHostPatch.1.0.0.nupkg` into `legacy/*/bin`, because
those csprojs set `GeneratePackageOnBuild`. Nothing publishes them, and no production file has been
changed, so they were left alone. **Note the versions: two of them are ahead of what is actually on
nuget.org** (Configuration 1.0.2, Hosting 1.0.3). Never treat a csproj version here as the live
version.

## The test suite

74 tests, all passing, in Debug and Release.

| File | Covers |
|---|---|
| `ConfigurationCharacterizationTests` | What `Xamarinme.Configuration` 1.0.3 actually does — key flattening, environment layering, and the Newtonsoft rendering quirks. |
| `HostingCharacterizationTests` | `XamarinHostBuilder`, the `XAMARIN_ENVIRONMENT` lookup, and the configuration object that is its own builder and root. |
| `WebHostPatchCharacterizationTests` | `ConsoleLifetimePatch`'s status logging and lifetime callbacks. |
| `PublicApiSurfaceTests` | The whole public surface against `PublicApi.approved.txt`. |
| `MultiTargetingTests` | That every library is built for every expected framework, that every platform slice exposes the same surface as the `net10.0` one, and that every slice records its reference paths. |
| `PublicApiDumper` / `TestAssemblies` / `SimpleNameResolver` | The machinery: metadata-only reflection, configuration-aware assembly lookup. |
| `KnownDefectTests` | One test per defect. |

`RunPatchedAsync` has no behavioural coverage: it needs a live ASP.NET Core 2.2 `IWebHost`. It is
covered by the API baseline only, and that is stated rather than papered over.

## The defect-test convention

`KnownDefectTests` holds one test per known defect, and the prefix carries the status:

- **`DEFECT_`** — pinned as broken, waiting to be fixed.
- **`FIXED_`** — was a defect pin, **rewritten in place** when fixed, with a comment naming the pin
  it replaced. Rewrite these, never delete them, so a fix shows up as a diff rather than as silence.
- **`LIMITATION_`** — not a bug in this code.

A red test that is not one of these means behaviour changed. That is only acceptable if the change
was intended, in which case update the test in the same commit and say so.

### Six pins read source text instead of running code

`DEFECT_ConsoleLifetimePatch_Dispose_touches_the_very_API_the_patch_exists_to_avoid`,
`DEFECT_ConsoleLifetimePatch_waits_forever_at_process_exit`, the two `WebHostPatch` packaging pins
and the two `Nfc` pins assert against files under `legacy/`, through the `LegacySource` helper.

Two of them — the vendored Primitives fork and the ASP.NET Core 2.2.0 pins — assert csproj
declarations, which is exactly where those defects live. (The Primitives one *also* asserts at
runtime, because the shadowing is real enough to affect the test project itself.) The other four
cannot be reached from a test at all:

- the `CancelKeyPress` throw is Mono-specific, so on Windows both the subscribe and the unsubscribe
  are harmless and no assertion could tell the difference;
- exercising `OnProcessExit` means letting a real process-exit handler block on an unbounded
  `WaitOne()`, which would hang the run rather than fail it;
- `legacy/Nfc` does not compile, so there is no assembly to load.

They are still real tests: uncommenting the `CancelKeyPress` subscription in
`ConsoleLifetimePatch.cs` turns the first one red, which was checked rather than assumed.

## Proving a test can fail

A test that has never been seen to fail is not a proven test. Every test added so far was checked by
breaking the thing it guards:

| Mutation | Result |
|---|---|
| Add an invented type to `PublicApi.approved.txt` | 1 failure |
| Change a value in `TestAssets/Basic/appsettings.json` | 4 failures |
| Uncomment the `Console.CancelKeyPress` subscription in `legacy/WebHostPatch` | 1 failure, the expected one |
| Add a platform-specific member to one slice of `Mauime.Nfc` | 2 failures — baseline and multi-targeting |
| Delete one target framework's build output | 1 failure |
| Delete one slice's `Mauime.ReferencePaths.txt` | 1 failure |

The suite is then run 30 times in a row against the restored tree, with no flakes.
