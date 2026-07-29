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
        if (type.TypeDefinition is ITypeDefinition definition)
            return definition.GetFullName();
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
        return $"[Parameter(Mandatory = {!parameter.Optional})][{GetTypeString(parameter.Type, targetElement)}]${parameter.Name.ToFirstCharacterUpperCase()}";
    }

    public override bool WriteShortDescription(IDocumentedElement element, LanguageWriter writer, string prefix = "", string suffix = "")
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(writer);
        if (!element.Documentation.DescriptionAvailable)
            return false;
        writer.WriteLine($"{DocCommentPrefix}{prefix}{element.Documentation.Description}{suffix}");
        return true;
    }
}
