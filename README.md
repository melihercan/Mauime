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

## Packages

| Package | Replaces | Status |
|---|---|---|
| [`Me.Toolkit.Maui.Configuration`](https://www.nuget.org/packages/Me.Toolkit.Maui.Configuration) | `Xamarinme.Configuration` 1.0.2 | **26.9.9** |
| [`Me.Toolkit.Maui.Hosting`](https://www.nuget.org/packages/Me.Toolkit.Maui.Hosting) | `Xamarinme.Hosting` 1.0.3 | **26.9.9** |
| [`Me.Toolkit.Maui.WebHostPatch`](https://www.nuget.org/packages/Me.Toolkit.Maui.WebHostPatch) | `Xamarinme.WebHostPatch` 1.0.0 | **26.9.9** — no patch needed on .NET 10 |
| `Me.Toolkit.Maui.Nfc` | `Xamarinme.Nfc` (never published) | Not published — ported, but the reading session is unfinished |

The IDs are prefixed because nuget.org reserves any package ID beginning with `Maui`; the repository
was called Mauime. See [Publishing](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Publishing).

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

## Documentation

The [wiki](https://github.com/melihercan/Me.Toolkit.Maui/wiki) is the long-form documentation. Its pages are maintained in `wiki/` in this
repository and published from there, so they are reviewed with the code they describe.

| Page | What it covers |
|---|---|
| [Building and Testing](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Building-and-Testing) | The commands, the multi-targeting rules, and the details that otherwise cost an afternoon |
| [Design Notes](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Design-Notes) | Why the repository looks like this, what each library is, and what MAUI now does for you |
| [Publishing](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Publishing) | Tags, Trusted Publishing, why packing must run on Windows, and why the packages are not called Mauime |
| [Modernization Log](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Modernization-Log) | What changed, phase by phase, including what went wrong |

Each package also has its own README, shown on its nuget.org page:
[Configuration](Me.Toolkit.Maui.Configuration/README.md) ·
[Hosting](Me.Toolkit.Maui.Hosting/README.md) ·
[Nfc](Me.Toolkit.Maui.Nfc/README.md) ·
[WebHostPatch](Me.Toolkit.Maui.WebHostPatch/README.md)

## Licence

MIT. See [LICENSE](LICENSE).
