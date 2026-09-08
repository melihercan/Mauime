# Design Notes

Why the repository looks the way it does, and the invariants any change must preserve.

## The governing constraint is the opposite of Blazorme's

The `Mauime.*` package IDs are **new**. Nothing on nuget.org resolves them, so there are no
consumers and no additive-only guarantee. The API is free to be fixed properly — `CrossNfc.Current`
can become DI registration, a hand-wired `MainActivity.OnNewIntent` can become
`ConfigureLifecycleEvents`, and a dead 2017 dependency can come out of the public surface.

The API baseline still exists, and matters more here than it did in Blazorme, for a different
reason: **so that changing the surface is a decision rather than an accident.** Every change to it is
a diff to review in the commit that causes it.

## The API baseline

`PublicApiSurfaceTests` renders every public type, member signature, generic arity, enum numeric
value, `const` literal and default parameter value into `Mauime.Tests/PublicApi.approved.txt`.

It reads **metadata only**, through `MetadataLoadContext` over the built assemblies, rather than
referencing the projects. In Blazorme that was because two libraries targeted `net5.0`. Here it is
forward-looking: the ported libraries will target **MAUI platform frameworks** — `net10.0-android`,
`net10.0-ios`, `net10.0-maccatalyst`, `net10.0-windows10.0.19041.0` — and a plain `net10.0` test
project cannot reference any of them. Establishing the machinery in Phase 0, while the legacy
libraries are still plain `netstandard2.0` and easy, means the baseline survives the port.

When a change is intentional, review the diff and copy `PublicApi.received.txt` from the test output
directory over `PublicApi.approved.txt`, in the same commit. Never weaken the assertion.

The baseline renders each Mauime library from its **`net10.0`** slice only. Dumping all five slices
would make the approved file four times longer and mostly repetition, so `MultiTargetingTests` holds
the platform slices to the neutral one instead: same public surface, every framework. An
Android-only member added by accident — the easy mistake when half a plugin lives behind
`#if ANDROID` — fails there. A platform-specific member added *on purpose* means rewriting that test
to name the exception, in the commit that adds it; it must not be weakened into a subset check.

Two things make reading a platform slice work at all:

- **One `MetadataLoadContext` per assembly.** The slices of a single library are compiled against
  different worlds, and an Android `System.Runtime` and an iOS `System.Runtime` cannot share a
  resolver that matches on simple name. A single shared context silently resolves types out of
  whichever pack was enumerated first.
- **`Mauime.ReferencePaths.txt`**, written beside every assembly by `Directory.Build.targets`. A
  library build does not copy its dependencies, so `Mono.Android`, `Microsoft.iOS` and the rest are
  nowhere near `bin/`. Guessing at the workload's reference-assembly folders would be a proxy;
  the `ReferencePath` item is what the compiler was actually handed.

That this works is verified rather than assumed: a temporary type exposing
`Android.Nfc.NfcAdapter`, `CoreNFC.NFCNdefReaderSession` and
`Windows.Devices.SmartCards.SmartCardReader` rendered correctly in each slice, from a `net10.0` test
project that can reference none of them.

### `SimpleNameResolver`, and why `PathAssemblyResolver` could not be used

`PathAssemblyResolver` matches on version as well as simple name, and here there is no version to
match. `legacy/WebHostPatch` drops a **fork of `Microsoft.Extensions.Primitives` stamped 5.9.0.0**
into every output directory that references it, shadowing the real 5.0.0 that `Xamarinme.Hosting` was
compiled against. The exact version the metadata asks for is nowhere on disk, so the baseline threw
`FileNotFoundException` until the resolver was replaced with one that matches on simple name alone.

For a legacy assembly the runtime directory is probed first, so framework assemblies resolve to the
real ones. A Mauime slice does not mix the host runtime in at all: it resolves only against its own
`Mauime.ReferencePaths.txt`, so an Android slice takes `System.Runtime` from the Android ref pack
rather than from whatever runtime the tests happen to be running on.

That shadowing is not a curiosity: the test project itself runs on the fork, which is asserted in
`DEFECT_WebHostPatch_ships_a_fork_of_Microsoft_Extensions_Primitives_that_shadows_the_real_one`.

## The shape of a Mauime library

All four are the same shape, and the shape was settled by building rather than by reading:

- **`Microsoft.Maui.Core`, not `Microsoft.Maui.Controls`.** Core carries `MauiAppBuilder`, the
  `LifecycleEvents` builders and `Microsoft.Maui.ApplicationModel`. `Microsoft.Maui.Essentials` has
  none of the first two. An NFC plugin has no business depending on Controls, and now does not.
- **`Mauime.WebHostPatch` takes no packages**, only a `Microsoft.AspNetCore.App` framework
  reference. That is enough for `WebApplication` and Kestrel on `net10.0-android`.
