# Mauime

Libraries and plugins for .NET MAUI applications, ported from
[Xamarinme](https://github.com/melihercan/Xamarinme) now that Xamarin is retired.

**All four libraries are ported and packaged at `26.9.8`; nothing is published yet.** The publishing
workflow exists and three of the four are ready to go out — `Mauime.Nfc` is held back as unfinished.
What remains is the nuget.org Trusted Publishing policy and the first tag; see
[Publishing](Publishing). `DemoApp/` is a single MAUI app with a tab per library, replacing Xamarinme's three
Xamarin.Forms solutions across 17 projects. The Xamarin sources were imported under `legacy/` for the
characterization phase and deleted once each library had been replaced — the history is the record.

The `Xamarinme.*` packages stay on nuget.org exactly as they are, and are **not** deprecated. That
is a decision, not an oversight: `Blazorme.TestHost` looked like a precedent, but it worked because
it kept the *same* package ID, so existing consumers could resolve a newer working version. Nothing
referencing `Xamarinme.WebHostPatch` will ever resolve `Melihercan.Mauime.WebHostPatch` — NuGet has no redirect
between IDs.

| Planned package | Replaces | Status |
|---|---|---|
| `Melihercan.Mauime.Nfc` | `Xamarinme.Nfc` — never published | **Ported, not published** — Android and iOS implemented, session unfinished; `IsPackable=false` |
| `Melihercan.Mauime.Configuration` | [`Xamarinme.Configuration`](https://www.nuget.org/packages/Xamarinme.Configuration) 1.0.2 | **Ported** |
| `Melihercan.Mauime.Hosting` | [`Xamarinme.Hosting`](https://www.nuget.org/packages/Xamarinme.Hosting) 1.0.3 | **Ported** |
| `Melihercan.Mauime.WebHostPatch` | [`Xamarinme.WebHostPatch`](https://www.nuget.org/packages/Xamarinme.WebHostPatch) 1.0.0 | **Ported** — no patch needed on .NET 10 |

The published IDs carry a `Toolkit.` prefix — `Melihercan.Mauime.Configuration` — because nuget.org
rejects any ID beginning with `Maui` as reserved. Projects, assemblies and namespaces are unchanged;
see [Publishing](Publishing).

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
