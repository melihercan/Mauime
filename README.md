# Me.Toolkit.Maui

Libraries and plugins for .NET MAUI applications, ported from
[Xamarinme](https://github.com/melihercan/Xamarinme) now that Xamarin is retired.

**Three of the four are published at `26.9.9`** — `Me.Toolkit.Maui.Configuration`,
`Me.Toolkit.Maui.Hosting` and `Me.Toolkit.Maui.WebHostPatch`. `Me.Toolkit.Maui.Nfc` is ported and
tested but held back: its reading session is unfinished and has never been used against a tag.

`DemoApp/` is a single .NET MAUI app with a tab per library, replacing Xamarinme's three
Xamarin.Forms solutions across 17 projects. Run it with:

```powershell
dotnet build DemoApp/DemoApp.csproj -f net10.0-windows10.0.19041.0
```

## Planned packages

| Package | Replaces | Status |
|---|---|---|
| `Me.Toolkit.Maui.Nfc` | `Xamarinme.Nfc` (never published) | Ported — Android and iOS |
| `Me.Toolkit.Maui.Configuration` | `Xamarinme.Configuration` 1.0.2 | **Ported** |
| `Me.Toolkit.Maui.Hosting` | `Xamarinme.Hosting` 1.0.3 | **Ported** |
| `Me.Toolkit.Maui.WebHostPatch` | `Xamarinme.WebHostPatch` 1.0.0 | **Ported** — no patch needed on .NET 10 |

Each multi-targets `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` and
`net10.0-windows10.0.19041.0`.

The `Xamarinme.*` packages stay on nuget.org as they are. They are not deprecated: Xamarin retiring
is what ended them, not a defect in the packages.

## Building

Requires the **.NET 10 SDK** and the `android`, `ios`, `maccatalyst` and `maui-windows` workloads.

```powershell
dotnet build Me.Toolkit.Maui.slnx
dotnet test
```

See [Building and Testing](wiki/Building-and-Testing.md) for the details that will otherwise cost
you an afternoon, and [the modernization log](wiki/Modernization-Log.md) for what has happened so
far.

## Licence

MIT. See [LICENSE](LICENSE).
