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

## Phase 1 — the MAUI skeleton

Four library projects, no library code: `Mauime.Nfc`, `Mauime.Configuration`, `Mauime.Hosting` and
`Mauime.WebHostPatch`, each multi-targeting `net10.0`, `net10.0-android`, `net10.0-ios`,
`net10.0-maccatalyst` and `net10.0-windows10.0.19041.0`. Twenty assemblies, all building clean.

The point of a skeleton phase is to settle the build questions while the answer to "did that work?"
is still unambiguous. Four were settled by building, not by reading:

- **`Microsoft.Maui.Core` is the right dependency**, not `Microsoft.Maui.Controls` and not
  `Microsoft.Maui.Essentials`. Essentials gives `Platform.CurrentActivity` but has no
  `MauiAppBuilder` and no `LifecycleEvents`; Core has all three. An NFC plugin has no business
  depending on Controls.
- **`Mauime.WebHostPatch` needs no packages at all.** `WebApplication.CreateSlimBuilder()` plus
  `UseKestrel` compiles clean against `net10.0-android` with a `Microsoft.AspNetCore.App` framework
  reference and nothing else. **This conclusion was wrong**, and Phase 6 explains why: the probe
  built a library, and the framework reference only fails when an *app* needs a runtime pack.
- **Android generates a public `Resource` class into every library**, even one with no Android
  resources, and it landed in all four packages' public surface. Turned off with
  `AndroidGenerateResourceDesigner=false`. Blazorme had to live with the equivalent Razor wart;
  this one has a knob.
- **iOS and Mac Catalyst library slices compile on Windows.** Only app builds and signing need a
  Mac.

`Directory.Build.props` and `Directory.Build.targets` were added at the root, with **empty shields
under `legacy/`** so the imported sources keep building exactly as they did. Those two shields are
the only files added under `legacy/`.

### The baseline machinery now reaches the platform slices

This was the claim Phase 0 made and could not test, since everything it covered was plain
netstandard2.0. Making it true needed two changes:

- one `MetadataLoadContext` **per assembly**, because an Android `System.Runtime` and an iOS
  `System.Runtime` cannot share a simple-name resolver;
- `Mauime.ReferencePaths.txt` beside every assembly, written from the `ReferencePath` item, because
  a library build leaves its dependencies nowhere near `bin/`.

Verified with a temporary type exposing `Android.Nfc.NfcAdapter`,
`CoreNFC.NFCNdefReaderSession` and `Windows.Devices.SmartCards.SmartCardReader`: every slice rendered
correctly, from a `net10.0` test project that can reference none of them. `MultiTargetingTests`
caught the divergence at the same time, which is exactly its job.

Also verified: a `net10.0` project referencing `Mauime.Nfc` resolves the `net10.0` slice byte for
byte, never the Android one. That is why the platform slices can only be covered through metadata.

74 tests, green in Debug and Release, 30 consecutive clean runs. The three new tests were each
proven by breaking what they guard.

## Phase 2 — Mauime.Nfc

The port of the one library with a clear reason to exist. `legacy/Nfc` was deleted in the same
commit; it had never built on this toolchain anyway.

Three decisions were taken before any code was written, and all three removed dependencies rather
than adding them, so **the phase added no NuGet packages at all**:

- **NDEF is implemented here.** `NdefMessage`, `NdefRecord` and `NdefTypeNameFormat` replace
  NdefLibrary 4.1.0 — a 2017 package with a netstandard1.4 asset that sat in the public API, since
  `ReadNdefAsync` returned its type. It was verified to restore, compile *and run* on .NET 10 before
  being dropped, so this was a choice rather than a forced move.
- **Android and iOS only.** Mac Catalyst and Windows would have meant the 678-line PC/SC layer and
  three `PCSC` packages, for an external USB reader rather than phone NFC. They share the
  platform-neutral implementation, which throws `PlatformNotSupportedException`.
