using System.Linq;
using System.Threading.Tasks;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Configuration;
using Kiota.Builder.Refiners;
using Kiota.Builder.Writers.PowerShell;

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

    [Fact]
    public async Task CmdletClassesAreGeneratedForRequestExecutorsAsync()
    {
        var ns = root.AddNamespace("MyClient");
        var requestBuilder = ns.AddClass(new CodeClass
        {
            Name = "UserRequestBuilder",
            Kind = CodeClassKind.RequestBuilder,
        }).First();
        var getMethod = requestBuilder.AddMethod(new CodeMethod
        {
            Name = "Get",
            Kind = CodeMethodKind.RequestExecutor,
            HttpMethod = HttpMethod.Get,
            ReturnType = new CodeType { Name = "string" },
        }).First();
        getMethod.AddParameter(new CodeParameter
        {
            Name = "cancellationToken",
            Optional = true,
            Kind = CodeParameterKind.Cancellation,
            Type = new CodeType { Name = "CancellationToken", IsExternal = true },
        });

        await ILanguageRefiner.RefineAsync(new GenerationConfiguration { Language = GenerationLanguage.PowerShell }, root);

        // A Cmdlets namespace should exist under the root
        var cmdletsNs = root.FindNamespaceByName($"{root.Name}.MyClient.{PowerShellRefiner.CmdletsNamespaceSuffix}");
        // Actually, cmdlets go in the root Cmdlets namespace
        var allNamespaces = GetAllNamespaces(root).ToList();
        var cmdletsNamespace = allNamespaces.FirstOrDefault(n =>
            n.Name.EndsWith("Cmdlets", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(cmdletsNamespace);

        // A GetUserCmdlet class should exist
        var cmdletClass = cmdletsNamespace.Classes.FirstOrDefault(c =>
            c.Name.StartsWith("Get", System.StringComparison.Ordinal) &&
            c.Name.EndsWith("Cmdlet", System.StringComparison.Ordinal));
        Assert.NotNull(cmdletClass);
        Assert.Equal(CodeClassKind.Custom, cmdletClass.Kind);
    }

    [Fact]
    public async Task InfrastructureClassesAreInjectedAsync()
    {
        var ns = root.AddNamespace("MyClient");
        ns.AddClass(new CodeClass
        {
            Name = "UserRequestBuilder",
            Kind = CodeClassKind.RequestBuilder,
        });

        await ILanguageRefiner.RefineAsync(new GenerationConfiguration { Language = GenerationLanguage.PowerShell }, root);

        var allNamespaces = GetAllNamespaces(root).ToList();
        var cmdletsNs = allNamespaces.FirstOrDefault(n =>
            n.Name.EndsWith("Cmdlets", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(cmdletsNs);

        var baseClass = cmdletsNs.Classes.FirstOrDefault(c =>
            c.Name.Equals(PowerShellInfrastructureTemplates.CmdletBaseClassName, System.StringComparison.Ordinal));
        Assert.NotNull(baseClass);
        Assert.StartsWith(PowerShellInfrastructureTemplates.StaticTemplateMarker,
            baseClass.Documentation.DescriptionTemplate, System.StringComparison.Ordinal);

        var jobClass = cmdletsNs.Classes.FirstOrDefault(c =>
            c.Name.Equals(PowerShellInfrastructureTemplates.JobClassName, System.StringComparison.Ordinal));
        Assert.NotNull(jobClass);
    }

    [Theory]
    [InlineData("Get", "VerbsCommon.Get")]
    [InlineData("New", "VerbsCommon.New")]
    [InlineData("Set", "VerbsCommon.Set")]
    [InlineData("Update", "VerbsData.Update")]
    [InlineData("Remove", "VerbsCommon.Remove")]
    [InlineData("Invoke", "VerbsLifecycle.Invoke")]
    public void ConventionServiceMapsVerbsCorrectly(string psVerb, string expected)
    {
        Assert.Equal(expected, PowerShellConventionService.GetVerbsConstant(psVerb));
    }

    [Theory]
    [InlineData("GetUserCmdlet", "Get", "User")]
    [InlineData("NewMessageCmdlet", "New", "Message")]
    [InlineData("SetUserGroupCmdlet", "Set", "UserGroup")]
    [InlineData("RemoveCmdlet", "Remove", "")]
    public void ConventionServiceParsesCmdletNameCorrectly(string className, string expectedVerb, string expectedNoun)
    {
        var (verb, noun) = PowerShellConventionService.ParseCmdletName(className);
        Assert.Equal(expectedVerb, verb);
        Assert.Equal(expectedNoun, noun);
    }

    private static System.Collections.Generic.IEnumerable<CodeNamespace> GetAllNamespaces(CodeNamespace ns)
    {
        yield return ns;
        foreach (var child in ns.Namespaces)
            foreach (var descendant in GetAllNamespaces(child))
                yield return descendant;
    }
}

