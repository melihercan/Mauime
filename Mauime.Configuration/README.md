# Mauime.Configuration

`appsettings.json` for .NET MAUI apps, from the app package or from embedded resources, with the
usual `appsettings.{environment}.json` overlay.

Ported from `Xamarinme.Configuration`.

## Use

Mark the file as a `MauiAsset`:

```xml
<ItemGroup>
  <MauiAsset Include="appsettings.json" />
  <MauiAsset Include="appsettings.Development.json" />
</ItemGroup>
```

Then, in `MauiProgram`:

```csharp
using Mauime.Configuration;

var builder = MauiApp.CreateBuilder();
builder.Configuration.AddAppPackageJson(environment: "Development");
```

Or keep the file as an `EmbeddedResource`, which is what Xamarinme did:

```csharp
builder.Configuration.AddEmbeddedResourceJson(
    typeof(App).Assembly, prefix: "MyApp", environment: "Development");
```

`prefix` is the default namespace plus any folders the file sits in, dot-separated — `MyApp.res` for
a file in a `res` folder.

Both take `optional: false` if a missing file should throw rather than be skipped, and the exception
names the file and where it looked.

## What this adds over plain MAUI

MAUI references `Microsoft.Extensions.Configuration` but **not** the JSON provider, so
`builder.Configuration` has no `AddJsonStream`. That is what this package supplies, along with the
environment overlay and the two ways of finding the file. The parsing is Microsoft's.

Xamarinme vendored a copy of Microsoft's Newtonsoft-era `JsonConfigurationFileParser` — the one
Microsoft replaced with a System.Text.Json implementation in .NET Core 3.0 — and froze it there,
with a Newtonsoft.Json dependency attached. Nothing is vendored here.

One behavioural difference from `Xamarinme.Configuration` is worth knowing: a JSON `null` used to
read as `""`, so the key existed; it is now absent, and `GetValue<T>` falls back to the default.
Booleans still render `"True"` — both parsers do that, so a string comparison against `"true"` was
always wrong. Use `GetValue<bool>`.

## Licence

MIT.