- **DI instead of a static locator.** `builder.UseMauimeNfc()` registers `INfc`; `CrossNfc.Current`
  is gone.

### The API is smaller than what it replaced

Every implementation is `internal`, so all five slices expose one surface —
`INfc`, `MauimeNfcExtensions`, `NfcTagDetectedEventArgs` and the three NDEF types.
`CrossNfc`, the per-platform public `Nfc` classes, the manual `OnNewIntent` hook and the unused
`NfcTagStatus` are all gone. `MultiTargetingTests` stopped being vacuous the moment there was a
surface to compare.

`UseMauimeNfc` wires Android's `OnNewIntent` through `ConfigureLifecycleEvents`, so consumers no
longer hand-edit `MainActivity`.

### Defects fixed, and how far each is proved

The two Phase 0 pins were rewritten as `FIXED_`. Reading the implementations turned up four more,
none of them visible from the API:

| Fixed | How it is proved |
|---|---|
| `PendingIntent.GetActivity(..., 0)` threw on **Android 12 and later** — API 31 made FLAG_MUTABLE/FLAG_IMMUTABLE mandatory, so `EnableSessionAsync` crashed on every current phone | source pin |
| iOS threw from inside CoreNFC completion callbacks, on the session's dispatch queue, where the throw could not reach the awaiting caller — a failed read or write left the caller awaiting forever | source pin |
| `Dispose()` was empty on Android, so the `ActivityStateChanged` subscription kept the instance alive for the life of the process | source-visible |
| `catch { if (ndef.IsConnected) ... }` ran on a variable still `null` whenever the failure happened first, masking the real error with a `NullReferenceException`; also `throw ex;` in three places, resetting the stack trace | source-visible |
| The activity was captured in the constructor and went stale on recreation; `DispatchQueue.CurrentQueue` has been obsolete since iOS 6 | source-visible |

**None of the Android or iOS fixes has behavioural coverage**, and
`LIMITATION_The_Android_and_iOS_implementations_have_no_behavioural_coverage` asserts that fact so
it fails if it ever stops being true. A net10.0 test project resolves the net10.0 slice, which is
the one with no implementation. Closing that gap needs the device-test harness that was deliberately
not taken on.

What *is* covered by running it: the NDEF codec, the DI registration and the platform-neutral
implementation — 40 tests.

### The NDEF codec is pinned against NdefLibrary's own output

The port's real risk is a codec that disagrees with the old one, which would corrupt tags silently.
So the byte vectors in `NdefTests` were not invented: each was produced by running NdefLibrary 4.1.0
on .NET 10 and capturing what it emitted — a short text record, a two-record message, a record with
an identifier, a 300-byte payload using the four-byte length field, and the empty record. Mauime
produces the same bytes for all of them.

Malformed input is refused rather than mis-parsed, including a four-byte payload length claiming
more than the buffer holds, which would otherwise overflow to a negative length or try to allocate
4 GB.

### The forked Primitives split the test project in two

Adding a reference to `Mauime.Nfc` broke **thirty tests that had nothing to do with NFC**.
`Microsoft.Maui.Core` wants `Microsoft.Extensions.Primitives` 10.0.0; `legacy/WebHostPatch`'s
vendored fork is stamped 5.9.0.0 and occupies that filename in the output directory; the real one
therefore never arrives and everything needing it fails with `FileNotFoundException`.

That fork cannot share a process with modern `Microsoft.Extensions`, so the tests were split:
`Mauime.Tests` for the libraries and the API baseline, `Legacy.Tests` for the characterization of
`legacy/`. Making them coexist would have meant hiding the very defect the fork is pinned for.
`Legacy.Tests` dies with `legacy/`.

It also removed the last heuristic from the baseline machinery: `legacy/` lost its
`Directory.Build.targets` shield so its assemblies emit `Mauime.ReferencePaths.txt` too, and there
is now one code path for resolving any assembly instead of a manifest for Mauime and a bin-scan for
legacy.

