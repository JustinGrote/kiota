using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Configuration;

namespace Kiota.Builder.Refiners;

public class PowerShellRefiner : CSharpRefiner, ILanguageRefiner
{
    public PowerShellRefiner(GenerationConfiguration configuration) : base(configuration) { }

    private static readonly Dictionary<HttpMethod, string> HttpMethodToPowerShellVerb = new()
    {
        { HttpMethod.Get,     "Get" },
        { HttpMethod.Post,    "New" },
        { HttpMethod.Put,     "Set" },
        { HttpMethod.Patch,   "Update" },
        { HttpMethod.Delete,  "Remove" },
        { HttpMethod.Head,    "Invoke" },
        { HttpMethod.Options, "Invoke" },
        { HttpMethod.Connect, "Invoke" },
        { HttpMethod.Trace,   "Invoke" },
    };

    public override Task RefineAsync(CodeNamespace generatedCode, CancellationToken cancellationToken)
    {
        return base.RefineAsync(generatedCode, cancellationToken).ContinueWith(_ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenameRequestExecutorMethodsWithPowerShellVerbs(generatedCode);
        }, cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    private static void RenameRequestExecutorMethodsWithPowerShellVerbs(CodeElement currentElement)
    {
        if (currentElement is CodeMethod method &&
            method.IsOfKind(CodeMethodKind.RequestExecutor) &&
            method.HttpMethod.HasValue &&
            HttpMethodToPowerShellVerb.TryGetValue(method.HttpMethod.Value, out var psVerb))
        {
            // Method name at this point has "Async" suffix appended by CSharpRefiner.AddAsyncSuffix
            // Replace the HTTP verb prefix with the PowerShell verb
            var currentName = method.Name;
            foreach (var (httpMethod, _) in HttpMethodToPowerShellVerb)
            {
                var httpMethodName = httpMethod.ToString();
                if (currentName.StartsWith(httpMethodName, System.StringComparison.OrdinalIgnoreCase))
                {
                    var suffix = currentName[httpMethodName.Length..];
                    method.Name = psVerb + suffix;
                    break;
                }
            }
        }
        CrawlTree(currentElement, RenameRequestExecutorMethodsWithPowerShellVerbs);
    }
}
