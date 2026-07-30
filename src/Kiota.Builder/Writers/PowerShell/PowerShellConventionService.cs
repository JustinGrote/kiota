using System;
using Kiota.Builder.CodeDOM;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.PowerShell;

public sealed class PowerShellConventionService : CommonLanguageConventionService
{
    public override string StreamTypeName => "System.IO.Stream";
    public override string VoidTypeName => "void";
    public override string DocCommentPrefix => "# ";
    public override string ParseNodeInterfaceName => "object";
    public override string TempDictionaryVarName => "urlTemplateParameters";

    public override string GetAccessModifier(AccessModifier access) => access switch
    {
        AccessModifier.Private => "hidden",
        _ => string.Empty,
    };

    public override string TranslateType(CodeType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.TypeDefinition is CodeElement definition)
            return definition.Name;
        return type.Name.ToLowerInvariant() switch
        {
            "integer" or "int32" => "int",
            "int64" => "long",
            "boolean" => "bool",
            "float" or "double" or "decimal" => "double",
            "binary" or "base64" or "base64url" => "byte[]",
            "string" => "string",
            "void" => "void",
            _ => type.Name.ToFirstCharacterUpperCase(),
        };
    }

    public override string GetTypeString(CodeTypeBase code, CodeElement targetElement, bool includeCollectionInformation = true, LanguageWriter? writer = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code is not CodeType type)
            return "object";
        var result = TranslateType(type);
        if (includeCollectionInformation && type.CollectionKind != CodeTypeBase.CodeTypeCollectionKind.None)
            result += "[]";
        return result;
    }

    public override string GetParameterSignature(CodeParameter parameter, CodeElement targetElement, LanguageWriter? writer = null)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return $"[System.Management.Automation.Parameter(Mandatory = {!parameter.Optional})] public {GetTypeString(parameter.Type, targetElement)} {parameter.Name.ToFirstCharacterUpperCase()} {{ get; set; }}";
    }

    public override bool WriteShortDescription(IDocumentedElement element, LanguageWriter writer, string prefix = "", string suffix = "")
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(writer);
        if (!element.Documentation.DescriptionAvailable)
            return false;
        if (element is not CodeElement codeElement)
            return false;
        var description = element.Documentation.GetDescription(type => GetTypeString(type, codeElement));
        writer.WriteLine($"{DocCommentPrefix}{prefix}{description}{suffix}");
        return true;
    }
}