120 tests, green in Debug and Release. Every new test was proven by breaking what it guards:
inverting the message-begin flag failed 10, restoring the immutable `PendingIntent` failed 1, and
letting the unsupported platform succeed silently failed 1.

## Phase 3 — the remaining three libraries

`Mauime.Configuration`, `Mauime.Hosting` and `Mauime.WebHostPatch`, which completes the port. One
package was added, `Microsoft.Extensions.Configuration.Json`; the other two libraries have no NuGet
dependencies at all.

Two facts settled the designs, both established by running code rather than reading about it — and
one of them only after the first attempt failed:

- **MAUI already registers an `IHostEnvironment`.** That was Phase 0's open question. It is
  `MauiHostEnvironment`, and it always reports `Production`.
- **Its `EnvironmentName` setter throws `NotImplementedException`.** The probe that found the
  registration checked `CanWrite` — true, because the interface declares a setter — and concluded
  the name could simply be assigned. It cannot. Only calling it says so, and what said so was a test.
  `Mauime.Hosting` wraps the platform environment instead, delegating everything but the name, so
  `ApplicationName` and `ContentRootPath` still come from the platform.

### Mauime.Configuration

`AddAppPackageJson` reads a `MauiAsset`; `AddEmbeddedResourceJson` reads an embedded resource, which
is the path Xamarinme offered and still works. Both layer `appsettings.{environment}.json` over the
base file. The options object that could be left half-filled is gone, and with it the three ways
Xamarinme turned a mistake into a `NullReferenceException` out of `Build()` — or, for a wrong
prefix, into an empty configuration and no error at all.

The parsing is `Microsoft.Extensions.Configuration.Json`'s. Xamarinme vendored a copy of Microsoft's
Newtonsoft-era parser and froze it; this references the maintained one. That is the opposite call
from `Mauime.Nfc`'s, and deliberately so: NdefLibrary was a dead 2017 package, while this is a live
first-party component, and reimplementing it would have been the same vendoring mistake in newer
clothes.

**A guess got corrected here too.** The obvious expectation was that Microsoft's parser renders a
JSON `true` as `"true"` where Newtonsoft rendered `"True"`. It does not — `JsonElement.ToString()`
also produces `"True"`, and the test asserting otherwise failed. The one real difference is a JSON
`null`: `""` before, absent now. The Xamarinme README's claim that
`Configuration["Logging:IncludeScopes"]` reads `false` was simply wrong, and was never about the
parser being old.

The app-package path has no coverage: MAUI's `FileSystem` on the net10.0 slice is the
reference-assembly stub and throws. Both paths funnel into the same layering code, so what is
untested is the four lines that open the asset.

### Mauime.WebHostPatch

No packages, no forks — a `Microsoft.AspNetCore.App` framework reference and nothing else.
`IMauimeWebHost` starts and stops a `WebApplication` on Kestrel and reports the address it bound to,
read from the server rather than the options so a `Port` of 0 reports the port the operating system
chose. Listening on every interface reports the machine's LAN address rather than `0.0.0.0`.

`NetworkAddress.GetLocalAddress()` replaces the demo's `NetworkHelper`, with its bug fixed: that
version picked the first qualifying interface and only then filtered its addresses, so a machine
whose first interface held nothing but a link-local address reported none at all.

**These tests start a real Kestrel and make real requests to it** — bind, serve, stop, rebind the
freed port, restart. The claim that .NET 10 needs no patch is worth more demonstrated than asserted.
Doing it turned up that `ListenLocalhost(0)` is refused by Kestrel, since it binds both loopback
addresses and they would get different ports; binding `127.0.0.1` explicitly is the fix.

### Documentation is now generated and enforced

