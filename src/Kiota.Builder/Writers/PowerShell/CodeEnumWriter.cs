using System;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.PowerShell;

public sealed class CodeEnumWriter(PowerShellConventionService conventions) : BaseElementWriter<CodeEnum, PowerShellConventionService>(conventions)
{
    public override void WriteCodeElement(CodeEnum codeElement, LanguageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(codeElement);
        ArgumentNullException.ThrowIfNull(writer);
        conventions.WriteShortDescription(codeElement, writer);
        writer.WriteLine($"enum {codeElement.Name.ToFirstCharacterUpperCase()}");
        writer.StartBlock();
        foreach (var option in codeElement.Options)
            writer.WriteLine(option.Name.ToFirstCharacterUpperCase());
        writer.CloseBlock();
    }
}
