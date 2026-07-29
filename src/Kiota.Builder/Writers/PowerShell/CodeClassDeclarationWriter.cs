using System;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.PowerShell;

public sealed class CodeClassDeclarationWriter(PowerShellConventionService conventions) : BaseElementWriter<ClassDeclaration, PowerShellConventionService>(conventions)
{
    public override void WriteCodeElement(ClassDeclaration codeElement, LanguageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(codeElement);
        ArgumentNullException.ThrowIfNull(writer);
        if (codeElement.Parent is not CodeClass parent)
            throw new InvalidOperationException("A class declaration must have a class parent.");
        conventions.WriteShortDescription(parent, writer);
        writer.WriteLine($"class {codeElement.Name.ToFirstCharacterUpperCase()}");
        writer.StartBlock();
    }
}