`GenerateDocumentationFile` is on for the four `Mauime.*` libraries, so CS1591 requires every public
member to be documented and the packages will ship IntelliSense. Blazorme had to leave this off
because its surface had no XML docs at all; here they came with the code. Keyed on project name
rather than `IsPackable`, because `Directory.Build.props` is imported before the project body sets
it.

176 tests, green in Debug and Release. Every new test was proven by breaking what it guards:
reversing the configuration overlay order failed 1, ignoring the requested environment name failed
7, and reporting the configured port instead of the bound one failed 4.

## Phase 4 — legacy retired, CI added

`legacy/` and `Legacy.Tests` are deleted. Everything they characterized has been ported, and the
only thing they were still doing was holding the build hostage: `Xamarinme.WebHostPatch`'s ASP.NET
Core 2.2.0 pins carry a critical and a moderate advisory, and `-warnaserror` turns those into
errors.

The build is now **clean with `-warnaserror` in Debug and Release**, and CI enforces it.

The API baseline lost its three `Xamarinme.*` assemblies. That is the second deliberate baseline
change of the modernization, and the diff was reviewed to confirm it removed exactly those and
touched nothing under `Mauime.*`.

Two pieces of machinery got simpler with the fork gone:

- `TestAssemblies` no longer carries a legacy assembly list, a project-folder map or a separate
  framework-agnostic lookup. One path finds any assembly.
- `SimpleNameResolver` stays, but not for the reason it was written. It replaced
  `PathAssemblyResolver` because the forked `Microsoft.Extensions.Primitives` made the requested
  version unfindable; it earns its place now because a reference assembly and the runtime assembly
  it stands in for need not agree on version, and a metadata dump does not care either way.

### The workflow

One job on **`windows-latest`**, deliberately, not `ubuntu-latest`. `Directory.Build.props` drops
`net10.0-ios` and `net10.0-maccatalyst` on Linux and `net10.0-windows10.0.19041.0` everywhere but
Windows, so a Linux job would build two slices per library instead of five — and
`MultiTargetingTests` would pass while covering less than half of what ships. The iOS and Mac
Catalyst *library* slices compile on Windows; only building and signing an app for them needs a Mac,
so a macOS job becomes necessary when the demo app lands, not before.

`dotnet workload restore Mauime.slnx` installs what the solution's target frameworks need rather
than a hardcoded list that would drift from `Directory.Build.props`. That it accepts a `.slnx` at
all was checked rather than assumed.

**The workflow itself is unverified until it runs on a runner** — there is no way to execute
GitHub Actions from here, and saying otherwise would be exactly the kind of proxy this repository
keeps refusing. What *was* verified is its command sequence, run locally in order on a Windows
machine, which is the same platform the job uses.

Publishing is not set up yet: it needs the package versions and metadata that have not been written,
and a NuGet Trusted Publishing policy scoped to `Mauime.*` and bound to this repository.

## Phase 5 — package metadata

All four packages are ready to publish, and none of them has been. `dotnet pack` produces
`Mauime.Configuration`, `Mauime.Hosting`, `Mauime.Nfc` and `Mauime.WebHostPatch` at **26.9.8**, each
with five target-framework slices, XML documentation for every slice, a README and the icon.

Versions are date-based — `<Version>26.09.08</Version>`, which NuGet normalises to `26.9.8` —
matching Blazorme and Utilme. The tag spelling trap comes with it: a version check compares raw
csproj text, so the tag has to be `v26.09.08`.

Shared identity lives in `Directory.Build.props`; `Version`, `Product`, `Description`,
`PackageTags` and `PackageReleaseNotes` stay per project so the four can move independently.
`GeneratePackageOnBuild` is deliberately absent — Xamarinme set it, which is why building that
repository quietly produced `.nupkg` files at versions that were never published.

The release notes say what each package replaces and what changed, rather than "Creation." — which
is what `Xamarinme.WebHostPatch` 1.0.0 shipped with. `Mauime.Configuration`'s names the one
behavioural difference a Xamarinme user would trip over: a JSON `null` used to read as an empty
string and is now simply absent.

