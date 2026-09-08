using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Storage;

namespace Me.Toolkit.Maui.Configuration;

/// <summary>
/// Adds <c>appsettings.json</c> to a .NET MAUI app's configuration, from the app package or from an
/// assembly's embedded resources, with the usual <c>appsettings.{environment}.json</c> overlay.
/// </summary>
/// <remarks>
/// <para>
/// The parsing is <c>Microsoft.Extensions.Configuration.Json</c>'s. Xamarinme.Configuration vendored
/// a copy of Microsoft's Newtonsoft-era <c>JsonConfigurationFileParser</c> — the one Microsoft
/// itself replaced with a System.Text.Json implementation in .NET Core 3.0 — and froze it there,
/// dragging a Newtonsoft.Json dependency along with it. Nothing is vendored here.
///
/// The two parsers agree more closely than expected: both render a JSON <c>true</c> as
/// <c>"True"</c>. The one behavioural difference is a JSON <c>null</c>, which the old parser turned
/// into <c>""</c> and this one leaves absent.
/// </para>
/// <para>
/// MAUI does not reference <c>Microsoft.Extensions.Configuration.Json</c> itself, which is the one
/// thing this package adds that an app could not already do in a line.
/// </para>
/// </remarks>
public static class MeToolkitMauiConfigurationExtensions
{
    /// <summary>The conventional settings file name.</summary>
    public const string DefaultFileName = "appsettings.json";

    /// <summary>
    /// Adds a JSON settings file from the app package — a <c>MauiAsset</c> — and, when
    /// <paramref name="environment"/> is given, the matching
    /// <c>appsettings.{environment}.json</c> over the top of it.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="fileName">The settings file name, as declared to <c>MauiAsset</c>.</param>
    /// <param name="environment">
    /// The environment whose overlay file should be layered on top, or <see langword="null"/> for
    /// none. <c>IHostEnvironment.EnvironmentName</c> is the natural source.
    /// </param>
    /// <param name="optional">
    /// When <see langword="true"/>, a file that is not in the package is skipped. When
    /// <see langword="false"/>, a missing file throws.
    /// </param>
    /// <exception cref="FileNotFoundException">
    /// A required file is not in the app package.
    /// </exception>
    /// <remarks>
    /// This blocks on MAUI's asynchronous file API, because <see cref="IConfigurationBuilder"/> is
    /// synchronous and <c>CreateMauiApp</c> is too. The wait is marshalled through
    /// <c>Task.Run</c> so it cannot deadlock against the caller's
    /// synchronization context.
    ///
    /// Only meaningful on a real platform: on the platform-neutral <c>net10.0</c> slice MAUI's
    /// <c>FileSystem</c> is the reference-assembly stub and throws.
    /// </remarks>
    public static IConfigurationBuilder AddAppPackageJson(
        this IConfigurationBuilder builder,
        string fileName = DefaultFileName,
        string? environment = null,
        bool optional = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        return AddJsonStreams(builder, OpenAppPackageFile, fileName, environment, optional,
            "the app package");
    }

    /// <summary>
    /// Adds a JSON settings file embedded in <paramref name="assembly"/> and, when
    /// <paramref name="environment"/> is given, the matching
    /// <c>appsettings.{environment}.json</c> over the top of it.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="assembly">The assembly holding the embedded resources.</param>
    /// <param name="prefix">
    /// The manifest resource prefix: the default namespace, then any folders the file sits in, each
    /// separated by a dot. For <c>appsettings.json</c> in a <c>res</c> folder of an assembly whose
    /// default namespace is <c>MyApp</c>, that is <c>MyApp.res</c>.
    /// </param>
    /// <param name="fileName">The settings file name.</param>
    /// <param name="environment">The environment whose overlay file should be layered on top.</param>
    /// <param name="optional">Whether a missing resource is skipped rather than thrown for.</param>
    /// <exception cref="FileNotFoundException">A required resource is not in the assembly.</exception>
    /// <remarks>
    /// This is the path Xamarinme.Configuration offered, and it still works: embedded resources are
    /// not a Xamarin idea. It takes the assembly explicitly for the same reason the original did —
    /// finding it by reflection means enumerating every loaded assembly — but the options object
    /// that carried it is gone, along with the three ways it could be left half-filled and produce
    /// a <see cref="NullReferenceException"/> out of <c>Build()</c>.
    /// </remarks>
    public static IConfigurationBuilder AddEmbeddedResourceJson(
        this IConfigurationBuilder builder,
        Assembly assembly,
        string prefix,
        string fileName = DefaultFileName,
        string? environment = null,
        bool optional = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        return AddJsonStreams(builder, name => assembly.GetManifestResourceStream($"{prefix}.{name}"),
            fileName, environment, optional, $"'{assembly.GetName().Name}' under prefix '{prefix}'");
    }

    /// <summary>
    /// The overlay file name for an environment: <c>appsettings.json</c> and <c>Development</c>
    /// give <c>appsettings.Development.json</c>.
    /// </summary>
    public static string EnvironmentFileName(string fileName, string environment)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentException.ThrowIfNullOrEmpty(environment);

        var extension = Path.GetExtension(fileName);

        return $"{Path.GetFileNameWithoutExtension(fileName)}.{environment}{extension}";
    }

    private static IConfigurationBuilder AddJsonStreams(
        IConfigurationBuilder builder,
        Func<string, Stream?> open,
        string fileName,
        string? environment,
        bool optional,
        string source)
    {
        Add(fileName);

        if (!string.IsNullOrEmpty(environment))
        {
            Add(EnvironmentFileName(fileName, environment));
        }

        return builder;

        void Add(string name)
        {
            // Copied into memory so the resource or asset stream can be released now. The JSON
            // source holds whatever it is handed until the configuration is built, and does not
            // dispose it.
            using var opened = open(name);

            if (opened is null)
            {
                if (optional)
                {
                    return;
                }

                throw new FileNotFoundException($"'{name}' was not found in {source}.", name);
            }

            var copy = new MemoryStream();
            opened.CopyTo(copy);
            copy.Position = 0;

            builder.AddJsonStream(copy);
        }
    }

    private static Stream? OpenAppPackageFile(string fileName)
    {
        try
        {
            return Task.Run(() => FileSystem.OpenAppPackageFileAsync(fileName)).GetAwaiter().GetResult();
        }
        catch (FileNotFoundException)
        {
            // Android throws this for an asset that is not in the package; the caller decides
            // whether that is fatal.
            return null;
        }
    }
}
