# Me.Toolkit.Maui

Libraries and plugins for .NET MAUI applications, ported from
[Xamarinme](https://github.com/melihercan/Xamarinme) now that Xamarin is retired.

**Three of the four are published at `26.9.9`.** `Me.Toolkit.Maui.Nfc` is held back as unfinished —
it builds and is tested, but its reading session has never been used against a physical tag. See
[Publishing](Publishing). `DemoApp/` is a single MAUI app with a tab per library, replacing Xamarinme's three
Xamarin.Forms solutions across 17 projects. The Xamarin sources were imported under `legacy/` for the
characterization phase and deleted once each library had been replaced — the history is the record.

The `Xamarinme.*` packages stay on nuget.org exactly as they are, and are **not** deprecated. That
is a decision, not an oversight: `Blazorme.TestHost` looked like a precedent, but it worked because
it kept the *same* package ID, so existing consumers could resolve a newer working version. Nothing
referencing `Xamarinme.WebHostPatch` will ever resolve `Me.Toolkit.Maui.WebHostPatch` — NuGet has no redirect
between IDs.

| Planned package | Replaces | Status |
|---|---|---|
| `Me.Toolkit.Maui.Nfc` | `Xamarinme.Nfc` — never published | **Ported, not published** — Android and iOS implemented, session unfinished; `IsPackable=false` |
| [`Me.Toolkit.Maui.Configuration`](https://www.nuget.org/packages/Me.Toolkit.Maui.Configuration) | [`Xamarinme.Configuration`](https://www.nuget.org/packages/Xamarinme.Configuration) 1.0.2 | **Published** 26.9.9 |
| [`Me.Toolkit.Maui.Hosting`](https://www.nuget.org/packages/Me.Toolkit.Maui.Hosting) | [`Xamarinme.Hosting`](https://www.nuget.org/packages/Xamarinme.Hosting) 1.0.3 | **Published** 26.9.9 |
| [`Me.Toolkit.Maui.WebHostPatch`](https://www.nuget.org/packages/Me.Toolkit.Maui.WebHostPatch) | [`Xamarinme.WebHostPatch`](https://www.nuget.org/packages/Xamarinme.WebHostPatch) 1.0.0 | **Published** 26.9.9 — no patch needed on .NET 10 |

The repository was called Mauime. It is not, because nuget.org rejects any package ID beginning with
`Maui` as reserved — see [Publishing](Publishing) for what that took to establish.

All four multi-target `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` and
`net10.0-windows10.0.19041.0`.

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
- **[Publishing](Publishing)** — the tags, why one workflow, and why it must run on Windows.
