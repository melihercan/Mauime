# Mauime

Libraries and plugins for .NET MAUI applications, ported from
[Xamarinme](https://github.com/melihercan/Xamarinme) now that Xamarin is retired.

**This repository is mid-port and ships nothing yet.** The Xamarin sources live under `legacy/` and
are being replaced library by library. Nothing under `Mauime.*` has been published to nuget.org.

## Planned packages

| Package | Replaces | Status |
|---|---|---|
| `Mauime.Nfc` | `Xamarinme.Nfc` (never published) | **Ported** — Android and iOS |
| `Mauime.Configuration` | `Xamarinme.Configuration` 1.0.2 | Project set up, no code yet |
| `Mauime.Hosting` | `Xamarinme.Hosting` 1.0.3 | Project set up, no code yet |
| `Mauime.WebHostPatch` | `Xamarinme.WebHostPatch` 1.0.0 | Project set up, no code yet |

Each multi-targets `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` and
`net10.0-windows10.0.19041.0`.

The `Xamarinme.*` packages stay on nuget.org as they are. They are not deprecated: Xamarin retiring
is what ended them, not a defect in the packages.

## Building

Requires the **.NET 10 SDK** and the `android`, `ios`, `maccatalyst` and `maui-windows` workloads.

```powershell
dotnet build Mauime.slnx
dotnet test
```

See [Building and Testing](wiki/Building-and-Testing.md) for the details that will otherwise cost
you an afternoon, and [the modernization log](wiki/Modernization-Log.md) for what has happened so
far.

## Licence

MIT. See [LICENSE](LICENSE).
