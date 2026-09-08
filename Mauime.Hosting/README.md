# Mauime.Hosting

Sets the host environment name of a .NET MAUI app, so `IsDevelopment()` means something.

Ported from `Xamarinme.Hosting` — and it is deliberately much smaller than what it replaces.

## Use

```csharp
using Mauime.Hosting;

var builder = MauiApp.CreateBuilder();
builder.UseMauimeHosting("Development");
```

Or take the name from configuration, which is how `Xamarinme.Hosting` worked:

```csharp
builder.Configuration.AddAppPackageJson();
builder.UseMauimeHostingFromConfiguration();     // reads "MAUI_ENVIRONMENT"
```

```json
{ "MAUI_ENVIRONMENT": "Development" }
```

The key is read when `IHostEnvironment` is first resolved, not when the call is made, so the order
of the two lines does not matter. Pass a different key if `MAUI_ENVIRONMENT` does not suit;
Xamarinme called it `XAMARIN_ENVIRONMENT`.

Then inject the framework's own interface — there is no Mauime-specific one:

```csharp
public sealed class Service(IHostEnvironment environment)
{
    public bool IsDev => environment.IsDevelopment();
}
```

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

## Licence

MIT.