- **`net10.0` alongside the four platform frameworks.** It is what a non-platform project resolves,
  and for `Mauime.Nfc` it is where "this platform has no implementation" lives — the role
  `Xamarinme.Nfc` gave `netstandard2.0`.
- **`AndroidGenerateResourceDesigner=false`.** Android otherwise generates a public `Resource` class
  into every library, resources or not, and it lands in the package's public surface.

The target framework list lives once, as `$(MauimeTargetFrameworks)` in `Directory.Build.props`.
`legacy/` is shielded from all of it by its own empty `Directory.Build.props` and
`Directory.Build.targets`: the imported sources must keep building exactly as they did, and enabling
`Nullable` across them would bury the four advisory warnings that matter under dozens that do not.

## What each library is, and what MAUI already does

The first question, per library, was whether it should exist at all. Verified rather than assumed —
a `net10.0-android` probe project was built to check the MAUI API, and the legacy projects were built
to check what still compiles.

### `Xamarinme.Hosting` → `Mauime.Hosting`, ported and much smaller

`XamarinHostBuilder` exposed Configuration, Services, HostEnvironment and Logging, and a `Build()`.
`MauiAppBuilder` exposes the same, member for member. `XamarinHostConfiguration` is a copy of
Blazor's `WebAssemblyHostConfiguration`; `ConfigurationManager` is the maintained equivalent. And
**MAUI registers an `IHostEnvironment` of its own** — which Phase 0 could not confirm and Phase 3
did.

So the only gap is that MAUI's environment always reports `Production`. That is what
`Mauime.Hosting` fills, and it is the whole library.

**It wraps rather than assigns**, because `MauiHostEnvironment.EnvironmentName` throws
`NotImplementedException` from its setter. The interface declares the property as settable and the
type reports `CanWrite`, so nothing short of calling it reveals otherwise — the probe that found the
registration got this wrong, and a test caught it. Everything but the name is delegated, so
`ApplicationName` and `ContentRootPath` still come from the platform, including their failure modes.

`XamarinHost` did not come across at all: it implemented `IHost` and threw
`NotImplementedException` from both `StartAsync` and `StopAsync`.

### `Xamarinme.Configuration` → `Mauime.Configuration`, ported

Two ways in — `AddAppPackageJson` for a `MauiAsset`, `AddEmbeddedResourceJson` for the embedded
resource Xamarinme used — both layering `appsettings.{environment}.json` over the base file.

**The parser is Microsoft's, referenced not vendored.** This is the opposite call from
`Mauime.Nfc`'s, and deliberately: NdefLibrary was a dead 2017 package sitting in the public API,
while `Microsoft.Extensions.Configuration.Json` is a live first-party component. Reimplementing it
to keep a zero-dependency badge would have been the same vendoring mistake Xamarinme made, in newer
clothes. MAUI does not reference it, which is the one thing this package supplies that an app could
not already do in a line.

The rendering differences are narrower than they look, and were measured rather than guessed. Both
parsers render a JSON `true` as `"True"`, through `bool.ToString()` and `JsonElement.ToString()`
respectively — the obvious assumption that Microsoft emits the raw token is wrong, and the test that
assumed it failed. **The one real difference is a JSON `null`**: `""` before, absent now.

The options object is gone, and with it three ways to turn a mistake into a `NullReferenceException`
out of `Build()` — or, for a wrong prefix, into an empty configuration and no error at all.

**The app-package path has no coverage.** MAUI's `FileSystem` on the net10.0 slice is the
reference-assembly stub and throws, so what is untested is the four lines that open the asset; both
paths funnel into the same layering code.

### `Xamarinme.WebHostPatch` → `Mauime.WebHostPatch`, with no patch in it

The two forks existed for two Mono-era problems, and **both causes are gone**:

1. `Microsoft.Net.Http.Headers` 2.2.0 calls `InplaceStringBuilder`, which
   `Microsoft.Extensions.Primitives` 5.0 deleted. The vendored fork restores the type and stamps
   itself 5.9.0.0 so it wins at bind time. This exists **only** because of the ASP.NET Core 2.2.0
   pin.
2. `Console.CancelKeyPress` threw on Mono, so `ConsoleLifetime` and `WebHostExtensions.RunAsync`
   were forked to route around it.

On .NET 10 a web host inside a MAUI app needs a `Microsoft.AspNetCore.App` framework reference and
nothing else, verified by compiling `WebApplication` plus `UseKestrel` against `net10.0-android` and
then by starting a real server in the tests. The package has **no NuGet dependencies**. What is left
is the part that was never the patch: starting and stopping the server, and knowing what address to
show the user.

The name is kept for continuity with the 30,366 downloads of `Xamarinme.WebHostPatch`, and is now a
slight misnomer. That is a deliberate trade.

