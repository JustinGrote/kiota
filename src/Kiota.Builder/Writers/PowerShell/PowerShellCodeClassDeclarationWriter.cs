using System;
using System.Linq;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.PowerShell;

/// <summary>
/// Writes class declarations for:
/// <list type="bullet">
///   <item>Cmdlet wrapper classes (name ends with "Cmdlet") — adds [Cmdlet] attribute.</item>
///   <item>Static helper classes (KiotaPSCmdletBase / KiotaPSJob) — writes the full template.</item>
///   <item>All other classes — delegates to the CSharp base writer.</item>
/// </list>
/// </summary>
public class PowerShellCodeClassDeclarationWriter : CSharp.CodeClassDeclarationWriter
{
    public PowerShellCodeClassDeclarationWriter(PowerShellConventionService conventionService)
        : base(conventionService) { }

    public override void WriteCodeElement(ClassDeclaration codeElement, LanguageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(codeElement);
        ArgumentNullException.ThrowIfNull(writer);

        if (codeElement.Parent is not CodeClass parentClass)
            throw new InvalidOperationException($"ClassDeclaration {codeElement.Name} has no CodeClass parent");

        // Static helper classes — write the full template (no blocks opened so block end is a no-op)
        if (IsStaticHelper(parentClass))
        {
            WriteStaticHelperContent(parentClass, writer);
            return;
        }

        // Cmdlet wrapper class — add [Cmdlet] attribute before the standard class declaration
        if (IsCmdletClass(parentClass))
        {
            WriteCmdletClassDeclaration(codeElement, parentClass, writer);
            return;
        }

        // All other classes — use inherited CSharp behaviour
        base.WriteCodeElement(codeElement, writer);
    }

    internal static bool IsStaticHelper(CodeClass codeClass) =>
        codeClass.Documentation.DescriptionTemplate?.StartsWith(
            PowerShellInfrastructureTemplates.StaticTemplateMarker,
            StringComparison.Ordinal) ?? false;

    internal static bool IsCmdletClass(CodeClass codeClass) =>
        codeClass.Kind == CodeClassKind.Custom &&
        codeClass.Name.EndsWith("Cmdlet", StringComparison.Ordinal);

    // -----------------------------------------------------------------------
    // Static helper writer
    // -----------------------------------------------------------------------

    private static void WriteStaticHelperContent(CodeClass parentClass, LanguageWriter writer)
    {
        var ns = (parentClass.Parent as CodeNamespace)?.Name ?? string.Empty;
        string content;
        if (parentClass.Name.Equals(PowerShellInfrastructureTemplates.CmdletBaseClassName, StringComparison.Ordinal))
            content = PowerShellInfrastructureTemplates.GetCmdletBaseTemplate(ns);
        else if (parentClass.Name.Equals(PowerShellInfrastructureTemplates.JobClassName, StringComparison.Ordinal))
            content = PowerShellInfrastructureTemplates.GetJobTemplate(ns);
        else
            return;

        // Write the full template without using StartBlock/CloseBlock so that the
        // LanguageWriter indent counter stays at 0.  The PowerShellCodeBlockEndWriter
        // detects static helpers and skips its own close-braces.
        foreach (var line in content.Split('\n'))
            writer.WriteLine(line.TrimEnd('\r'), false);
    }

    // -----------------------------------------------------------------------
    // Cmdlet class writer
    // -----------------------------------------------------------------------

    private void WriteCmdletClassDeclaration(ClassDeclaration codeElement, CodeClass parentClass, LanguageWriter writer)
    {
        // Standard CSharp preamble (auto-generated header + usings + namespace)
        if (codeElement.Parent?.Parent is CodeNamespace codeNamespace)
        {
            writer.WriteLine(AutoGenerationHeader);
            WritePragmaDisable(writer);
            WriteUsings(codeElement, writer);
            writer.WriteLine($"namespace {codeNamespace.Name}");
            writer.StartBlock();
        }

        // [Cmdlet] attribute
        var (verb, noun) = PowerShellConventionService.ParseCmdletName(parentClass.Name);
        var verbsConstant = PowerShellConventionService.GetVerbsConstant(verb);
        writer.WriteLine($"[global::System.CodeDom.Compiler.GeneratedCode(\"Kiota\", \"{Kiota.Generated.KiotaVersion.CurrentMajor()}\")]");
        writer.WriteLine($"[Cmdlet({verbsConstant}, \"{noun}\")]");

        // [OutputType] attribute if there is a non-void return type
        var executeMethod = parentClass.Methods.FirstOrDefault(
            static m => m.Name.Equals("ExecuteAsync", StringComparison.Ordinal));
        if (executeMethod?.ReturnType is CodeType retType &&
            !retType.Name.Equals("void", StringComparison.OrdinalIgnoreCase) &&
            !retType.Name.Equals("object", StringComparison.OrdinalIgnoreCase))
        {
            var typeName = ((PowerShellConventionService)conventions).GetTypeString(retType, executeMethod);
            writer.WriteLine($"[OutputType(typeof({typeName}))]");
        }

        // Inheritance
        var baseTypes = (codeElement.Inherits is null
            ? Enumerable.Empty<string>()
            : new[] { ((PowerShellConventionService)conventions).GetTypeString(codeElement.Inherits, parentClass) })
            .Union(codeElement.Implements.Select(static x => x.Name))
            .ToArray();
        var derivation = baseTypes.Length != 0 ? ": " + string.Join(", ", baseTypes) : string.Empty;

        // Documentation comment
        conventions.WriteLongDescription(parentClass, writer);

        // Class declaration line
        writer.WriteLine($"{conventions.GetAccessModifier(parentClass.Access)} class {codeElement.Name.ToFirstCharacterUpperCase()} {derivation}");
        if (!HasDescription(parentClass)) WritePragmaRestoreCs1591(writer);
        writer.StartBlock();
    }

    private static bool HasDescription(CodeClass cls) =>
        cls.Documentation?.DescriptionAvailable ?? false;

    private static void WritePragmaDisable(LanguageWriter writer) =>
        writer.WriteLine($"#pragma warning disable {CSharp.CSharpConventionService.CS0618}", false);

    private static void WritePragmaRestoreCs1591(LanguageWriter writer) =>
        writer.WriteLine($"#pragma warning restore {CSharp.CSharpConventionService.CS1591}", false);

    private static void WriteUsings(ClassDeclaration codeElement, LanguageWriter writer)
    {
        codeElement.Usings
            .Where(x => (x.Declaration?.IsExternal ?? true) ||
                        !x.Declaration.Name.Equals(codeElement.Name, StringComparison.OrdinalIgnoreCase))
            .Select(static x => x.Declaration?.IsExternal ?? false
                ? $"using {x.Declaration.Name.NormalizeNameSpaceName(".")};"
                : $"using {x.Name.NormalizeNameSpaceName(".")};")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToList()
            .ForEach(x => writer.WriteLine(x));
    }
}
