using Kiota.Builder.PathSegmenters;

namespace Kiota.Builder.Writers.PowerShell;

public class PowerShellWriter : CSharp.CSharpWriter
{
    public PowerShellWriter(string rootPath, string clientNamespaceName) : base(rootPath, clientNamespaceName)
    {
        PathSegmenter = new PowerShellPathSegmenter(rootPath, clientNamespaceName);
    }
}
