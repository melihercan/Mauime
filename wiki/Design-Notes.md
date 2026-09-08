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

### `SimpleNameResolver`, and why `PathAssemblyResolver` could not be used

`PathAssemblyResolver` matches on version as well as simple name, and here there is no version to
match. `legacy/WebHostPatch` drops a **fork of `Microsoft.Extensions.Primitives` stamped 5.9.0.0**
into every output directory that references it, shadowing the real 5.0.0 that `Xamarinme.Hosting` was
compiled against. The exact version the metadata asks for is nowhere on disk, so the baseline threw
`FileNotFoundException` until the resolver was replaced with one that matches on simple name alone.

The runtime directory is probed first, so framework assemblies still resolve to the real ones.

That shadowing is not a curiosity: the test project itself runs on the fork, which is asserted in
`DEFECT_WebHostPatch_ships_a_fork_of_Microsoft_Extensions_Primitives_that_shadows_the_real_one`.

## What each library is, and what MAUI already does

The first question, per library, was whether it should exist at all. Verified rather than assumed —
a `net10.0-android` probe project was built to check the MAUI API, and the legacy projects were built
to check what still compiles.

### `Xamarinme.Hosting` → `MauiAppBuilder`

`XamarinHostBuilder` exposes `Configuration`, `Services`, `HostEnvironment` and `Logging`, and a
`Build()`. `MauiAppBuilder` exposes `Configuration` (a `ConfigurationManager`), `Services` and
`Logging`, and a `Build()` — member for member, confirmed by compiling against it.
`XamarinHostConfiguration` is a copy of Blazor's `WebAssemblyHostConfiguration`;
`ConfigurationManager` is the maintained equivalent of the same idea.

What MAUI does **not** give you is the environment name. Xamarin had no usable environment
variables, so `XamarinHostBuilder` reads a `XAMARIN_ENVIRONMENT` entry out of `appsettings.json`
instead. That is the only part of this library with anything left to do.

`XamarinHost` itself must not be carried over: it implements `IHost` and then throws
`NotImplementedException` from both `StartAsync` and `StopAsync`.

### `Xamarinme.Configuration` → `MauiAsset` plus `AddJsonStream`

The library is an embedded-resource JSON provider wrapped around a vendored copy of Microsoft's
**Newtonsoft-era** `JsonConfigurationFileParser` — the one Microsoft itself replaced with a
System.Text.Json implementation in .NET Core 3.0. Keeping it means keeping Newtonsoft.

`FileSystem.OpenAppPackageFileAsync` exists in MAUI and reads a `MauiAsset`, confirmed by compiling
against it. `builder.Configuration.AddJsonStream(...)` does **not** compile out of the box: MAUI does
not reference `Microsoft.Extensions.Configuration.Json` transitively, so replacing this library costs
a package reference as well as a few lines.

Carrying the vendored parser forward would also carry its rendering: Newtonsoft renders a JSON `true`
as `"True"` and a JSON `null` as `""`, where Microsoft's parser emits `"true"` and `null`. Both are
pinned in `ConfigurationCharacterizationTests`. The library's own README claims
`Configuration["Logging:IncludeScopes"]` reads `false`; it reads `False`.

### `Xamarinme.WebHostPatch` → nothing, on .NET 10

Two forks of Microsoft code, for two Mono-era problems, and **both causes are gone**:

1. `Microsoft.Net.Http.Headers` 2.2.0 calls `InplaceStringBuilder`, which
   `Microsoft.Extensions.Primitives` 5.0 deleted. The vendored fork restores the type and stamps
   itself 5.9.0.0 so it wins at bind time. This exists **only** because of the ASP.NET Core 2.2.0
   pin — on .NET 10 there is no 2.2-versus-5.0 mismatch to work around.
2. `Console.CancelKeyPress` threw on Mono, so `ConsoleLifetime` and `WebHostExtensions.RunAsync`
   were forked to route around it. Modern code uses `WebApplication` and `RunAsync(token)` and never
   enters that path.

It is also written against `IWebHost`, `WebHostBuilder`, `IHostingEnvironment` and
`IApplicationLifetime` — the removed ASP.NET Core 2.x API.

**And it is not merely obsolete, it is unsafe.** Its 2.2.0 pins carry a critical advisory
([GHSA-5rrx-jjjq-q2r5](https://github.com/advisories/GHSA-5rrx-jjjq-q2r5), Kestrel) and a moderate
one ([GHSA-prrf-397v-83xh](https://github.com/advisories/GHSA-prrf-397v-83xh), IIS). Version 1.0.0 is
on nuget.org with 30,366 downloads and every consumer inherits both.

### `Xamarinme.Nfc` → the one with a reason to exist

~1,260 real lines: Android (`NfcAdapter`, foreground dispatch, activity lifecycle), iOS (CoreNFC),
and UWP plus macOS as thin shims over a shared 678-line PC/SC implementation. MAUI has no NFC
support, so this is the library the port is actually for. It was never published, so there is not
even an old version to be compatible with.

Three things it drags along:

- **`NdefLibrary` 4.1.0 is from 2017 and ships a `netstandard1.4` asset only**, and it is in the
  *public API*: `INfc.ReadNdefAsync()` returns `NdefLibrary.Ndef.NdefMessage`. New package IDs mean
  this can be replaced; keeping it bakes a dead dependency into the surface.
- `PCSC`, `PCSC.Iso7816` and `PCSC.Reactive` are alive at **7.0.1**; the repository pins 5.0.0.
- Xamarin.Mac → Mac Catalyst is not a rename. Catalyst is sandboxed, and the PC/SC path may not
  survive it.

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
- **CI will need a macOS runner** for the `ios` and `maccatalyst` slices of `Mauime.Nfc`.
