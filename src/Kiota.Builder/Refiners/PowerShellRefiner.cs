using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Configuration;
using Kiota.Builder.Extensions;
using Kiota.Builder.Writers.PowerShell;

namespace Kiota.Builder.Refiners;

public class PowerShellRefiner : CSharpRefiner, ILanguageRefiner
{
    public PowerShellRefiner(GenerationConfiguration configuration) : base(configuration) { }

    private const string RequestBuilderSuffix = "RequestBuilder";
    private const string CmdletSuffix = "Cmdlet";
    private const string SmaNamespace = "System.Management.Automation";

    /// <summary>The suffix appended to the client namespace to form the Cmdlets namespace (e.g., "Cmdlets").</summary>
    public const string CmdletsNamespaceSuffix = "Cmdlets";

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
            // Rename RequestExecutor methods with PS verb prefixes
            RenameRequestExecutorMethodsWithPowerShellVerbs(generatedCode);
            cancellationToken.ThrowIfCancellationRequested();
            // Build the Cmdlets namespace and generate cmdlet wrapper classes
            GenerateCmdletClasses(generatedCode);
        }, cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    // -----------------------------------------------------------------------
    // Step 1 — Rename RequestExecutor methods with PS verb prefixes
    // -----------------------------------------------------------------------

    private static void RenameRequestExecutorMethodsWithPowerShellVerbs(CodeElement currentElement)
    {
        if (currentElement is CodeMethod method &&
            method.IsOfKind(CodeMethodKind.RequestExecutor) &&
            method.HttpMethod.HasValue &&
            HttpMethodToPowerShellVerb.TryGetValue(method.HttpMethod.Value, out var psVerb))
        {
            var currentName = method.Name;
            foreach (var (httpMethod, _) in HttpMethodToPowerShellVerb)
            {
                var httpMethodName = httpMethod.ToString();
                if (currentName.StartsWith(httpMethodName, StringComparison.OrdinalIgnoreCase))
                {
                    var suffix = currentName[httpMethodName.Length..];
                    method.Name = psVerb + suffix;
                    break;
                }
            }
        }
        CrawlTree(currentElement, RenameRequestExecutorMethodsWithPowerShellVerbs);
    }

    // -----------------------------------------------------------------------
    // Step 2 — Generate cmdlet wrapper classes
    // -----------------------------------------------------------------------

    private static void GenerateCmdletClasses(CodeNamespace rootNamespace)
    {
        // Determine the single root Cmdlets namespace (e.g., MyClient.Cmdlets)
        var cmdletsNs = GetOrCreateCmdletsNamespace(rootNamespace);

        // Inject infrastructure classes (KiotaPSCmdletBase, KiotaPSJob)
        EnsureInfrastructureClasses(cmdletsNs);

        // Scan the entire tree for RequestBuilder classes and create cmdlets
        CreateCmdletsForNamespace(rootNamespace, cmdletsNs, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static CodeNamespace GetOrCreateCmdletsNamespace(CodeNamespace rootNamespace)
    {
        // If the root namespace has an empty name (e.g., during unit tests), find the first
        // meaningful child namespace to use as the prefix; otherwise build {rootName}.Cmdlets.
        var prefix = rootNamespace.Name;
        if (string.IsNullOrEmpty(prefix))
        {
            prefix = rootNamespace.Namespaces.FirstOrDefault()?.Name ?? CmdletsNamespaceSuffix;
            // If the prefix already ends with Cmdlets, avoid double-appending
            if (prefix.EndsWith(CmdletsNamespaceSuffix, StringComparison.OrdinalIgnoreCase))
                return rootNamespace.FindOrAddNamespace(prefix);
        }
        var cmdletsNsName = $"{prefix}.{CmdletsNamespaceSuffix}";
        return rootNamespace.FindOrAddNamespace(cmdletsNsName);
    }

    private static void EnsureInfrastructureClasses(CodeNamespace cmdletsNs)
    {
        if (cmdletsNs.FindChildByName<CodeClass>(PowerShellInfrastructureTemplates.CmdletBaseClassName) is null)
        {
            var baseClass = new CodeClass
            {
                Name = PowerShellInfrastructureTemplates.CmdletBaseClassName,
                Kind = CodeClassKind.Custom,
                Documentation = new CodeDocumentation
                {
                    DescriptionTemplate = PowerShellInfrastructureTemplates.StaticTemplateMarker,
                },
            };
            cmdletsNs.AddClass(baseClass);
        }

        if (cmdletsNs.FindChildByName<CodeClass>(PowerShellInfrastructureTemplates.JobClassName) is null)
        {
            var jobClass = new CodeClass
            {
                Name = PowerShellInfrastructureTemplates.JobClassName,
                Kind = CodeClassKind.Custom,
                Documentation = new CodeDocumentation
                {
                    DescriptionTemplate = PowerShellInfrastructureTemplates.StaticTemplateMarker,
                },
            };
            cmdletsNs.AddClass(jobClass);
        }
    }

    private static void CreateCmdletsForNamespace(
        CodeNamespace currentNamespace,
        CodeNamespace cmdletsNs,
        HashSet<string> createdCmdletNames)
    {
        // For every RequestBuilder class in this namespace, create cmdlet wrappers
        foreach (var codeClass in currentNamespace.Classes
            .Where(static c => c.IsOfKind(CodeClassKind.RequestBuilder)))
        {
            CreateCmdletsForRequestBuilder(codeClass, cmdletsNs, createdCmdletNames);
        }

        // Recurse into child namespaces
        foreach (var childNs in currentNamespace.Namespaces)
            CreateCmdletsForNamespace(childNs, cmdletsNs, createdCmdletNames);
    }

    private static void CreateCmdletsForRequestBuilder(
        CodeClass requestBuilder,
        CodeNamespace cmdletsNs,
        HashSet<string> createdCmdletNames)
    {
        var noun = GetNounFromRequestBuilder(requestBuilder.Name);

        var executorMethods = requestBuilder.Methods
            .Where(static m => m.IsOfKind(CodeMethodKind.RequestExecutor) && m.HttpMethod.HasValue)
            .ToArray();

        // Get path parameters from the RequestBuilder constructor
        var pathParams = GetPathParametersFromConstructor(requestBuilder);

        foreach (var executorMethod in executorMethods)
        {
            if (!HttpMethodToPowerShellVerb.TryGetValue(executorMethod.HttpMethod!.Value, out var psVerb))
                continue;

            var cmdletName = MakeUniqueCmdletName($"{psVerb}{noun}{CmdletSuffix}", createdCmdletNames);
            var cmdletClass = BuildCmdletClass(cmdletName, psVerb, executorMethod, requestBuilder, pathParams, cmdletsNs);
            cmdletsNs.AddClass(cmdletClass);
        }
    }

    private static string GetNounFromRequestBuilder(string className)
    {
        var noun = className.EndsWith(RequestBuilderSuffix, StringComparison.OrdinalIgnoreCase)
            ? className[..^RequestBuilderSuffix.Length]
            : className;
        // Strip "Item" suffix (e.g., UserItemRequestBuilder → User)
        if (noun.EndsWith("Item", StringComparison.OrdinalIgnoreCase) && noun.Length > 4)
            noun = noun[..^4];
        return noun.ToFirstCharacterUpperCase();
    }

    private static string MakeUniqueCmdletName(string baseName, HashSet<string> existingNames)
    {
        if (existingNames.Add(baseName))
            return baseName;
        // Suffix with a counter if there's a collision
        for (var i = 2; i < 100; i++)
        {
            var candidate = $"{baseName}{i}";
            if (existingNames.Add(candidate))
                return candidate;
        }
        return baseName; // fallback (shouldn't happen)
    }

    private static List<(string Name, CodeTypeBase Type, bool Required)> GetPathParametersFromConstructor(
        CodeClass requestBuilder)
    {
        var ctor = requestBuilder.Methods.FirstOrDefault(
            static m => m.IsOfKind(CodeMethodKind.Constructor));
        if (ctor is null)
            return [];

        return ctor.Parameters
            .Where(static p => p.IsOfKind(CodeParameterKind.Path))
            .Select(static p => (p.Name.ToFirstCharacterUpperCase(), p.Type, true))
            .ToList();
    }

    private static CodeClass BuildCmdletClass(
        string cmdletName,
        string psVerb,
        CodeMethod executorMethod,
        CodeClass requestBuilder,
        List<(string Name, CodeTypeBase Type, bool Required)> pathParams,
        CodeNamespace cmdletsNs)
    {
        var cmdletClass = new CodeClass
        {
            Name = cmdletName,
            Kind = CodeClassKind.Custom,
            Documentation = new CodeDocumentation
            {
                DescriptionTemplate = executorMethod.Documentation?.DescriptionTemplate
                    ?? $"PowerShell cmdlet wrapping {requestBuilder.Name}.{executorMethod.Name}",
            },
        };

        // Inherit from KiotaPSCmdletBase
        cmdletClass.StartBlock.Inherits = new CodeType
        {
            Name = PowerShellInfrastructureTemplates.CmdletBaseClassName,
            IsExternal = false,
            TypeDefinition = cmdletsNs.FindChildByName<CodeClass>(PowerShellInfrastructureTemplates.CmdletBaseClassName),
        };

        // Add using for System.Management.Automation
        cmdletClass.StartBlock.AddUsings(new CodeUsing
        {
            Name = SmaNamespace,
            Declaration = new CodeType { Name = SmaNamespace, IsExternal = true },
        });
        cmdletClass.StartBlock.AddUsings(new CodeUsing
        {
            Name = "System.Threading",
            Declaration = new CodeType { Name = "System.Threading", IsExternal = true },
        });
        cmdletClass.StartBlock.AddUsings(new CodeUsing
        {
            Name = "System.Threading.Tasks",
            Declaration = new CodeType { Name = "System.Threading.Tasks", IsExternal = true },
        });

        // Add path parameter properties (Mandatory)
        foreach (var (name, type, required) in pathParams)
        {
            var prop = new CodeProperty
            {
                Name = name,
                Kind = CodePropertyKind.Custom,
                ReadOnly = false,
                Type = (CodeTypeBase)type.Clone(),
                Access = AccessModifier.Public,
            };
            cmdletClass.AddProperty(prop);
        }

        // Add request body property if present
        var bodyParam = executorMethod.Parameters.FirstOrDefault(
            static p => p.IsOfKind(CodeParameterKind.RequestBody));
        if (bodyParam is not null)
        {
            var bodyProp = new CodeProperty
            {
                Name = "Body",
                Kind = CodePropertyKind.RequestBody,
                ReadOnly = false,
                Type = (CodeTypeBase)bodyParam.Type.Clone(),
                Access = AccessModifier.Public,
            };
            cmdletClass.AddProperty(bodyProp);
        }

        // Add ExecuteAsync method
        var executeMethod = new CodeMethod
        {
            Name = "ExecuteAsync",
            Kind = CodeMethodKind.Custom,
            IsAsync = true,
            Access = AccessModifier.Protected,
            ReturnType = new CodeType { Name = "object", IsNullable = true },
            HttpMethod = executorMethod.HttpMethod,
        };
        executeMethod.AddParameter(new CodeParameter
        {
            Name = "cancellationToken",
            Kind = CodeParameterKind.Cancellation,
            Optional = false,
            Type = new CodeType { Name = "CancellationToken", IsExternal = true },
        });
        cmdletClass.AddMethod(executeMethod);

        return cmdletClass;
    }
}

