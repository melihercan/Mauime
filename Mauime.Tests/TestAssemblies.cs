using System.Reflection;

namespace Mauime.Tests;

/// <summary>
/// Locates built assemblies on disk so the public-API baseline can read them without referencing
/// them.
///
/// That indirection is not a style choice. The Mauime libraries multi-target the MAUI platform
/// frameworks — net10.0-android, net10.0-ios, net10.0-maccatalyst, net10.0-windows10.0.19041.0 —
/// and a plain net10.0 test project cannot reference any of them. Reading metadata is the only way
/// to cover the platform slices at all.
/// </summary>
internal static class TestAssemblies
{
    /// <summary>The imported Xamarinme assemblies, in baseline order.</summary>
    internal static readonly string[] LegacyNames =
    [
        "Xamarinme.Configuration",
        "Xamarinme.Hosting",
        "Xamarinme.WebHostPatch",
    ];

    /// <summary>The Mauime libraries, in baseline order.</summary>
    internal static readonly string[] LibraryNames =
    [
        "Mauime.Configuration",
        "Mauime.Hosting",
        "Mauime.Nfc",
        "Mauime.WebHostPatch",
    ];

    /// <summary>
    /// The framework the baseline renders a Mauime library from. The platform slices are held to
    /// this one by <see cref="MultiTargetingTests"/> rather than being dumped four more times.
    /// </summary>
    internal const string NeutralTargetFramework = "net10.0";

    /// <summary>Legacy assembly simple name to the project folder under <c>legacy/</c>.</summary>
    private static readonly Dictionary<string, string> LegacyProjects = new()
    {
        ["Xamarinme.Configuration"] = Path.Combine("legacy", "Configuration"),
        ["Xamarinme.Hosting"] = Path.Combine("legacy", "Hosting"),
        ["Xamarinme.WebHostPatch"] = Path.Combine("legacy", "WebHostPatch"),
    };

    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>The build configuration this test run was compiled in, e.g. "Debug".</summary>
    internal static string Configuration { get; } =
        typeof(TestAssemblies).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(TestAssemblies).Assembly.Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mauime.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate Mauime.slnx above the test assembly.");
    }

    /// <summary>Every assembly the baseline covers: the legacy three, then the Mauime four.</summary>
    internal static IEnumerable<(string Name, string Path)> Baseline() =>
        LegacyNames.Select(n => (n, LocateLegacyDll(n)))
            .Concat(LibraryNames.Select(n => (n, LocateDll(n, NeutralTargetFramework))));

    /// <summary>The target frameworks a Mauime library was actually built for, read from bin.</summary>
    internal static IReadOnlyList<string> BuiltTargetFrameworks(string assemblyName)
    {
        var bin = Path.Combine(RepositoryRoot, assemblyName, "bin", Configuration);

        if (!Directory.Exists(bin))
        {
            throw new DirectoryNotFoundException(
                $"'{bin}' does not exist. Build the whole solution first: dotnet build Mauime.slnx");
        }

        var frameworks = Directory.GetDirectories(bin)
            .Where(d => File.Exists(Path.Combine(d, assemblyName + ".dll")))
            .Select(d => Path.GetFileName(d)!)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        return frameworks.Count > 0
            ? frameworks
            : throw new FileNotFoundException($"No build of {assemblyName}.dll found under '{bin}'.");
    }

    /// <summary>One Mauime library, built for one specific target framework.</summary>
    internal static string LocateDll(string assemblyName, string targetFramework)
    {
        var path = Path.Combine(RepositoryRoot, assemblyName, "bin", Configuration, targetFramework,
            assemblyName + ".dll");

        return File.Exists(path)
            ? path
            : throw new FileNotFoundException(
                $"No {targetFramework} build of {assemblyName}.dll. Build the solution first.", path);
    }

    /// <summary>
    /// The newest build of a legacy assembly. Framework-agnostic, because those projects are plain
    /// netstandard2.0 and their bin layout is not worth pinning.
    /// </summary>
    internal static string LocateLegacyDll(string assemblyName)
    {
        var bin = Path.Combine(RepositoryRoot, LegacyProjects[assemblyName], "bin");

        if (!Directory.Exists(bin))
        {
            throw new FileNotFoundException(
                $"'{bin}' does not exist. Build the whole solution before running the API baseline: "
                + "dotnet build Mauime.slnx");
        }

        var configuration = Path.DirectorySeparatorChar + Configuration + Path.DirectorySeparatorChar;

        var candidates = Directory.GetFiles(bin, assemblyName + ".dll", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar))
            // Match the test run's own configuration first, so a stale Release build cannot shadow
            // a fresh Debug one just by having a newer timestamp.
            .OrderByDescending(p => p.Contains(configuration, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        return candidates.Count > 0
            ? candidates[0]
            : throw new FileNotFoundException($"No build of {assemblyName}.dll found under '{bin}'.");
    }

    /// <summary>
    /// The assemblies a metadata resolver should search when reading <paramref name="dllPath"/>.
    ///
    /// For a Mauime library this is exactly what the compiler was handed, read from the
    /// <c>Mauime.ReferencePaths.txt</c> that <c>Directory.Build.targets</c> writes beside the
    /// assembly. Nothing from the host runtime is mixed in: an android slice must resolve
    /// System.Runtime out of the Android ref pack, not out of the runtime this test happens to be
    /// running on, or the two worlds meet and types stop matching.
    ///
    /// For a legacy assembly there is no such file — legacy/ is shielded from the repository's
    /// build customisation on purpose — so the old bin-scan is used instead.
    /// </summary>
    internal static IEnumerable<string> ProbingFiles(string dllPath)
    {
        var directory = Path.GetDirectoryName(dllPath)!;
        var referencePaths = Path.Combine(directory, "Mauime.ReferencePaths.txt");

        if (File.Exists(referencePaths))
        {
            // The project directory: MSBuild writes some entries relative to it (the generated
            // Android resource designer, for one).
            var projectDirectory = Path.Combine(RepositoryRoot, Path.GetFileNameWithoutExtension(dllPath));

            var references = File.ReadAllLines(referencePaths)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => Path.IsPathRooted(line) ? line : Path.Combine(projectDirectory, line))
                .Where(File.Exists);

            return references.Concat(Directory.GetFiles(directory, "*.dll"));
        }

        return LegacyProbingFiles();
    }

    private static IEnumerable<string> LegacyProbingFiles()
    {
        var directories = new List<string>
        {
            Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            Path.GetDirectoryName(typeof(TestAssemblies).Assembly.Location)!,
        };

        foreach (var name in LegacyNames)
        {
            var bin = Path.Combine(RepositoryRoot, LegacyProjects[name], "bin");
            if (!Directory.Exists(bin)) continue;

            directories.AddRange(Directory.GetFiles(bin, name + ".dll", SearchOption.AllDirectories)
                .Select(dll => Path.GetDirectoryName(dll)!));
        }

        return directories.Distinct().SelectMany(d => Directory.GetFiles(d, "*.dll"));
    }
}
