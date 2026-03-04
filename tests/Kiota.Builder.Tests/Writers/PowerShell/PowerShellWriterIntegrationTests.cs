using System;
using System.IO;
using System.Linq;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Writers;
using Kiota.Builder.Writers.PowerShell;
using Xunit;

namespace Kiota.Builder.Tests.Writers.PowerShell;

/// <summary>
/// Integration-style tests that exercise the full <see cref="PowerShellWriter"/> pipeline.
/// Each test sets up a CodeDOM graph, calls <c>writer.Write(element)</c>, and asserts the
/// resulting text contains the expected PowerShell C# constructs.
/// </summary>
public sealed class PowerShellWriterIntegrationTests : IDisposable
{
    private const string DefaultPath = "./";
    private const string DefaultName = "name";
    private readonly StringWriter tw;
    private readonly LanguageWriter writer;

    public PowerShellWriterIntegrationTests()
    {
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
    // Infrastructure classes
    // -----------------------------------------------------------------------

    [Fact]
    public void WritesKiotaPSCmdletBaseInfrastructureClass()
    {
        var ns = SetupNamespace("MyClient.Cmdlets");
        var helperClass = CreateStaticHelperClass(ns, PowerShellInfrastructureTemplates.CmdletBaseClassName);

        writer.Write(helperClass.StartBlock);
        var result = tw.ToString();

        Assert.Contains("abstract class KiotaPSCmdletBase", result);
        Assert.Contains(": PSCmdlet", result);
        Assert.Contains("public SwitchParameter AsJob", result);
        Assert.Contains("protected override void ProcessRecord()", result);
        Assert.Contains("protected abstract Task<object?> ExecuteAsync(CancellationToken cancellationToken)", result);
    }

    [Fact]
    public void WritesKiotaPSJobInfrastructureClass()
    {
        var ns = SetupNamespace("MyClient.Cmdlets");
        var jobClass = CreateStaticHelperClass(ns, PowerShellInfrastructureTemplates.JobClassName);

        writer.Write(jobClass.StartBlock);
        var result = tw.ToString();

        Assert.Contains("sealed class KiotaPSJob", result);
        Assert.Contains(": Job2", result);
        Assert.Contains("public override void StopJob()", result);
        Assert.Contains("public override void ResumeJob()", result);
        Assert.Contains("public override void SuspendJob()", result);
        Assert.Contains("public override void UnblockJob()", result);
        Assert.Contains("public override string StatusMessage", result);
        Assert.Contains("public override bool HasMoreData", result);
        Assert.Contains("public override string Location", result);
    }

    [Fact]
    public void KiotaPSCmdletBaseContainsAsJobHandlingLogic()
    {
        var ns = SetupNamespace("MyClient.Cmdlets");
        var helperClass = CreateStaticHelperClass(ns, PowerShellInfrastructureTemplates.CmdletBaseClassName);

        writer.Write(helperClass.StartBlock);
        var result = tw.ToString();

        // AsJob branch — creates KiotaPSJob
        Assert.Contains("AsJob.IsPresent", result);
        Assert.Contains("new KiotaPSJob", result);
        Assert.Contains("JobRepository.Add", result);
        // Synchronous branch
        Assert.Contains("GetAwaiter().GetResult()", result);
        Assert.Contains("WriteObject(result, true)", result);
    }

    [Fact]
    public void KiotaPSJobContainsCompletionCallbackLogic()
    {
        var ns = SetupNamespace("MyClient.Cmdlets");
        var jobClass = CreateStaticHelperClass(ns, PowerShellInfrastructureTemplates.JobClassName);

        writer.Write(jobClass.StartBlock);
        var result = tw.ToString();

        Assert.Contains("IsFaulted", result);
        Assert.Contains("IsCanceled", result);
        Assert.Contains("JobState.Completed", result);
        Assert.Contains("JobState.Failed", result);
        Assert.Contains("JobState.Stopped", result);
        Assert.Contains("JobState.Running", result);
    }

    // -----------------------------------------------------------------------
    // Complete cmdlet class generation
    // -----------------------------------------------------------------------

    [Fact]
    public void WritesFullGetCmdletClass()
    {
        var ns = SetupNamespace("MyClient.Cmdlets");
        var cmdletClass = ns.AddClass(new CodeClass
        {
            Name = "GetUserCmdlet",
            Kind = CodeClassKind.Custom,
        }).First();
        cmdletClass.StartBlock.Inherits = new CodeType
        {
            Name = "KiotaPSCmdletBase",
        };
        cmdletClass.AddProperty(new CodeProperty
        {
            Name = "UserId",
            Kind = CodePropertyKind.Custom,
            Type = new CodeType { Name = "string", IsExternal = true, IsNullable = false },
        });
        cmdletClass.AddMethod(new CodeMethod
        {
            Name = "ExecuteAsync",
            Kind = CodeMethodKind.Custom,
            IsAsync = true,
            Access = AccessModifier.Protected,
            ReturnType = new CodeType { Name = "object", IsNullable = true },
        }).First().AddParameter(new CodeParameter
        {
            Name = "cancellationToken",
            Kind = CodeParameterKind.Cancellation,
            Optional = false,
            Type = new CodeType { Name = "CancellationToken", IsExternal = true },
        });

        WriteClass(cmdletClass);
        var result = tw.ToString();

        // Class declaration
        Assert.Contains("[Cmdlet(VerbsCommon.Get, \"User\")]", result);
        Assert.Contains("public class GetUserCmdlet", result);
        Assert.Contains(": KiotaPSCmdletBase", result);
        // Property
        Assert.Contains("[Parameter(Mandatory = true)]", result);
        Assert.Contains("UserId", result);
        // ExecuteAsync method
        Assert.Contains("protected override async", result);
        Assert.Contains("ExecuteAsync", result);
        Assert.Contains("NotImplementedException", result);
        // Closing brace
        Assert.Contains("}", result);
    }

    [Fact]
    public void WritesFullSetCmdletClassWithPipelineInput()
    {
        var ns = SetupNamespace("MyClient.Cmdlets");
        var cmdletClass = ns.AddClass(new CodeClass
        {
            Name = "SetUserCmdlet",
            Kind = CodeClassKind.Custom,
        }).First();
        cmdletClass.StartBlock.Inherits = new CodeType { Name = "KiotaPSCmdletBase" };
        cmdletClass.AddProperty(new CodeProperty
        {
            Name = "Body",
            Kind = CodePropertyKind.RequestBody,
            Type = new CodeType { Name = "UserModel", IsExternal = true },
        });
        cmdletClass.AddMethod(new CodeMethod
        {
            Name = "ExecuteAsync",
            Kind = CodeMethodKind.Custom,
            IsAsync = true,
            Access = AccessModifier.Protected,
            ReturnType = new CodeType { Name = "object", IsNullable = true },
        }).First().AddParameter(new CodeParameter
        {
            Name = "cancellationToken",
            Kind = CodeParameterKind.Cancellation,
            Optional = false,
            Type = new CodeType { Name = "CancellationToken", IsExternal = true },
        });

        WriteClass(cmdletClass);
        var result = tw.ToString();

        Assert.Contains("[Cmdlet(VerbsCommon.Set, \"User\")]", result);
        Assert.Contains("ValueFromPipeline = true", result);
    }

    [Fact]
    public void WritesFullRemoveCmdletClass()
    {
        var ns = SetupNamespace("MyClient.Cmdlets");
        var cmdletClass = ns.AddClass(new CodeClass
        {
            Name = "RemoveFileCmdlet",
            Kind = CodeClassKind.Custom,
        }).First();
        cmdletClass.AddMethod(new CodeMethod
        {
            Name = "ExecuteAsync",
            Kind = CodeMethodKind.Custom,
            IsAsync = true,
            Access = AccessModifier.Protected,
            ReturnType = new CodeType { Name = "object", IsNullable = true },
        }).First().AddParameter(new CodeParameter
        {
            Name = "cancellationToken",
            Kind = CodeParameterKind.Cancellation,
            Optional = false,
            Type = new CodeType { Name = "CancellationToken", IsExternal = true },
        });

        WriteClass(cmdletClass);
        var result = tw.ToString();

        Assert.Contains("[Cmdlet(VerbsCommon.Remove, \"File\")]", result);
    }

    // -----------------------------------------------------------------------
    // Infrastructure template content assertions
    // -----------------------------------------------------------------------

    [Fact]
    public void CmdletBaseTemplateContainsAutoGeneratedComment()
    {
        var template = PowerShellInfrastructureTemplates.GetCmdletBaseTemplate("My.Module.Cmdlets");
        Assert.Contains("// <auto-generated/>", template);
    }

    [Fact]
    public void CmdletBaseTemplateUsesProvidedNamespace()
    {
        var template = PowerShellInfrastructureTemplates.GetCmdletBaseTemplate("Custom.NS");
        Assert.Contains("namespace Custom.NS", template);
    }

    [Fact]
    public void JobTemplateContainsAutoGeneratedComment()
    {
        var template = PowerShellInfrastructureTemplates.GetJobTemplate("My.Module.Cmdlets");
        Assert.Contains("// <auto-generated/>", template);
    }

    [Fact]
    public void JobTemplateUsesProvidedNamespace()
    {
        var template = PowerShellInfrastructureTemplates.GetJobTemplate("Custom.NS");
        Assert.Contains("namespace Custom.NS", template);
    }

    [Fact]
    public void CmdletBaseTemplateContainsGeneratedCodeAttribute()
    {
        var template = PowerShellInfrastructureTemplates.GetCmdletBaseTemplate("NS");
        Assert.Contains("[global::System.CodeDom.Compiler.GeneratedCode(", template);
    }

    [Fact]
    public void JobTemplateContainsGeneratedCodeAttribute()
    {
        var template = PowerShellInfrastructureTemplates.GetJobTemplate("NS");
        Assert.Contains("[global::System.CodeDom.Compiler.GeneratedCode(", template);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static CodeNamespace SetupNamespace(string nsName)
    {
        var root = CodeNamespace.InitRootNamespace();
        return root.AddNamespace(nsName);
    }

    private static CodeClass CreateStaticHelperClass(CodeNamespace ns, string className)
    {
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

    /// <summary>
    /// Simulates what <see cref="Kiota.Builder.CodeRenderers.CodeRenderer"/> does:
    /// writes StartBlock, each property, each method, then EndBlock.
    /// </summary>
    private void WriteClass(CodeClass codeClass)
    {
        writer.Write(codeClass.StartBlock);
        foreach (var prop in codeClass.Properties)
            writer.Write(prop);
        foreach (var method in codeClass.Methods)
            writer.Write(method);
        writer.Write(codeClass.EndBlock);
    }
}
