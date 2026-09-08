# Publishing

Packaging is done by GitHub Actions, in `.github/workflows/`.

- **`ci.yml`** — build and test on every push and PR to master. Builds with `-warnaserror`, so the
  repository's zero-warning bar is enforced there, and a new NuGet advisory fails the build.
- **`publish.yml`** — **one workflow for all three packages, deliberately.**

## One workflow, on purpose

A NuGet Trusted Publishing policy is bound to a **single workflow file** and a **single repository**,
so one file means one policy covers every package, every future version, and any future `Melihercan.Mauime.X`
package. **Do not split publishing into per-package workflow files** — that would require a policy
per file.

## Ways in

| Trigger | Publishes |
|---|---|
| tag `v<version>` | every package |
| tag `configuration-v<version>` | `Melihercan.Mauime.Configuration` only |
| tag `hosting-v<version>` | `Melihercan.Mauime.Hosting` only |
| tag `webhostpatch-v<version>` | `Melihercan.Mauime.WebHostPatch` only |
| manual run | defaults to a **dry run** that packs and uploads the `.nupkg` files as artifacts without publishing |

The workflow verifies the tag matches each project's `<Version>`, runs the tests, and packs with
`--no-build`, so what is published is exactly what the tests ran against.

A tag push does **not** pass `--skip-duplicate`. That flag is for deliberately re-running a release
that is already out, and it cannot distinguish that from a refusal — the first attempt to publish
these packages was rejected three times with `409 The package ID is reserved` and reported success,
because that is a 409 like any other. Only a manual run can ask for it.

## `Melihercan.Mauime.Nfc` is not published

It builds, ships in the demo, and has tests — but its reading session is unfinished and has never
been exercised against a physical tag. The project sets `IsPackable=false`, so a plain
`dotnet pack` on the solution cannot produce a `Melihercan.Mauime.Nfc.nupkg` by accident; `publish.yml` has no
`nfc-v*` trigger either. Remove the `IsPackable` line and add the trigger together when it is ready.

It is still **built** by the publish job, because the tests read every library's compiled assemblies
off disk through `MetadataLoadContext`. Skipping its build fails `PublicApiSurfaceTests` and
`MultiTargetingTests`, not just its own tests.

## Why the IDs are prefixed

The packages are `Melihercan.Mauime.*`, while the projects, assemblies and namespaces are `Mauime.*`.
That mismatch is deliberate, and it is not a style choice.

`Mauime.Configuration` cannot be published. nuget.org rejects it:

```
409 The package ID is reserved. You can upload your package with a different
package ID. Reach out to support@nuget.org if you have questions.
```

An ID beginning with `Maui` is reserved. No package on nuget.org starts with those four letters —
200 search results and eight targeted probes found none — and every community MAUI package puts the
word second: `CommunityToolkit.Maui`, `Plugin.Maui.Audio`, `Syncfusion.Maui.Toolkit`. The clearest
case is `Reactor.Maui`, whose project is *called* MauiReactor.

`Blazor` and `Xamarin` were never reserved this way, which is why `Blazorme.*` and `Xamarinme.*`
publish without trouble. Reservations are granted per request, not automatically for every Microsoft
technology name, and MAUI's was taken when it shipped in 2022.

Only the published ID changes. Consumers install `Melihercan.Mauime.Configuration` and write
`using Mauime.Configuration;`, which is ordinary for a vendor- or toolkit-prefixed package.

`Toolkit.*` was tried first and refused the same way: *"This package ID has been reserved. Please
request access to upload to this reserved namespace from the owner of the reserved prefix."* That
`Toolkit.Data` and `Toolkit.CodeBase` already exist proves nothing - a reservation blocks only new
IDs, which is the same reason `Xamarinme.*` survives while `Mauime.*` does not.

There is no way to query reservations; the only test is a push. So any ordinary English word is a
gamble, and an identity prefix is the only kind nobody else can hold. `Melihercan.*` is also
eligible for a prefix reservation of your own, which would give the verified badge on everything
published under it.

## This must run on Windows