**The old package is not merely obsolete, it is unsafe.** Its 2.2.0 pins carry a critical advisory
([GHSA-5rrx-jjjq-q2r5](https://github.com/advisories/GHSA-5rrx-jjjq-q2r5), Kestrel) and a moderate
one ([GHSA-prrf-397v-83xh](https://github.com/advisories/GHSA-prrf-397v-83xh), IIS), and its forked
Primitives shadows the real assembly for anything sharing an output directory — which is why this
repository needs two test projects.

### `Xamarinme.Nfc` → `Mauime.Nfc`, ported

MAUI has no NFC support, so this is the library the port was actually for. It was never published,
so there was not even an old version to stay compatible with — which is why the API is smaller than
what it replaced rather than a translation of it.

**`INfc` is the surface**, resolved from DI after `builder.UseMauimeNfc()`. Every implementation is
`internal`, which is what allows `MultiTargetingTests` to hold all five slices to one public API.
Gone: `CrossNfc.Current` (a 2019 static locator), the per-platform public `Nfc` classes, the unused
`NfcTagStatus`, and the `Nfc.OnNewIntent(intent)` call consumers had to add to their own
`MainActivity` — that hook is now wired by the library through `ConfigureLifecycleEvents`.

**The NDEF types are ours.** `NdefLibrary` 4.1.0 was verified to restore, compile and run on
.NET 10, so dropping it was a choice rather than a forced move: it is a 2017 package with a
netstandard1.4 asset only, and it was in the *public* API, since `ReadNdefAsync` returned its
`NdefMessage`. `Mauime.Nfc` has no third-party dependencies at all.

The codec is pinned against NdefLibrary's own output. Every byte vector in `NdefTests` was captured
by running NdefLibrary 4.1.0 and recording what it emitted, because the port's real risk is a codec
that disagrees with the old one and corrupts tags silently. **Do not regenerate those vectors from
the code under test.** Chunked records are refused rather than mis-parsed.

**Android and iOS only.** Mac Catalyst has no NFC hardware API — CoreNFC is iOS-only — and Windows
would mean the 678-line PC/SC layer and three `PCSC` packages, for an external USB reader rather
than phone NFC. Those two share the platform-neutral implementation, which throws
`PlatformNotSupportedException`, so an app targeting everything needs no conditional registration.

Platform sources are selected by **explicit `Compile Include` conditions**. The `Platforms/` folder
convention is an app thing; in a class library those files compile into every target framework,
which was checked rather than assumed.

### The gap in Mauime.Nfc's coverage

**Neither platform implementation has behavioural coverage.** A net10.0 test project resolves the
net10.0 slice, which is the one with no implementation, so nothing exercises `AndroidNfc` or
`IosNfc`. They are held by the API baseline, by `MultiTargetingTests` and by source pins — which is
a real guard on their shape and none at all on their behaviour.

That includes the two most valuable fixes the port made: the `PendingIntent` flags, without which
`EnableSessionAsync` throws on every Android 12 or later device, and the CoreNFC callbacks, which
used to throw on a dispatch queue where nothing could observe it and left callers awaiting forever.

`LIMITATION_The_Android_and_iOS_implementations_have_no_behavioural_coverage` asserts the gap so it
fails if it ever closes. Closing it needs a device-test harness, which was deliberately not taken
on.

## Deletions that are not ports

- **`Microsoft.Extensions.FileProviders.Xamarin`** — an empty `Class1`, referenced by nothing, not
  even by `WebHostPatch`. Never imported into this repository.
- **`Xamarin.Essentials.FileSystem.Extensions`** — one method, in the solution, referenced by
  nothing, never published, and **broken by construction**: it declares `public partial class
  FileSystem` in `namespace Xamarin.Essentials`, hijacking a Microsoft type name, and calls
  `Assembly.GetExecutingAssembly()`, which returns the *library's* assembly and so can never find the
  caller's embedded resource. `FileSystem.OpenAppPackageFileAsync` replaces it. Never imported.
- **`Microsoft.Extensions.Primitives.Patch`** — imported only because `legacy/WebHostPatch` will not
  build without it. It goes when `legacy/` goes.

## Environment facts worth not rediscovering

- The .NET 10 SDK's `dotnet new sln` produces **`Mauime.slnx`**, not a classic `.sln`. The API
  baseline finds the repository root by that exact file name.
- The MAUI workloads present here are `android`, `ios`, `maccatalyst` and `maui-windows`. A
  `net10.0-android` MAUI app builds clean in about 20 seconds.
- The **iOS and Mac Catalyst library slices compile on Windows**. Only building and signing an *app*
  for them needs a Mac, so CI on Linux will build two slices per library rather than five — but a
  macOS runner is still needed to build the demo app for those platforms later.
- `UseMaui`/`UseMauiCore`/`UseMauiEssentials` **no longer add package references implicitly** since
  .NET 8. MSBuild says so with MA002.
- Android drops a public `Resource` designer class into every library unless
  `AndroidGenerateResourceDesigner` is turned off.
