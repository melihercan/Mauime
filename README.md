# Mauime

Libraries and plugins for .NET MAUI applications, ported from
[Xamarinme](https://github.com/melihercan/Xamarinme) now that Xamarin is retired.

**All four libraries are ported; nothing is published yet.** What remains is a demo app, package
metadata and publishing.

## Planned packages

| Package | Replaces | Status |
|---|---|---|
| `Mauime.Nfc` | `Xamarinme.Nfc` (never published) | **Ported** — Android and iOS |
| `Mauime.Configuration` | `Xamarinme.Configuration` 1.0.2 | **Ported** |
| `Mauime.Hosting` | `Xamarinme.Hosting` 1.0.3 | **Ported** |
| `Mauime.WebHostPatch` | `Xamarinme.WebHostPatch` 1.0.0 | **Ported** — no patch needed on .NET 10 |

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
