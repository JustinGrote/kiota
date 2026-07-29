using System;
using Kiota.Builder.CodeDOM;

namespace Kiota.Builder.Writers.PowerShell;

public sealed class CodeBlockEndWriter(PowerShellConventionService conventions) : BaseElementWriter<BlockEnd, PowerShellConventionService>(conventions)
{
    public override void WriteCodeElement(BlockEnd codeElement, LanguageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(codeElement);
        ArgumentNullException.ThrowIfNull(writer);
        writer.CloseBlock();
    }
}
