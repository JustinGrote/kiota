using System;

using Kiota.Builder.Writers.PowerShell;

using Xunit;

namespace Kiota.Builder.Tests.Writers.PowerShell;

public class PowerShellWriterTests
{
    [Fact]
    public void Instantiates()
    {
        var writer = new PowerShellWriter("./", "graph");
        Assert.NotNull(writer);
        Assert.NotNull(writer.PathSegmenter);
        Assert.Throws<ArgumentNullException>(() => new PowerShellWriter(null, "graph"));
        Assert.Throws<ArgumentNullException>(() => new PowerShellWriter("./", null));
    }
}
