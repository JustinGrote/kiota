using System;
using Kiota.Builder.PathSegmenters;
using Kiota.Builder.Writers.PowerShell;
using Xunit;

namespace Kiota.Builder.Tests.Writers.PowerShell;

public class PowerShellWriterTests
{
    [Fact]
    public void Instantiates()
    {
        var writer = new PowerShellWriter("./", "ApiSdk");
        Assert.NotNull(writer);
        Assert.Equal(".cs", ((PowerShellPathSegmenter)writer.PathSegmenter!).FileSuffix);
        Assert.Throws<ArgumentException>(() => new PowerShellWriter(string.Empty, "ApiSdk"));
        Assert.Throws<ArgumentException>(() => new PowerShellWriter("./", string.Empty));
    }
}
