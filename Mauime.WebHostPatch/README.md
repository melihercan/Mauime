# Mauime.WebHostPatch

Run an ASP.NET Core web host and Kestrel server inside a .NET MAUI app.

Ported from `Xamarinme.WebHostPatch`. **The name is kept for continuity, but there is no patch any
more** — see below.

## Use

```csharp
using Mauime.WebHostPatch;

var builder = MauiApp.CreateBuilder();
builder.UseMauimeWebHost(options =>
{
    options.Port = 5000;                  // 0 lets the OS pick; the bound port is reported back
    options.ConfigureApplication = app => app.MapGet("/", () => "Hello from a phone");
});
```

The server is registered, not started. Start it when the app is ready:

```csharp
public sealed partial class MainPage(IMauimeWebHost host) : ContentPage
{
    private async void OnStartClicked(object sender, EventArgs e)
    {
        var address = await host.StartAsync();
        UrlLabel.Text = address.ToString();     // e.g. http://192.168.1.24:5000/
    }
}
```

`Address` is read from the server after it binds, so it carries the real port even when `Port` was
left at 0. When listening on every interface — the default — the reported host is the machine's LAN
address rather than `0.0.0.0`, so it is something you can type into another device.
`NetworkAddress.GetLocalAddress()` exposes that lookup directly, and returns `null` when the machine
has no routable address, in which case `Address` keeps whatever the server bound to.

That lookup does not filter on `NetworkInterfaceType`, and the reason is worth knowing if you write
your own: .NET classifies an interface on Linux by reading `/sys/class/net/<name>/type`, and
Android's SELinux policy denies that file — to an app, and even to the more privileged `shell` user.
A working Wi-Fi interface therefore reports `Unknown`, so the obvious `Ethernet or Wireless80211`
test rejects it and the app reports `0.0.0.0` — under a label telling the user to open it from
another device. Verified on an Android 14 device, which reported `wlan0|Unknown|Up|[192.168.1.16]`.

Loopback is excluded by address rather than by interface type. That same device does classify `lo`
correctly, so the type would have served — but the point of the change is that classification cannot
be relied on, and an address check does not depend on it.

`StopAsync` shuts the server down gracefully and frees the port; the host can be started again
afterwards. Disposing it stops it.

**Android** needs `android.permission.INTERNET`. Listening on a port under 1024 is not permitted on
either mobile platform, and iOS will stop a server that is running when the app is backgrounded.

## There is no patch any more

`Xamarinme.WebHostPatch` was two forks of Microsoft's code, because ASP.NET Core 2.2 on Mono needed
them:

- `Console.CancelKeyPress` threw, so `ConsoleLifetime` and `WebHostExtensions.RunAsync` were
  reimplemented around it.
- `Microsoft.Net.Http.Headers` 2.2.0 called an `InplaceStringBuilder` that
  `Microsoft.Extensions.Primitives` 5.0 had deleted, so a fork of Primitives stamped **5.9.0.0** was
  shipped to outrank the real one at bind time.

Neither fork came across, but not because the approach changed. ASP.NET Core still comes from **netstandard2.0 packages**, exactly as it did on Xamarin, because that is the only
route that works: the `Microsoft.AspNetCore.App` shared framework has **no runtime pack for android,
ios or maccatalyst**, so a framework reference compiles a library and then fails the consuming app
with `NETSDK1082`.

What changed is the version and the patching:

- The packages are the **2.3.x servicing line**, not 2.2.0, so there are no known advisories.
- `InplaceStringBuilder` is **fixed upstream** — `Microsoft.Net.Http.Headers` 2.3.11 no longer
  references it — so the forked Primitives is gone.
- The `CancelKeyPress` problem is **avoided by construction**: that call lives in `RunAsync`, and
  this starts and stops the host explicitly instead. The generic host's `ConsoleLifetime` is not
  involved either.

What is left is the part that was never the patch: starting and stopping the thing from app code,
and knowing what address to show the user.

If you are on `Xamarinme.WebHostPatch` 1.0.0, note that it pins ASP.NET Core 2.2.0, which carries a
critical advisory in `Microsoft.AspNetCore.Server.Kestrel.Core`
([GHSA-5rrx-jjjq-q2r5](https://github.com/advisories/GHSA-5rrx-jjjq-q2r5)) and a moderate one in
`Microsoft.AspNetCore.Server.IIS`
([GHSA-prrf-397v-83xh](https://github.com/advisories/GHSA-prrf-397v-83xh)). Its forked Primitives
also shadows the real assembly for anything in the same output directory.

## Licence

MIT.
