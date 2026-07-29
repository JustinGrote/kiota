using Kiota.Builder.PathSegmenters;

namespace Kiota.Builder.Writers.PowerShell;

public sealed class PowerShellWriter : LanguageWriter
{
    public PowerShellWriter(string rootPath, string clientNamespaceName)
    {
        PathSegmenter = new PowerShellPathSegmenter(rootPath, clientNamespaceName);
        var conventions = new PowerShellConventionService();
        AddOrReplaceCodeElementWriter(new CodeBlockEndWriter(conventions));
        AddOrReplaceCodeElementWriter(new CodeClassDeclarationWriter(conventions));
        AddOrReplaceCodeElementWriter(new CodeEnumWriter(conventions));
        AddOrReplaceCodeElementWriter(new CodeMethodWriter(conventions));
        AddOrReplaceCodeElementWriter(new CodePropertyWriter(conventions));
        AddOrReplaceCodeElementWriter(new CodeTypeWriter(conventions));
    }
}
