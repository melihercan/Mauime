# Publishing

Packaging is done by GitHub Actions, in `.github/workflows/`.

- **`ci.yml`** — build and test on every push and PR to master. Builds with `-warnaserror`, so the
  repository's zero-warning bar is enforced there, and a new NuGet advisory fails the build.
- **`publish.yml`** — **one workflow for all three packages, deliberately.**

## One workflow, on purpose

A NuGet Trusted Publishing policy is bound to a **single workflow file** and a **single repository**,
so one file means one policy covers every package, every future version, and any future `Mauime.X`
package. **Do not split publishing into per-package workflow files** — that would require a policy
per file.

## Ways in

| Trigger | Publishes |
|---|---|
| tag `v<version>` | every package |
| tag `configuration-v<version>` | `Mauime.Configuration` only |
| tag `hosting-v<version>` | `Mauime.Hosting` only |
| tag `webhostpatch-v<version>` | `Mauime.WebHostPatch` only |
| manual run | defaults to a **dry run** that packs and uploads the `.nupkg` files as artifacts without publishing |

The workflow verifies the tag matches each project's `<Version>`, runs the tests, and pushes with
`--skip-duplicate` so a re-run is harmless. It packs with `--no-build`, so what is published is
exactly what the tests ran against.

## `Mauime.Nfc` is not published

It builds, ships in the demo, and has tests — but its reading session is unfinished and has never
been exercised against a physical tag. The project sets `IsPackable=false`, so a plain
`dotnet pack` on the solution cannot produce a `Mauime.Nfc.nupkg` by accident; `publish.yml` has no
`nfc-v*` trigger either. Remove the `IsPackable` line and add the trigger together when it is ready.

It is still **built** by the publish job, because the tests read every library's compiled assemblies
off disk through `MetadataLoadContext`. Skipping its build fails `PublicApiSurfaceTests` and
`MultiTargetingTests`, not just its own tests.

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
| Glob | `Mauime.*` |

The policy binds to the repository **ID**, not just its name, so a policy created for another
repository can never cover this one however wide its glob.

Because `Mauime.*` are new IDs with no existing owner, the glob has to permit **new** packages, not
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
exactly as they are and are not deprecated — nothing referencing them will ever resolve a `Mauime.*`
package, because NuGet has no redirect between IDs.

## There is no demo deployment

`DemoApp` is a MAUI app, not a web app, so there is no equivalent of a GitHub Pages demo to publish.
It is deliberately excluded from both workflows: it is slow to build and nothing in it ships. Its
value is in being run on a device, which is where the `0.0.0.0` defect in `NetworkAddress` was found
— see the [Modernization Log](Modernization-Log).
