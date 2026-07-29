using Kiota.Builder.CodeDOM;

namespace Kiota.Builder.Writers.PowerShell;

public sealed class CodeTypeWriter(PowerShellConventionService conventions) : BaseElementWriter<CodeType, PowerShellConventionService>(conventions)
{
    public override void WriteCodeElement(CodeType codeElement, LanguageWriter writer)
    {
    }
}
