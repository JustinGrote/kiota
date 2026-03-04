using System;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.PowerShell;

/// <summary>
/// Writes properties for PowerShell cmdlet classes. For cmdlet wrapper classes
/// (name ends with "Cmdlet") it prepends a <c>[Parameter]</c> attribute.
/// For all other classes it delegates to the CSharp base writer.
/// </summary>
public class PowerShellCodePropertyWriter : CSharp.CodePropertyWriter
{
    public PowerShellCodePropertyWriter(PowerShellConventionService conventionService)
        : base(conventionService) { }

    public override void WriteCodeElement(CodeProperty codeElement, LanguageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(codeElement);
        ArgumentNullException.ThrowIfNull(writer);

        // Skip static helper classes entirely (content written by the declaration writer)
        if (codeElement.Parent is CodeClass parentClass &&
            PowerShellCodeClassDeclarationWriter.IsStaticHelper(parentClass))
            return;

        // For cmdlet wrapper classes, add [Parameter] attribute before the property
        if (codeElement.Parent is CodeClass cmdletClass &&
            PowerShellCodeClassDeclarationWriter.IsCmdletClass(cmdletClass))
        {
            WriteCmdletProperty(codeElement, cmdletClass, writer);
            return;
        }

        base.WriteCodeElement(codeElement, writer);
    }

    private void WriteCmdletProperty(CodeProperty codeElement, CodeClass parentClass, LanguageWriter writer)
    {
        // A property is mandatory if its type is non-nullable (value types / required model params)
        var isMandatory = !codeElement.Type.IsNullable;
        var isPipelineInput = IsPipelineCandidate(codeElement, parentClass);

        var paramAttr = (isMandatory, isPipelineInput) switch
        {
            (true, true) => "[Parameter(Mandatory = true, ValueFromPipeline = true)]",
            (true, false) => "[Parameter(Mandatory = true)]",
            (false, true) => "[Parameter(ValueFromPipeline = true)]",
            _ => "[Parameter]",
        };
        writer.WriteLine(paramAttr);

        // Delegate to CSharp base for the actual property declaration
        base.WriteCodeElement(codeElement, writer);
    }

    private static bool IsPipelineCandidate(CodeProperty property, CodeClass parentClass)
    {
        // For Set/Remove cmdlets, the primary model object (RequestBody kind) accepts pipeline input
        var (verb, _) = PowerShellConventionService.ParseCmdletName(parentClass.Name);
        if (verb is not ("Set" or "Update" or "Remove"))
            return false;

        return property.IsOfKind(CodePropertyKind.RequestBody);
    }
}