`publish.yml` uses `runs-on: windows-latest`, and unlike `ci.yml` this is not merely about coverage —
it decides what ends up **inside the published package**. `Directory.Build.props` builds
`net10.0-ios` and `net10.0-maccatalyst` only off Linux, and `net10.0-windows10.0.19041.0` only on
Windows:

| Runner | Slices packed |
|---|---|
| `ubuntu-latest` | 2 of 5 — `net10.0`, `net10.0-android` |
| `macos-latest` | 4 of 5 — no Windows |
| `windows-latest` | **all 5** |

Packing on the wrong runner does not fail. It produces a valid package that silently supports two
platforms instead of five, publishes it with a green tick, and nobody finds out until someone's iOS
app fails to restore it. Since a published version cannot be replaced, that is a permanent mistake.

So the job ends with **“Verify every package carries all five target frameworks”**, which opens each
`.nupkg` and fails the run if any expected slice is missing. The framework names are matched by
prefix, because the platform versions are stamped in — `net10.0-android36.0`,
`net10.0-windows10.0.19041`. Treat that step as the real rule and the `runs-on` line as its
consequence.

## Tag with the csproj spelling

The projects declare `<Version>26.09.08</Version>` and NuGet normalises that to `26.9.8`. The
verification step compares against the **raw csproj text**, so the tag is:

```
v26.09.08
```

not `v26.9.8`. Tagging the normalised form fails the version check — which is the point: it is the
same check that catches tagging a version no project declares.

## Trusted Publishing, not an API key

nuget.org discourages API keys for automated publishing. The job requests a GitHub OIDC token
(`id-token: write`) and `NuGet/login@v1` exchanges it for an API key valid for one hour. **No
publishing secret is stored in the repository — do not introduce `NUGET_API_KEY`.**

The login step sits immediately before the push on purpose: the key expires, and each OIDC token
buys exactly one key.

### The policy

**This has to exist before the first tag is pushed.** Without it the run does everything correctly
and fails at the final step. Created on nuget.org under Account → Trusted Publishing:

| Field | Value |
|---|---|
| Package Owner | `melihercan` |
| CI/CD Provider | GitHub Actions |
| Repository Owner | `melihercan` |
| Repository | `Mauime` |
| Workflow File | `publish.yml` (file name only, no path) |
| Environment | *(none)* |
| Scopes | Push new packages and package versions |
| Glob | `Melihercan.Mauime.*` |

The policy binds to the repository **ID**, not just its name, so a policy created for another
repository can never cover this one however wide its glob.

Because `Melihercan.Mauime.*` are new IDs with no existing owner, the glob has to permit **new** packages, not
just new versions of existing ones — that is the "Push new packages and package versions" scope
above. A policy limited to new versions works for every release after the first and fails on the
first.

### Rehearsing without publishing

Run the workflow manually from the Actions tab and leave **Dry run** ticked. It packs, prints the
contents of every `.nupkg`, runs the five-framework check, and uploads the packages as build
artifacts — without ever requesting an OIDC token. That exercises everything except the policy
itself, and is worth doing once before the first real tag.

## Versioning

Date-based: `26.09.08`, normalised by NuGet to `26.9.8`, matching the convention used across these
repositories. Bump `<Version>` in each `.csproj` together with its `<PackageReleaseNotes>`.

`AssemblyVersion` and `FileVersion` are left to derive from `Version` rather than being set
separately, so they cannot drift.

Nothing here inherits a version history: these are new package IDs with no consumers, so there is no
lowest-version or downgrade problem to reason about. The `Xamarinme.*` packages stay on nuget.org
exactly as they are and are not deprecated — nothing referencing them will ever resolve a `Melihercan.Mauime.*`
package, because NuGet has no redirect between IDs.

## There is no demo deployment

`DemoApp` is a MAUI app, not a web app, so there is no equivalent of a GitHub Pages demo to publish.
It is deliberately excluded from both workflows: it is slow to build and nothing in it ships. Its
value is in being run on a device, which is where the `0.0.0.0` defect in `NetworkAddress` was found
— see the [Modernization Log](Modernization-Log).
