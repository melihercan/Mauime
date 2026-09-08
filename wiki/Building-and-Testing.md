# Building and Testing

Requires the **.NET 10 SDK**.

```powershell
dotnet build Mauime.slnx
dotnet test
```

The solution is `Mauime.slnx`, the .NET 10 SDK's default XML solution format, not a classic `.sln`.
`TestAssemblies.FindRepositoryRoot` walks up looking for that exact file name, so renaming it breaks
the API baseline.

## The build is not warning-free yet, and cannot be

`legacy/WebHostPatch` pins `Microsoft.AspNetCore` 2.2.0 and `Microsoft.AspNetCore.Server.Kestrel`
2.2.0, and restoring those today reports:

- **NU1904**, critical — `Microsoft.AspNetCore.Server.Kestrel.Core` 2.2.0, [GHSA-5rrx-jjjq-q2r5](https://github.com/advisories/GHSA-5rrx-jjjq-q2r5)
- **NU1902**, moderate — `Microsoft.AspNetCore.Server.IIS` 2.2.0, [GHSA-prrf-397v-83xh](https://github.com/advisories/GHSA-prrf-397v-83xh)

Four warnings, deliberately left visible. **`-warnaserror` cannot be turned on until `legacy/` is
gone**, which is why there is no CI workflow yet. Do not silence them with `NoWarn`: they are the
strongest single argument about what to do with `Xamarinme.WebHostPatch` on nuget.org, whose 30,366
downloads all inherit both advisories.

Everything else builds clean.

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

`PublicApiSurfaceTests` reads the libraries' **compiled assemblies off disk** through
`MetadataLoadContext` rather than referencing them, so every library must have been built in the
same configuration first. `dotnet test` at solution level does this for you. The lookup is
configuration-aware, so a stale `Release` build cannot shadow a fresh `Debug` one by having a newer
timestamp.

### FluentAssertions is pinned to 7.x

Version 8 moved to a licence that requires payment for commercial use; 7.x is the last Apache-2.0
release. The pin is deliberate — do not let a tool bump it.

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

Adding it to the solution would make the whole build fail. It has no runtime coverage in Phase 0 for
the same reason.

Building the solution also drops `Xamarinme.Configuration.1.0.3.nupkg`,
`Xamarinme.Hosting.1.0.4.nupkg` and `Xamarinme.WebHostPatch.1.0.0.nupkg` into `legacy/*/bin`, because
those csprojs set `GeneratePackageOnBuild`. Nothing publishes them, and Phase 0 changed no production
file, so they were left alone. **Note the versions: two of them are ahead of what is actually on
nuget.org** (Configuration 1.0.2, Hosting 1.0.3). Never treat a csproj `<Version>` here as the live
version.

## The test suite

62 tests, all passing.

| File | Covers |
|---|---|
| `ConfigurationCharacterizationTests` | What `Xamarinme.Configuration` 1.0.3 actually does — key flattening, environment layering, and the Newtonsoft rendering quirks. |
| `HostingCharacterizationTests` | `XamarinHostBuilder`, the `XAMARIN_ENVIRONMENT` lookup, and the configuration object that is its own builder and root. |
| `WebHostPatchCharacterizationTests` | `ConsoleLifetimePatch`'s status logging and lifetime callbacks. |
| `PublicApiSurfaceTests` | The whole public surface against `PublicApi.approved.txt`. |
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

A test that has never been seen to fail is not a proven test. Every Phase 0 test was checked by
breaking the thing it guards:

| Mutation | Result |
|---|---|
| Add an invented type to `PublicApi.approved.txt` | 1 failure |
| Change `"Build"` in `TestAssets/Basic/appsettings.json` | 4 failures |
| Uncomment the `Console.CancelKeyPress` subscription in `legacy/WebHostPatch` | 1 failure, the expected one |

The suite was then run 30 times in a row against the restored tree, with no flakes.
