# Mauime

Libraries and plugins for .NET MAUI applications, ported from
[Xamarinme](https://github.com/melihercan/Xamarinme) now that Xamarin is retired.

**Nothing is published yet.** The port is in progress; the Xamarin sources live under `legacy/` and
are being replaced library by library.

| Planned package | Replaces | Status |
|---|---|---|
| `Mauime.Nfc` | `Xamarinme.Nfc` — never published | Not started |
| `Mauime.Configuration` | [`Xamarinme.Configuration`](https://www.nuget.org/packages/Xamarinme.Configuration) 1.0.2 | Not started |
| `Mauime.Hosting` | [`Xamarinme.Hosting`](https://www.nuget.org/packages/Xamarinme.Hosting) 1.0.3 | Not started |
| `Mauime.WebHostPatch` | [`Xamarinme.WebHostPatch`](https://www.nuget.org/packages/Xamarinme.WebHostPatch) 1.0.0 | Not started |

This is a **port, not a framework bump**. Xamarin.Forms became MAUI, `MSBuild.Sdk.Extras` with
`MonoAndroid10.0`/`Xamarin.iOS10`/`uap10.0.19041`/`Xamarin.Mac20` became MAUI's built-in
multi-targeting over `net10.0-android`/`-ios`/`-maccatalyst`/`-windows`, and three Xamarin-shaped
libraries are being reshaped around what MAUI already provides rather than duplicating it. See
[Design Notes](Design-Notes) for what each library is and what MAUI does for you now.

The package IDs are new, so there are no existing consumers and **no additive-only guarantee**: the
API is free to be fixed. The API baseline exists so that changing it is a decision rather than an
accident.

The `Xamarinme.*` packages stay on nuget.org as they are. Xamarin retiring is what ended them.

**Careful with versions**: the Xamarinme csprojs say Configuration 1.0.3 and Hosting 1.0.4, which are
*ahead* of what was ever published. Never read a live version out of a csproj here.

## Where things are

- **[Building and Testing](Building-and-Testing)** — the commands, why the build is not
  warning-free, and what the test suite covers.
- **[Design Notes](Design-Notes)** — why the repository looks like this, and the invariants.
- **[Modernization Log](Modernization-Log)** — what changed, phase by phase.
