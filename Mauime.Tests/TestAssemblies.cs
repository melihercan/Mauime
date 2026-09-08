using System.Reflection;

namespace Mauime.Tests;

/// <summary>
/// Locates the built legacy assemblies on disk so the public-API baseline can read them without
/// referencing them.
///
/// Reading metadata rather than referencing matters more here than it did in Blazorme: the ported
/// libraries will target MAUI platform frameworks (net10.0-android and friends), which a plain
/// net10.0 test project cannot reference at all. Establishing the machinery now means the baseline
/// survives the port.
/// </summary>
internal static class TestAssemblies
{
    /// <summary>Assembly simple names, in the order they appear in the approved baseline.</summary>
    internal static readonly string[] Names =
    [
        "Xamarinme.Configuration",
        "Xamarinme.Hosting",
        "Xamarinme.WebHostPatch",
    ];

    /// <summary>Assembly simple name to the project folder under <c>legacy/</c> that builds it.</summary>
    private static readonly Dictionary<string, string> Projects = new()
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

    /// <summary>
    /// The newest build of <paramref name="assemblyName"/> found under its project's bin folder.
    /// Deliberately framework-agnostic, so the same lookup keeps working when a library moves from
    /// netstandard2.0 to the MAUI target frameworks.
    /// </summary>
    internal static string LocateDll(string assemblyName)
    {
        var bin = Path.Combine(RepositoryRoot, Projects[assemblyName], "bin");

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

    /// <summary>Every directory a metadata resolver should search for dependency assemblies.</summary>
    internal static IEnumerable<string> ProbingDirectories()
    {
        yield return Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        yield return Path.GetDirectoryName(typeof(TestAssemblies).Assembly.Location)!;

        foreach (var name in Names)
        {
            var bin = Path.Combine(RepositoryRoot, Projects[name], "bin");
            if (!Directory.Exists(bin)) continue;

            foreach (var dll in Directory.GetFiles(bin, name + ".dll", SearchOption.AllDirectories))
            {
                yield return Path.GetDirectoryName(dll)!;
            }
        }
    }
}
