using System;
using System.IO;
using System.Linq;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Writers;
using Kiota.Builder.Writers.PowerShell;
using Xunit;

namespace Kiota.Builder.Tests.Writers.PowerShell;

/// <summary>
/// Tests for <see cref="PowerShellCodeBlockEndWriter"/>.
/// Covers:
///   - Block-end is silently skipped for static helper classes.
///   - Block-end writes the expected closing braces for cmdlet classes (namespace + class).
///   - Block-end writes the expected closing braces for regular CSharp classes.
/// </summary>
public sealed class CodeClassEndWriterTests : IDisposable
{
    private const string DefaultPath = "./";
    private const string DefaultName = "name";
    private readonly StringWriter tw;
    private readonly LanguageWriter writer;
    private readonly PowerShellCodeBlockEndWriter endWriter;

    public CodeClassEndWriterTests()
    {
        var conventions = new PowerShellConventionService();
        endWriter = new PowerShellCodeBlockEndWriter(conventions);
        writer = LanguageWriter.GetLanguageWriter(GenerationLanguage.PowerShell, DefaultPath, DefaultName);
        tw = new StringWriter();
        writer.SetTextWriter(tw);
    }

    public void Dispose()
    {
        tw?.Dispose();
        GC.SuppressFinalize(this);
    }

    // -----------------------------------------------------------------------
    // Static helpers — block end is a no-op
    // -----------------------------------------------------------------------

    [Fact]
    public void SkipsBlockEndForKiotaPSCmdletBase()
    {
        var helperClass = CreateStaticHelperClass(PowerShellInfrastructureTemplates.CmdletBaseClassName);

        endWriter.WriteCodeElement(helperClass.EndBlock, writer);
        var result = tw.ToString();

        Assert.Empty(result.Trim());
    }

    [Fact]
    public void SkipsBlockEndForKiotaPSJob()
    {
        var helperClass = CreateStaticHelperClass(PowerShellInfrastructureTemplates.JobClassName);

        endWriter.WriteCodeElement(helperClass.EndBlock, writer);
        var result = tw.ToString();

        Assert.Empty(result.Trim());
    }

    // -----------------------------------------------------------------------
    // Non-static classes — closing braces are written
    // -----------------------------------------------------------------------

    [Fact]
    public void WritesClosingBraceForTopLevelClass()
    {
        var root = CodeNamespace.InitRootNamespace();
        var ns = root.AddNamespace("TestNs");
        var regularClass = ns.AddClass(new CodeClass
        {
            Name = "SomeModel",
            Kind = CodeClassKind.Model,
        }).First();

        // A class whose parent is a namespace gets TWO closing braces (class + namespace)
        endWriter.WriteCodeElement(regularClass.EndBlock, writer);
        var result = tw.ToString();

        Assert.Equal(2, CountChar(result, '}'));
    }

    [Fact]
    public void WritesOneClosingBraceForNestedClass()
    {
        var root = CodeNamespace.InitRootNamespace();
        var ns = root.AddNamespace("TestNs");
        var outerClass = ns.AddClass(new CodeClass { Name = "Outer", Kind = CodeClassKind.Custom }).First();
        var innerClass = outerClass.AddInnerClass(new CodeClass { Name = "Inner", Kind = CodeClassKind.Custom }).First();

        endWriter.WriteCodeElement(innerClass.EndBlock, writer);
        var result = tw.ToString();

        // Nested class → only 1 closing brace (no extra namespace brace)
        Assert.Equal(1, CountChar(result, '}'));
    }

    [Fact]
    public void WritesCs0618PragmaRestoreForTopLevelClass()
    {
        var root = CodeNamespace.InitRootNamespace();
        var ns = root.AddNamespace("TestNs");
        var cls = ns.AddClass(new CodeClass { Name = "AClass", Kind = CodeClassKind.Custom }).First();

        endWriter.WriteCodeElement(cls.EndBlock, writer);
        var result = tw.ToString();

        Assert.Contains("#pragma warning restore CS0618", result);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static CodeClass CreateStaticHelperClass(string className)
    {
        var root = CodeNamespace.InitRootNamespace();
        var ns = root.AddNamespace("MyClient.Cmdlets");
        return ns.AddClass(new CodeClass
        {
            Name = className,
            Kind = CodeClassKind.Custom,
            Documentation = new CodeDocumentation
            {
                DescriptionTemplate = PowerShellInfrastructureTemplates.StaticTemplateMarker,
            },
        }).First();
    }

    private static int CountChar(string s, char c)
    {
        var count = 0;
        foreach (var ch in s)
            if (ch == c) count++;
        return count;
    }
}
