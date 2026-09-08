using Microsoft.Extensions.Configuration;
using ReactiveUI;

namespace DemoApp.ViewModels;

/// <summary>
/// Every key the configuration ended up with, which is how you can see the environment overlay
/// having been applied: <c>Demo:Source</c> comes from appsettings.Development.json, over the top of
/// the base file's value.
/// </summary>
public sealed class ConfigurationViewModel : ReactiveObject
{
    public ConfigurationViewModel(IConfiguration configuration)
    {
        Settings =
        [
            .. configuration.AsEnumerable()
                .Where(setting => setting.Value is not null)
                .OrderBy(setting => setting.Key, StringComparer.Ordinal)
                .Select(setting => new SettingRow(setting.Key, setting.Value!)),
        ];
    }

    public IReadOnlyList<SettingRow> Settings { get; }
}

public sealed record SettingRow(string Key, string Value);
