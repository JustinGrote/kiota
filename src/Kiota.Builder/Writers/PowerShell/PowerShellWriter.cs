using Kiota.Builder.PathSegmenters;

namespace Kiota.Builder.Writers.PowerShell;

public class PowerShellWriter : CSharp.CSharpWriter
{
    public PowerShellWriter(string rootPath, string clientNamespaceName) : base(rootPath, clientNamespaceName)
    {
        PathSegmenter = new PowerShellPathSegmenter(rootPath, clientNamespaceName);
        var conventionService = new PowerShellConventionService();
        // Override the CSharp writers with PowerShell-aware ones
        AddOrReplaceCodeElementWriter(new PowerShellCodeClassDeclarationWriter(conventionService));
        AddOrReplaceCodeElementWriter(new PowerShellCodeBlockEndWriter(conventionService));
        AddOrReplaceCodeElementWriter(new PowerShellCodePropertyWriter(conventionService));
        AddOrReplaceCodeElementWriter(new PowerShellCodeMethodWriter(conventionService));
    }
}

