using System;
using System.IO;
using System.Linq;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Writers;
using Kiota.Builder.Writers.PowerShell;
using Xunit;

namespace Kiota.Builder.Tests.Writers.PowerShell;

/// <summary>
/// Tests for <see cref="PowerShellCodeMethodWriter"/>.
/// Covers:
///   - ExecuteAsync methods inside cmdlet classes get the PS scaffold body.
///   - Methods on static helper classes are silently skipped (content written by declaration writer).
///   - Other method kinds (non-ExecuteAsync) on cmdlet classes fall through to CSharp base.
///   - The scaffold body includes the NotImplementedException.
///   - Path-param properties are referenced in the scaffold comment.
/// </summary>
public sealed class CodeMethodWriterTests : IDisposable
{
    private const string DefaultPath = "./";
    private const string DefaultName = "name";
    private readonly StringWriter tw;
    private readonly LanguageWriter writer;
    private readonly PowerShellCodeMethodWriter methodWriter;

    public CodeMethodWriterTests()
    {
        var conventions = new PowerShellConventionService();
        methodWriter = new PowerShellCodeMethodWriter(conventions);
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
    // ExecuteAsync on a cmdlet class
    // -----------------------------------------------------------------------

    [Fact]
    public void WritesExecuteAsyncMethodSignature()
    {
        var (_, method) = SetupExecuteAsyncMethod("GetUserCmdlet");

        methodWriter.WriteCodeElement(method, writer);
        var result = tw.ToString();

        Assert.Contains("protected override async", result);
        Assert.Contains("ExecuteAsync", result);
        Assert.Contains("CancellationToken cancellationToken", result);
    }

    [Fact]
    public void WritesNotImplementedExceptionInScaffold()
    {
        var (_, method) = SetupExecuteAsyncMethod("NewItemCmdlet");

        methodWriter.WriteCodeElement(method, writer);
        var result = tw.ToString();

        Assert.Contains("NotImplementedException", result);
    }

    [Fact]
    public void WritesTodoCommentInScaffold()
    {
        var (_, method) = SetupExecuteAsyncMethod("GetDocumentCmdlet");

        methodWriter.WriteCodeElement(method, writer);
        var result = tw.ToString();

        Assert.Contains("// TODO:", result);
    }

    [Fact]
    public void WritesExamplePatternCommentInScaffold()
    {
        var (_, method) = SetupExecuteAsyncMethod("GetFileCmdlet");

        methodWriter.WriteCodeElement(method, writer);
        var result = tw.ToString();

        Assert.Contains("// Example pattern:", result);
    }

    [Fact]
    public void WritesPathParamInScaffoldComment()
    {
        var root = CodeNamespace.InitRootNamespace();
        var ns = root.AddNamespace("TestNs");
        var cmdletClass = ns.AddClass(new CodeClass
        {
            Name = "GetUserCmdlet",
            Kind = CodeClassKind.Custom,
        }).First();
        cmdletClass.AddProperty(new CodeProperty
        {
            Name = "UserId",
            Kind = CodePropertyKind.Custom,
            Type = new CodeType { Name = "string", IsExternal = true },
        });
        var method = cmdletClass.AddMethod(new CodeMethod
        {
            Name = "ExecuteAsync",
            Kind = CodeMethodKind.Custom,
            IsAsync = true,
            Access = AccessModifier.Protected,
            ReturnType = new CodeType { Name = "object", IsNullable = true },
        }).First();
        method.AddParameter(new CodeParameter
        {
            Name = "cancellationToken",
            Kind = CodeParameterKind.Cancellation,
            Optional = false,
            Type = new CodeType { Name = "CancellationToken", IsExternal = true },
        });

        methodWriter.WriteCodeElement(method, writer);
        var result = tw.ToString();

        // Path param name should appear in the scaffold comment
        Assert.Contains("UserId", result);
    }

    // -----------------------------------------------------------------------
    // Static helper classes — methods are skipped
    // -----------------------------------------------------------------------

    [Fact]
    public void SkipsMethodsOnStaticHelperClasses()
    {
        var root = CodeNamespace.InitRootNamespace();
        var ns = root.AddNamespace("MyClient.Cmdlets");
        var helperClass = ns.AddClass(new CodeClass
        {
            Name = PowerShellInfrastructureTemplates.CmdletBaseClassName,
            Kind = CodeClassKind.Custom,
            Documentation = new CodeDocumentation
            {
                DescriptionTemplate = PowerShellInfrastructureTemplates.StaticTemplateMarker,
            },
        }).First();
        var method = helperClass.AddMethod(new CodeMethod
        {
            Name = "SomeMethod",
            Kind = CodeMethodKind.Custom,
            ReturnType = new CodeType { Name = "void" },
        }).First();

        methodWriter.WriteCodeElement(method, writer);
        var result = tw.ToString();

        Assert.Empty(result.Trim());
    }

    // -----------------------------------------------------------------------
    // Non-ExecuteAsync methods on cmdlet classes fall through to CSharp base
    // -----------------------------------------------------------------------

    [Fact]
    public void DelegatesNonExecuteAsyncMethodOnCmdletClassToCSharpBase()
    {
        var root = CodeNamespace.InitRootNamespace();
        var ns = root.AddNamespace("TestNs");
        var cmdletClass = ns.AddClass(new CodeClass
        {
            Name = "GetUserCmdlet",
            Kind = CodeClassKind.Custom,
        }).First();
        // A plain Custom method (not ExecuteAsync)
        var method = cmdletClass.AddMethod(new CodeMethod
        {
            Name = "HelperMethod",
            Kind = CodeMethodKind.Custom,
            ReturnType = new CodeType { Name = "void" },
        }).First();

        // Should not throw; CSharp base handles it
        methodWriter.WriteCodeElement(method, writer);
        // The CSharp base writes "return null;" for default methods
        var result = tw.ToString();
        Assert.Contains("return null;", result);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (CodeClass cmdletClass, CodeMethod method) SetupExecuteAsyncMethod(string cmdletName)
    {
        var root = CodeNamespace.InitRootNamespace();
        var ns = root.AddNamespace("TestNs");
        var cmdletClass = ns.AddClass(new CodeClass
        {
            Name = cmdletName,
            Kind = CodeClassKind.Custom,
        }).First();
        var method = cmdletClass.AddMethod(new CodeMethod
        {
            Name = "ExecuteAsync",
            Kind = CodeMethodKind.Custom,
            IsAsync = true,
            Access = AccessModifier.Protected,
            ReturnType = new CodeType { Name = "object", IsNullable = true },
        }).First();
        method.AddParameter(new CodeParameter
        {
            Name = "cancellationToken",
            Kind = CodeParameterKind.Cancellation,
            Optional = false,
            Type = new CodeType { Name = "CancellationToken", IsExternal = true },
        });
        return (cmdletClass, method);
    }
}
