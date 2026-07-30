using System;
using System.Linq;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.PowerShell;

public sealed class CodeMethodWriter(PowerShellConventionService conventions) : BaseElementWriter<CodeMethod, PowerShellConventionService>(conventions)
{
    public override void WriteCodeElement(CodeMethod codeElement, LanguageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(codeElement);
        ArgumentNullException.ThrowIfNull(writer);
        conventions.WriteShortDescription(codeElement, writer);
        var parameters = codeElement.Parameters
            .Where(static x => !x.IsOfKind(CodeParameterKind.RequestAdapter, CodeParameterKind.PathParameters, CodeParameterKind.RawUrl))
            .Select(x => conventions.GetParameterSignature(x, codeElement))
            .ToArray();
        var commandName = codeElement.Name.ToFirstCharacterUpperCase();
        writer.WriteLine($"[System.Management.Automation.Cmdlet(\"Invoke\", \"{commandName}\")]");
        writer.WriteLine($"public sealed class {commandName}Command : System.Management.Automation.PSCmdlet");
        writer.StartBlock();
        foreach (var parameter in parameters)
            writer.WriteLine(parameter);
        writer.WriteLine("protected override void ProcessRecord()");
        writer.StartBlock();
        writer.IncreaseIndent();
        writer.WriteLine("WriteObject(null);");
        writer.DecreaseIndent();
        writer.CloseBlock();
        writer.CloseBlock();
    }
}
