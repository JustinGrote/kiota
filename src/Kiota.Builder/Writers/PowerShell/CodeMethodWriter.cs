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
        writer.WriteLine($"function {codeElement.Name.ToFirstCharacterUpperCase()}");
        writer.StartBlock();
        writer.WriteLine("[CmdletBinding(SupportsShouldProcess)]");
        writer.WriteLine($"param({string.Join(", ", parameters)})");
        writer.WriteLine("if ($PSCmdlet.ShouldProcess($PSCmdlet.MyInvocation.Line)) {");
        writer.IncreaseIndent();
        writer.WriteLine("$PSCmdlet.WriteObject($null)");
        writer.DecreaseIndent();
        writer.CloseBlock();
    }
}
