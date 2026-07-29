using System;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.PowerShell;

public sealed class CodePropertyWriter(PowerShellConventionService conventions) : BaseElementWriter<CodeProperty, PowerShellConventionService>(conventions)
{
    public override void WriteCodeElement(CodeProperty codeElement, LanguageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(codeElement);
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine($"[{conventions.GetTypeString(codeElement.Type, codeElement)}]${codeElement.Name.ToFirstCharacterUpperCase()}");
    }
}
