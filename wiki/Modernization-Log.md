# Modernization Log

Xamarinme sat untouched from January 2022. Xamarin has since been retired, so this is a port to
.NET MAUI rather than a framework bump: a new repository, new package IDs, and a per-library
question of whether the library should exist at all.

Each phase is one commit on `master`.

## Phase 0 — characterization tests

No production code changed. The point was to pin **current** behaviour, defects included, before
touching anything.

The Xamarin sources for the four libraries were imported verbatim into `legacy/`, together with the
vendored `Microsoft.Extensions.Primitives.Patch` that `WebHostPatch` cannot build without. Two
Xamarinme projects were **not** imported, because there is nothing there to port: an empty
`Class1` (`Microsoft.Extensions.FileProviders.Xamarin`) and a one-method file-system helper that
cannot work by construction (`Xamarin.Essentials.FileSystem.Extensions`). See
[Design Notes](Design-Notes.md#deletions-that-are-not-ports).

A `net10.0` xUnit v3 project was added with 62 tests, plus `PublicApi.approved.txt` as the API
baseline.

### What could be exercised

Better than Blazorme's starting position: the legacy libraries are `netstandard2.0`, which a
`net10.0` test project can reference, so three of the four could be characterized **behaviourally**
rather than through metadata alone.

| Library | Builds today? | Coverage in Phase 0 |
|---|---|---|
| Configuration | Yes, 0 warnings | Behavioural — 19 tests |
| Hosting | Yes, 0 warnings | Behavioural — 13 tests |
| WebHostPatch | Yes, 4 advisory warnings | Behavioural for the lifetime half; API baseline for `RunPatchedAsync`, which needs a live ASP.NET Core 2.2 `IWebHost` |
| Nfc | **No** | **None** — see below |

`legacy/Nfc` is not in the solution and has no coverage. `MSBuild.Sdk.Extras/3.0.23` needs desktop
`msbuild.exe` for its `MonoAndroid10.0`, `Xamarin.iOS10`, `uap10.0.19041` and `Xamarin.Mac20`
targets, and the `netstandard2.0` slice fails on its own terms. Its two defects are pinned as source
assertions, and become real tests in the phase that ports it. That gap is stated rather than papered
over.

### Nineteen defects pinned

Most were found by reading. **Four were found only by building or running**, which is the argument
for doing this phase at all:

| Found by | Defect |
|---|---|
| Building `legacy/Nfc` | The `netstandard2.0` slice does not compile: `NETSTANDARD2_0` is undefined under this toolchain, so `CrossNfc` compiles `new Nfc()` against a type that slice cannot see |
| Restoring the tests | `Xamarinme.WebHostPatch` pulls a **critical** Kestrel advisory and a moderate IIS one, inherited by all 30,366 downloads of 1.0.0 |
| Running the tests | The vendored Primitives fork really does shadow the real assembly — the test project itself ends up running on 5.9.0.0 |
| Running the tests | Newtonsoft renders JSON `true` as `"True"` and `null` as `""`, so the library's own README example is wrong about its own output |

The rest, by library:

- **Configuration** — no argument validation anywhere (null options and null assembly are both a
  `NullReferenceException` out of `Build()`); a null `Prefix` is worse still, silently loading
  nothing so the app runs on defaults it never chose; `AssemblyVersion` 1.0.0 against `Version`
  1.0.3.
- **Hosting** — `XamarinHost` claims `IHost` and throws `NotImplementedException` from `StartAsync`
  and `StopAsync`; `Dispose()` is empty, so the `ServiceProvider` it owns is never disposed; the
  environment lookup wraps `GetProperty`/`GetString` in a `try` with an **empty catch**, so a typo in
  the setting name is indistinguishable from not setting it; a JSON `null` there overwrites the
  "Production" default with `null`; `JsonDocument.Parse` sits one line *above* that `try`, so the one
  failure a user is most likely to cause is the one that escapes; `Build()` registers
  `IConfiguration` but not `IConfigurationRoot`, and can be called twice, registering the
  configuration twice; `XamarinHost` is exported from the internals namespace with no public
  constructor.
- **WebHostPatch** — `ShutdownPatched` is an extension method on `IWebHost` that never touches its
  own parameter, raising a private *static* event instead, so shutdown is process-global and calling
  it on a host that was never run silently does nothing; `Dispose` unsubscribes
  `Console.CancelKeyPress`, the very API the fork exists to avoid and one `WaitForStartAsync` never
  subscribes; `OnProcessExit` waits for the shutdown block with a timeout, logs that it timed out,
  and then waits again with no timeout at all.
- **Nfc** — the `netstandard2.0` slice above, and about 180 lines of dead code behind an `#if false`
  in `Pcsc.cs`, including a whole second `Pcsc()` constructor.

### The tests were proven, not assumed

Every Phase 0 test was checked by breaking what it guards: an invented type in the approved baseline
produced 1 failure, changing a fixture value produced 4, and uncommenting the `CancelKeyPress`
subscription produced exactly the 1 expected failure. The suite then ran **30 times** in a row clean.

### One consequence to carry forward

**The zero-warning bar cannot be enforced yet.** `legacy/WebHostPatch`'s 2.2.0 pins produce four
NuGet advisory warnings, and `-warnaserror` turns advisories into errors. There is therefore no CI
workflow yet; it arrives when `legacy/` goes. The warnings are deliberately left visible rather than
suppressed with `NoWarn`.

## Decisions taken before Phase 0

- **Fresh git history.** Mauime does not carry Xamarinme's 153 commits.
- **All four libraries are ported.** The recommendation was `Mauime.Nfc` alone — Hosting and
  Configuration are `MauiAppBuilder`, and WebHostPatch's two reasons to exist are both gone on
  .NET 10 — and the decision was to port all four anyway. `Mauime.WebHostPatch` will therefore be a
  Kestrel-in-MAUI convenience layer, not a fork of Microsoft code.
- **No deprecations.** The `Xamarinme.*` packages stay on nuget.org as they are.
- **Platform-agnostic tests only.** Unit tests for the shared slices plus the metadata API baseline;
  no device test harness.

## What is still open

- **Whether "no deprecations" survives the Kestrel advisory.** The decision was taken before the
  critical CVE in `Xamarinme.WebHostPatch`'s dependency tree was known.
- **`NdefLibrary` in the public API.** A 2017 `netstandard1.4`-only package, returned by
  `INfc.ReadNdefAsync()`. New package IDs mean it can be replaced; that costs a package decision.
- **Porting `Mauime.Nfc` needs new package references** — PCSC 7.0.1 and whatever replaces
  NdefLibrary — which will be asked for, not assumed.
