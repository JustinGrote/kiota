using System;
using System.IO;
using System.Linq;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Writers;
using Kiota.Builder.Writers.PowerShell;
using Xunit;

namespace Kiota.Builder.Tests.Writers.PowerShell;

/// <summary>
/// Tests for <see cref="PowerShellCodePropertyWriter"/>.
/// Covers:
///   - Properties on cmdlet classes get [Parameter] attributes (Mandatory / ValueFromPipeline).
///   - Properties on static helper classes are silently skipped.
///   - Properties on regular classes use standard CSharp behaviour.
/// </summary>
public sealed class CodePropertyWriterTests : IDisposable
{
    private const string DefaultPath = "./";
    private const string DefaultName = "name";
    private readonly StringWriter tw;
    private readonly LanguageWriter writer;
    private readonly PowerShellCodePropertyWriter propertyWriter;

    public CodePropertyWriterTests()
    {
        var conventions = new PowerShellConventionService();
        propertyWriter = new PowerShellCodePropertyWriter(conventions);
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
    // Cmdlet class properties — [Parameter] attributes
    // -----------------------------------------------------------------------

    [Fact]
    public void WritesParameterAttributeOnCmdletClassProperty()
    {
        var (_, prop) = SetupCmdletProperty("GetUserCmdlet", "UserId", isNullable: false);

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        Assert.Contains("[Parameter", result);
    }

    [Fact]
    public void WritesMandatoryTrueForNonNullableProperty()
    {
        var (_, prop) = SetupCmdletProperty("GetUserCmdlet", "UserId", isNullable: false);

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        Assert.Contains("[Parameter(Mandatory = true)]", result);
    }

    [Fact]
    public void WritesMandatoryFalseForNullableProperty()
    {
        var (_, prop) = SetupCmdletProperty("GetUserCmdlet", "Filter", isNullable: true);

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        // Nullable → Mandatory = false (i.e., plain [Parameter])
        Assert.Contains("[Parameter]", result);
        Assert.DoesNotContain("Mandatory = true", result);
    }

    [Fact]
    public void WritesPropertyDeclarationAfterParameterAttribute()
    {
        var (_, prop) = SetupCmdletProperty("GetItemCmdlet", "ItemId", isNullable: false);

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        // The actual C# property should follow the [Parameter] attribute
        Assert.Contains("public", result);
        Assert.Contains("ItemId", result);
    }

    // -----------------------------------------------------------------------
    // Pipeline input — ValueFromPipeline for Set/Update/Remove + RequestBody
    // -----------------------------------------------------------------------

    [Fact]
    public void WritesValueFromPipelineForRequestBodyOnSetCmdlet()
    {
        var (_, prop) = SetupCmdletProperty("SetUserCmdlet", "Body",
            isNullable: false, kind: CodePropertyKind.RequestBody);

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        Assert.Contains("ValueFromPipeline = true", result);
    }

    [Fact]
    public void WritesValueFromPipelineForRequestBodyOnRemoveCmdlet()
    {
        var (_, prop) = SetupCmdletProperty("RemoveUserCmdlet", "Body",
            isNullable: false, kind: CodePropertyKind.RequestBody);

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        Assert.Contains("ValueFromPipeline = true", result);
    }

    [Fact]
    public void WritesValueFromPipelineForRequestBodyOnUpdateCmdlet()
    {
        var (_, prop) = SetupCmdletProperty("UpdateItemCmdlet", "Body",
            isNullable: false, kind: CodePropertyKind.RequestBody);

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        Assert.Contains("ValueFromPipeline = true", result);
    }

    [Fact]
    public void DoesNotWriteValueFromPipelineForGetCmdlet()
    {
        var (_, prop) = SetupCmdletProperty("GetUserCmdlet", "Body",
            isNullable: false, kind: CodePropertyKind.RequestBody);

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        Assert.DoesNotContain("ValueFromPipeline", result);
    }

    [Fact]
    public void DoesNotWriteValueFromPipelineForNonBodyProperty()
    {
        // Custom (path-param style) property on Set cmdlet — no pipeline
        var (_, prop) = SetupCmdletProperty("SetUserCmdlet", "UserId",
            isNullable: false, kind: CodePropertyKind.Custom);

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        Assert.DoesNotContain("ValueFromPipeline", result);
    }

    // -----------------------------------------------------------------------
    // Static helper classes — properties silently skipped
    // -----------------------------------------------------------------------

    [Fact]
    public void SkipsPropertiesOnStaticHelperClasses()
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
        var prop = helperClass.AddProperty(new CodeProperty
        {
            Name = "SomeProp",
            Type = new CodeType { Name = "string", IsExternal = true },
            Kind = CodePropertyKind.Custom,
        }).First();

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        Assert.Empty(result.Trim());
    }

    // -----------------------------------------------------------------------
    // Regular (non-cmdlet) classes — CSharp base behaviour
    // -----------------------------------------------------------------------

    [Fact]
    public void WritesStandardPropertyForNonCmdletClass()
    {
        var root = CodeNamespace.InitRootNamespace();
        var ns = root.AddNamespace("MyClient");
        var modelClass = ns.AddClass(new CodeClass
        {
            Name = "UserModel",
            Kind = CodeClassKind.Model,
        }).First();
        var prop = modelClass.AddProperty(new CodeProperty
        {
            Name = "DisplayName",
            Type = new CodeType { Name = "string", IsExternal = true },
            Kind = CodePropertyKind.Custom,
        }).First();

        propertyWriter.WriteCodeElement(prop, writer);
        var result = tw.ToString();

        Assert.Contains("public", result);
        Assert.Contains("DisplayName", result);
        Assert.DoesNotContain("[Parameter", result);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (CodeClass cmdletClass, CodeProperty prop) SetupCmdletProperty(
        string cmdletName,
        string propName,
        bool isNullable,
        CodePropertyKind kind = CodePropertyKind.Custom)
    {
        var root = CodeNamespace.InitRootNamespace();
        var ns = root.AddNamespace("TestNs");
        var cmdletClass = ns.AddClass(new CodeClass
        {
            Name = cmdletName,
            Kind = CodeClassKind.Custom,
        }).First();
        var prop = cmdletClass.AddProperty(new CodeProperty
        {
            Name = propName,
            Kind = kind,
            Type = new CodeType { Name = "string", IsExternal = true, IsNullable = isNullable },
        }).First();
        return (cmdletClass, prop);
    }
}
