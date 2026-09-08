# Me.Toolkit.Maui.Hosting

Sets the host environment name of a .NET MAUI app, so `IsDevelopment()` means something.

Ported from `Xamarinme.Hosting` — and it is deliberately much smaller than what it replaces.

## Use

```csharp
using Me.Toolkit.Maui.Hosting;

var builder = MauiApp.CreateBuilder();
builder.UseMeToolkitMauiHosting("Development");
```

Or take the name from configuration, which is how `Xamarinme.Hosting` worked:

```csharp
builder.Configuration.AddAppPackageJson();
builder.UseMeToolkitMauiHostingFromConfiguration();     // reads "MAUI_ENVIRONMENT"
```

```json
{ "MAUI_ENVIRONMENT": "Development" }
```

The key is read when `IHostEnvironment` is first resolved, not when the call is made, so the order
of the two lines does not matter. Pass a different key if `MAUI_ENVIRONMENT` does not suit;
Xamarinme called it `XAMARIN_ENVIRONMENT`.

Then inject the framework's own interface — there is no Me.Toolkit.Maui-specific one:

```csharp
public sealed class Service(IHostEnvironment environment)
{
    public bool IsDev => environment.IsDevelopment();
}
```

## What comes from the platform

Only the environment name is Me.Toolkit.Maui's. `ApplicationName`, `ContentRootPath` and
`ContentRootFileProvider` are delegated to MAUI's own host environment, unchanged — including their
failure modes.

**`ContentRootPath` throws `NotImplementedException` on Android**, and this package passes that
through rather than inventing a value. Verified on a device. If you need a writable path, use
`FileSystem.AppDataDirectory`; if you need one for an ASP.NET Core content root,
`AppContext.BaseDirectory` is what `Me.Toolkit.Maui.WebHostPatch` defaults to.

## Why this is so small

`Xamarinme.Hosting` existed because Xamarin had no hosting at all. It built a parallel
`XamarinHostBuilder` exposing Configuration, Services, Logging and an environment, plus an `IHost`
that threw `NotImplementedException` from both `StartAsync` and `StopAsync`.

`MauiAppBuilder` is that builder, and MAUI registers an `IHostEnvironment` of its own. The only gap
is that MAUI's always reports `Production` and its `EnvironmentName` setter throws. This package
wraps it, so the name can change while `ApplicationName` and `ContentRootPath` keep coming from the
platform.

If you only ever need a constant environment name, you do not need this package — but you do need to
wrap rather than assign, which is the part that is easy to get wrong.

## Documentation

Full documentation is in the [wiki](https://github.com/melihercan/Me.Toolkit.Maui/wiki) — [Design Notes](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Design-Notes) for why each
library is shaped the way it is, [Building and Testing](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Building-and-Testing) for the
multi-targeting rules, and [Publishing](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Publishing) for how these packages are released.

Source: [github.com/melihercan/Me.Toolkit.Maui](https://github.com/melihercan/Me.Toolkit.Maui)

## Licence

MIT.
