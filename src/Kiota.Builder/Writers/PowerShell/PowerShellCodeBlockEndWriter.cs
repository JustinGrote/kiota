using Kiota.Builder.CodeDOM;

namespace Kiota.Builder.Writers.PowerShell;

/// <summary>
/// Closes class/namespace blocks for PowerShell-generated C# code.
/// Static helper classes (KiotaPSCmdletBase, KiotaPSJob) write their full
/// content inside <see cref="PowerShellCodeClassDeclarationWriter"/> and leave
/// no open blocks, so the block end is a no-op for those classes.
/// </summary>
public class PowerShellCodeBlockEndWriter : CSharp.CodeBlockEndWriter
{
    public PowerShellCodeBlockEndWriter(PowerShellConventionService conventionService)
        : base(conventionService) { }

    public override void WriteCodeElement(BlockEnd codeElement, LanguageWriter writer)
    {
        // Static helper classes write everything (including braces) in the declaration writer.
        // There are no open blocks to close here.
        if (codeElement?.Parent is CodeClass parentClass &&
            PowerShellCodeClassDeclarationWriter.IsStaticHelper(parentClass))
            return;

        base.WriteCodeElement(codeElement!, writer);
    }
}
