using System;
using System.Globalization;

using Kiota.Builder.CodeDOM;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.PowerShell;

public class PowerShellConventionService : CSharp.CSharpConventionService
{
    /// <summary>Maps a PS verb class-name prefix to the PowerShell verb constant (class.Verb).</summary>
    public static string GetVerbsConstant(string psVerb) => psVerb switch
    {
        "Get" => "VerbsCommon.Get",
        "New" => "VerbsCommon.New",
        "Set" => "VerbsCommon.Set",
        "Update" => "VerbsData.Update",
        "Remove" => "VerbsCommon.Remove",
        _ => "VerbsLifecycle.Invoke",
    };

    /// <summary>
    /// Given a cmdlet class name like "GetUsersCmdlet", returns (verb="Get", noun="Users").
    /// </summary>
    public static (string verb, string noun) ParseCmdletName(string className)
    {
        if (string.IsNullOrEmpty(className))
            return ("Invoke", string.Empty);

        const string cmdletSuffix = "Cmdlet";
        var name = className.EndsWith(cmdletSuffix, StringComparison.Ordinal)
            ? className[..^cmdletSuffix.Length]
            : className;

        foreach (var verb in PsVerbs)
        {
            if (name.StartsWith(verb, StringComparison.Ordinal))
                return (verb, name[verb.Length..]);
        }
        return ("Invoke", name);
    }

    private static readonly string[] PsVerbs = ["Get", "New", "Set", "Update", "Remove", "Invoke"];
}