**Verified by unzipping the four packages**, not by reading the build log: id, version, icon,
readme, copyright, tags, dependency groups, all five slices, all five XML files, and the icon
compared byte for byte against `doc/me.png`. A plain build was confirmed to emit no package at all.

### The Xamarinme packages stay as they are

Decided rather than defaulted. `Blazorme.TestHost` looked like a precedent — a broken 1.0.0 rescued
by multi-targeting a new version back to net8.0 — but it does not transfer: that worked because it
was the **same package ID**, so existing consumers could resolve a newer, working version of the
package they already referenced. `Xamarinme.WebHostPatch` and `Mauime.WebHostPatch` are different
IDs, and NuGet has no redirect between IDs at any target framework. Targeting further back would not
help either, since the implementation is `WebApplication` on ASP.NET Core 10.

The only mechanism that crosses package IDs is deprecation with an alternate package, and the
decision is not to deprecate.

## Decisions taken before Phase 0

- **Fresh git history.** Mauime does not carry Xamarinme's 153 commits.
- **All four libraries are ported.** The recommendation was `Mauime.Nfc` alone — Hosting and
  Configuration are `MauiAppBuilder`, and WebHostPatch's two reasons to exist are both gone on
  .NET 10 — and the decision was to port all four anyway. `Mauime.WebHostPatch` will therefore be a
  Kestrel-in-MAUI convenience layer, not a fork of Microsoft code.
- **No deprecations.** The `Xamarinme.*` packages stay on nuget.org as they are.
- **Platform-agnostic tests only.** Unit tests for the shared slices plus the metadata API baseline;
  no device test harness.

## Phase 6 — the demo app, and a correction

One MAUI app replaces Xamarinme's three Xamarin.Forms solutions across 17 projects: four tabs, one
per library, on Syncfusion's `SfTabView` with ReactiveUI view models. It is in the solution and
deliberately out of CI.

### Mauime.WebHostPatch was rebuilt, because Phase 3 got it wrong

Phase 3 rebuilt the library on `WebApplication` and a `Microsoft.AspNetCore.App` framework
reference, and recorded that "a framework reference is all you need on .NET 10". **That is false on
every mobile platform**, and it made the package a trap: it would install into a MAUI app and break
the Android build with `NETSDK1082`.

The mistake was a verification one. The Phase 1 probe compiled a *library* targeting
`net10.0-android` against the ASP.NET Core reference assemblies, which works for any target
framework, and I concluded the capability was there. Deployment needs a **runtime pack**, and
`Microsoft.AspNetCore.App.Runtime.android-arm64`, `.ios-arm64` and `.maccatalyst-x64` do not exist —
they 404 on nuget.org. Nothing failed until an *app* was built, in this phase. A reference assembly
is not a deployment, and "it compiles" is not "it runs".

The fix is to do what Xamarinme did, because it was right: **ASP.NET Core as netstandard2.0
packages**, which are copied into the app like any assembly and need no runtime pack. That is the
whole reason the original worked on Xamarin, and the author's write-up says so directly — Xamarin
supported netstandard2.0, so the 2.2 line was the last one it could consume.

What improves on the original:

- **The 2.3.x servicing line instead of 2.2.0**, which is advisory-clean. 2.2.0 carries a critical
  Kestrel advisory and a moderate IIS one.
- **The Primitives fork is gone.** `Microsoft.Net.Http.Headers` 2.3.11 no longer references
  `InplaceStringBuilder`, so there is nothing left to shadow. Checked by grepping the assemblies —
  after the first attempt used `strings`, which is not installed on this machine and reported zero
  for everything including the 2.2.0 assemblies that provably contain it. The instrument check
  caught two false findings.
- **The `WebHostExtensions` fork is unnecessary.** `Console.CancelKeyPress` is still called in
  2.3.11, but from `RunAsync`; starting and stopping the host explicitly never enters that path, and
  blocking until shutdown is not what an app wants. The generic host's `ConsoleLifetime` is not
  involved either.

