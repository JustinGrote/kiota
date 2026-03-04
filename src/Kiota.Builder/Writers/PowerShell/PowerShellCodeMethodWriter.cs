using System;
using System.Linq;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.PowerShell;

/// <summary>
/// Writes methods for PowerShell code.
/// For <c>ExecuteAsync</c> methods inside cmdlet wrapper classes it generates a
/// body that wires up the underlying C# SDK call.
/// Static helper class methods are skipped (written by the declaration writer).
/// All other methods delegate to the CSharp base writer.
/// </summary>
public class PowerShellCodeMethodWriter : CSharp.CodeMethodWriter
{
    public PowerShellCodeMethodWriter(PowerShellConventionService conventionService)
        : base(conventionService) { }

    public override void WriteCodeElement(CodeMethod codeElement, LanguageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(codeElement);
        ArgumentNullException.ThrowIfNull(writer);

        // Skip static helper classes (written by declaration writer)
        if (codeElement.Parent is CodeClass parentClass &&
            PowerShellCodeClassDeclarationWriter.IsStaticHelper(parentClass))
            return;

        // For ExecuteAsync in cmdlet wrapper classes, write the PS-specific body
        if (codeElement.Parent is CodeClass cmdletClass &&
            PowerShellCodeClassDeclarationWriter.IsCmdletClass(cmdletClass) &&
            codeElement.Name.Equals("ExecuteAsync", StringComparison.Ordinal))
        {
            WriteExecuteAsyncMethod(codeElement, cmdletClass, writer);
            return;
        }

        base.WriteCodeElement(codeElement, writer);
    }

    private void WriteExecuteAsyncMethod(CodeMethod method, CodeClass parentClass, LanguageWriter writer)
    {
        conventions.WriteLongDescription(method, writer);

        var returnType = conventions.GetTypeString(method.ReturnType, method);
        writer.WriteLine($"protected override async {returnType} ExecuteAsync(global::System.Threading.CancellationToken cancellationToken)");
        writer.StartBlock();

        // Find matching parameters (path params + request body)
        var pathParams = parentClass.Properties
            .Where(static p => p.IsOfKind(CodePropertyKind.Custom))
            .ToArray();
        var bodyParam = parentClass.Properties
            .FirstOrDefault(static p => p.IsOfKind(CodePropertyKind.RequestBody));

        // Build a comment showing the intended SDK call pattern
        writer.WriteLine($"// TODO: obtain a request adapter and call the underlying SDK method.");
        writer.WriteLine($"// Example pattern:");
        writer.WriteLine($"//   var requestAdapter = GetVariableValue(\"requestAdapter\") as IRequestAdapter;");
        if (pathParams.Length > 0)
        {
            var paramsList = string.Join(", ", pathParams.Select(p => $"\"{p.Name}\": {p.Name.ToFirstCharacterUpperCase()}"));
            writer.WriteLine($"//   var pathParams = new Dictionary<string, object> {{ {paramsList} }};");
        }
        writer.WriteLine($"//   var result = await new <RequestBuilder>(pathParams, requestAdapter).<Method>(cancellationToken: cancellationToken);");
        writer.WriteLine($"//   return result;");
        writer.WriteLine("throw new global::System.NotImplementedException(\"Inject a request adapter to use this cmdlet.\");");

        writer.CloseBlock();
    }
}
