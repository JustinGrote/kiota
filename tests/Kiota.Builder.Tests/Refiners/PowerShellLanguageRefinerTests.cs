using System.Linq;
using System.Threading.Tasks;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Configuration;
using Kiota.Builder.Refiners;

using Xunit;

namespace Kiota.Builder.Tests.Refiners;

public class PowerShellLanguageRefinerTests
{
    private readonly CodeNamespace root = CodeNamespace.InitRootNamespace();

    [Theory]
    [InlineData(HttpMethod.Get, "Get")]
    [InlineData(HttpMethod.Post, "New")]
    [InlineData(HttpMethod.Put, "Set")]
    [InlineData(HttpMethod.Patch, "Update")]
    [InlineData(HttpMethod.Delete, "Remove")]
    public async Task RequestExecutorMethodsAreRenamedWithPowerShellVerbsAsync(HttpMethod httpMethod, string expectedVerb)
    {
        var model = root.AddClass(new CodeClass
        {
            Name = "model",
            Kind = CodeClassKind.RequestBuilder
        }).First();
        var method = model.AddMethod(new CodeMethod
        {
            Name = httpMethod.ToString().ToLowerInvariant().ToUpperInvariant()[0] + httpMethod.ToString().ToLowerInvariant()[1..],
            Kind = CodeMethodKind.RequestExecutor,
            HttpMethod = httpMethod,
            ReturnType = new CodeType
            {
                Name = "string"
            }
        }).First();
        method.AddParameter(new CodeParameter
        {
            Name = "cancellationToken",
            Optional = true,
            Kind = CodeParameterKind.Cancellation,
            Type = new CodeType { Name = "CancellationToken", IsExternal = true },
        });

        await ILanguageRefiner.RefineAsync(new GenerationConfiguration { Language = GenerationLanguage.PowerShell }, root);

        // After refinement the method name should start with the PS verb (with "Async" suffix added by CSharp base)
        Assert.StartsWith(expectedVerb, method.Name, System.StringComparison.Ordinal);
        Assert.EndsWith("Async", method.Name, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonRequestExecutorMethodsAreNotRenamedWithPsVerbAsync()
    {
        var model = root.AddClass(new CodeClass
        {
            Name = "model",
            Kind = CodeClassKind.RequestBuilder
        }).First();
        var method = model.AddMethod(new CodeMethod
        {
            Name = "serialize",
            Kind = CodeMethodKind.Serializer,
            ReturnType = new CodeType { Name = "void" }
        }).First();

        await ILanguageRefiner.RefineAsync(new GenerationConfiguration { Language = GenerationLanguage.PowerShell }, root);

        // Should not have a PS verb prefix - name may have "Async" suffix from base refiner but should not start with a PS verb
        Assert.DoesNotMatch("^(Get|New|Set|Update|Remove|Invoke)", method.Name);
    }
}