Verified by building: a `net10.0-android` MAUI app consuming the netstandard2.0 web library builds
clean with zero warnings and no runtime pack, and the demo app now builds for all four platforms.
Not verified: that it *runs* on a device. That needs hardware.

### ReactiveUI 24 is not the ReactiveUI anyone remembers

Three surprises, each found by a failure rather than by reading:

- **It has dropped System.Reactive** and reimplemented the operators on its own primitives. Adding
  Rx.NET alongside makes every `Select`, `Merge` and `Subscribe` ambiguous, so it is ReactiveUI's
  operators or Rx's, not both. `Signal.FromEventPattern` and `ObserveOn(ISequencer)` cover what the
  demo needs.
- **`ReactiveCommand<Unit, Unit>` is now `ReactiveCommand<RxVoid, RxVoid>`**, and `RxApp` is gone;
  the UI-thread sequencer is supplied explicitly, which is better anyway.
- **It must be initialized explicitly.** Without `RxAppBuilder.CreateReactiveUIBuilder()...BuildApp()`
  the first `WhenAnyValue` throws and the app fail-fasts at startup with no output. ReactiveUI's own
  error message suggests a call order that does not compile.

`ReactiveUI.Maui` was not used: it requires `Microsoft.Maui.Controls` 10.0.100, and the installed
workload's `$(MauiVersion)` is 10.0.20, so taking it would mean bumping MAUI across the repository
to satisfy a demo. The core package has no MAUI dependency.

### The demo really starts

Building an app is not running one. The Windows head was launched and watched: the first attempt
fail-fasted with exit code `0xC0000409` and no output, which was the missing ReactiveUI
initialization. A stock MAUI template app was run first to establish that the environment was not at
fault. After the fix it stays up, which exercises the configuration load, the DI graph, all four
view models, the Syncfusion XAML and the ReactiveUI initialization.

### CI nearly tested nothing

The first version of the test step passed `-warnaserror` to `dotnet test`. MTP forwards flags it
does not recognise to the test executable, which ran **zero tests**. It exits 5, so the job would
have failed rather than passing silently — but the lesson stands, and the comment in `ci.yml` now
names the specific mistake rather than the general rule.

CI builds the four libraries by name rather than the solution, because the demo is in the solution
and out of CI.

## Settled, and not to be reopened

- **`26.9.8` is the version.** Date-based, matching Blazorme and Utilme. Publishing it closes the
  door on ever shipping a `1.x`, and that was weighed and accepted.
- **`Mauime.*` targets .NET 10 only.** No reaching back to `net8.0` or `net9.0`. MAUI apps on older
  .NET cannot use these packages, which is a deliberate limit rather than an oversight — there are
  no consumers to strand, and `Mauime.WebHostPatch` would need ASP.NET Core 8/9 rather than 10.
- **The `Xamarinme.*` packages stay on nuget.org as they are**, and are not deprecated. See Phase 5
  for why `Blazorme.TestHost` is not a precedent for rescuing them.
- **No PC/SC.** Mac Catalyst and Windows throw `PlatformNotSupportedException` rather than pulling
  in three `PCSC` packages for an external USB reader.

## What is still open

- **The demo app.** One MAUI project to replace Xamarinme's three Xamarin.Forms
  solutions-in-a-solution across 17 projects. It is also the only thing that would exercise
  `Mauime.Nfc`'s Android and iOS implementations, which have no behavioural coverage at all, and it
  is what forces a macOS CI job.
- **Publishing.** A single `publish.yml` and a NuGet Trusted Publishing policy scoped to `Mauime.*`
  and bound to this repository. The repository does not exist on GitHub yet — there is no remote.
- **The first CI run.** `ci.yml` has never executed on a runner; only its command sequence has been
  verified, locally.
