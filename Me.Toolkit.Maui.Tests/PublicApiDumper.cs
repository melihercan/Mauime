using System.Globalization;
using System.Reflection;
using System.Text;

namespace Me.Toolkit.Maui.Tests;

/// <summary>
/// Renders the public surface of an assembly as deterministic text.
///
/// Reads metadata only, via <see cref="MetadataLoadContext"/>, so it works on assemblies this test
/// project cannot reference. That is the whole point here: the libraries target MAUI
/// platform frameworks (net10.0-android and friends), which a net10.0 test project cannot
/// reference at all.
/// </summary>
internal static class PublicApiDumper
{
    /// <summary>Dumps several assemblies, each rendered under its own heading.</summary>
    internal static string Dump(IEnumerable<(string Name, string Path)> assemblies)
    {
        var sb = new StringBuilder();

        foreach (var (name, path) in assemblies)
        {
            sb.Append(Dump(name, path));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Dumps one assembly, in a <see cref="MetadataLoadContext"/> of its own.
    ///
    /// One context per assembly, not one for all of them: the platform slices of a single library
    /// are compiled against different worlds, and an android System.Runtime and an ios System.Runtime
    /// cannot share a resolver that matches on simple name. Mixing them silently resolves types out
    /// of whichever pack happened to be enumerated first.
    /// </summary>
    internal static string Dump(string name, string path)
    {
        using var context = new MetadataLoadContext(
            new SimpleNameResolver(TestAssemblies.ProbingFiles(path)));

        var assembly = context.LoadFromAssemblyPath(path);
        var sb = new StringBuilder();

        sb.Append("assembly ").Append(name).AppendLine();

        foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            AppendType(sb, type);
        }

        return sb.AppendLine().ToString();
    }

    private static void AppendType(StringBuilder sb, Type type)
    {
        sb.Append("  type ").Append(Render(type)).Append(" : ").Append(Kind(type)).AppendLine();

        if (type.IsEnum)
        {
            // Numeric values are part of the contract: callers may have persisted them as ints.
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
                         .OrderBy(f => Convert.ToInt64(f.GetRawConstantValue(), CultureInfo.InvariantCulture)))
            {
                sb.Append("    ").Append(field.Name).Append(" = ")
                  .Append(Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture))
                  .AppendLine();
            }

            return;
        }

        foreach (var member in Members(type).OrderBy(RenderMember, StringComparer.Ordinal))
        {
            sb.Append("    ").Append(RenderMember(member)).AppendLine();
        }
    }

    private static IEnumerable<MemberInfo> Members(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var m in type.GetMembers(flags))
        {
            if (m is MethodInfo { IsSpecialName: true }) continue;   // property/event accessors
            if (m is Type) continue;                                  // nested types come from GetExportedTypes
            if (Visible(m)) yield return m;
        }
    }

    private static bool Visible(MemberInfo member) => member switch
    {
        FieldInfo f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
        MethodBase m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly,
        PropertyInfo p => Visible(p.GetMethod ?? (MemberInfo)p.SetMethod!),
        EventInfo e => Visible(e.AddMethod!),
        _ => false,
    };

    private static string RenderMember(MemberInfo member) => member switch
    {
        FieldInfo f when f.IsLiteral =>
            $"const {Render(f.FieldType)} {f.Name} = {Literal(f.GetRawConstantValue())}",
        FieldInfo f =>
            $"field {(f.IsStatic ? "static " : "")}{Render(f.FieldType)} {f.Name}",
        ConstructorInfo c =>
            $"ctor .ctor({Parameters(c)})",
        MethodInfo m =>
            $"method {(m.IsStatic ? "static " : "")}{(m.IsVirtual && !m.IsFinal && !m.DeclaringType!.IsInterface ? "virtual " : "")}"
            + $"{Render(m.ReturnType)} {m.Name}{GenericParameters(m)}({Parameters(m)})",
        // static is rendered here as well as on methods and fields: moving a member between static
        // and instance changes the call site, so the baseline has to see it.
        PropertyInfo p =>
            $"property {((p.GetMethod ?? p.SetMethod)!.IsStatic ? "static " : "")}"
            + $"{Render(p.PropertyType)} {p.Name} {{ {(p.GetMethod is not null && Visible(p.GetMethod) ? "get; " : "")}"
            + $"{(p.SetMethod is not null && Visible(p.SetMethod) ? "set; " : "")}}}",
        EventInfo e =>
            $"event {Render(e.EventHandlerType!)} {e.Name}",
        _ => member.ToString()!,
    };

    /// <summary>Arity is part of the signature, so generic parameters have to be rendered.</summary>
    private static string GenericParameters(MethodBase method) =>
        method.IsGenericMethodDefinition
            ? "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">"
            : string.Empty;

    private static string Parameters(MethodBase method) =>
        string.Join(", ", method.GetParameters().Select(p =>
            $"{Render(p.ParameterType)} {p.Name}"
            + (p.HasDefaultValue ? $" = {Literal(p.RawDefaultValue)}" : string.Empty)));

    private static string Literal(object? value) => value switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        bool b => b ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
    };

    private static string Kind(Type type) =>
        type.IsEnum ? "enum " + Render(type.GetEnumUnderlyingType())
        : type.IsInterface ? "interface"
        : type.IsValueType ? "struct"
        : (type.IsAbstract && type.IsSealed) ? "static class"
        : $"{(type.IsAbstract ? "abstract " : "")}{(type.IsSealed ? "sealed " : "")}class";

    private static string Render(Type type)
    {
        if (type.IsArray) return Render(type.GetElementType()!) + "[]";
        if (type.IsByRef) return "ref " + Render(type.GetElementType()!);
        if (!type.IsGenericType) return type.FullName ?? type.Name;

        var name = (type.GetGenericTypeDefinition().FullName ?? type.Name);
        name = name[..name.IndexOf('`')];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Render))}>";
    }
}

/// <summary>
/// Resolves dependencies by simple name only, ignoring the requested version.
///
/// <see cref="PathAssemblyResolver"/> matches on version too, which is more precision than a
/// metadata dump needs and more than the MAUI workloads reliably offer: a reference assembly and the
/// runtime assembly it stands in for do not always agree, and rendering a type name does not depend
/// on telling them apart.
///
/// First path wins per name. <see cref="TestAssemblies.ProbingFiles"/> supplies exactly what the
/// compiler was handed for that one slice, so there is no second candidate to get wrong.
/// </summary>
internal sealed class SimpleNameResolver : MetadataAssemblyResolver
{
    private readonly Dictionary<string, string> _byName;

    internal SimpleNameResolver(IEnumerable<string> assemblyPaths)
    {
        _byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in assemblyPaths)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!_byName.ContainsKey(name))
            {
                _byName[name] = path;
            }
        }
    }

    public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName) =>
        assemblyName.Name is { } name && _byName.TryGetValue(name, out var path)
            ? context.LoadFromAssemblyPath(path)
            : null;
}
