using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EngineSimRecorder.Services;

/// <summary>
/// Validates that a JavaScript string is safe for FMOD Studio 1.08's ES3 engine.
/// </summary>
public static class FmodEs3Validator
{
    private static readonly (Regex Pattern, string Message)[] Rules =
    {
        (new Regex(@"\b(const|let)\b",        RegexOptions.Compiled), "ES6 declaration: const/let (use var instead)"),
        (new Regex(@"`",                       RegexOptions.Compiled), "ES6 template literal: ` (use string concatenation)"),
        (new Regex(@"\.endsWith\s*\(",         RegexOptions.Compiled), "ES6 method: .endsWith() (use indexOf or lastIndexOf)"),
        (new Regex(@"=>\s*[{(]",              RegexOptions.Compiled), "ES6 arrow function: => (use function() {})"),
        (new Regex(@"\bfor\s*\(.*\bof\b",      RegexOptions.Compiled), "ES6 for...of loop (use for(;;) or forEach)"),
        (new Regex(@"\.startsWith\s*\(",       RegexOptions.Compiled), "ES6 method: .startsWith() (use indexOf === 0)"),
        (new Regex(@"\.includes\s*\(",         RegexOptions.Compiled), "ES6 method: .includes() (use indexOf !== -1)"),
        (new Regex(@"\bSymbol\s*\(",           RegexOptions.Compiled), "ES6 Symbol"),
        (new Regex(@"\bPromise\s*\(",          RegexOptions.Compiled), "ES6 Promise"),
        (new Regex(@"\bclass\s+\w",            RegexOptions.Compiled), "ES6 class declaration (use function constructors)"),
        (new Regex(@"\.\.\.",                  RegexOptions.Compiled), "ES6 spread/rest operator ..."),
    };

    /// <summary>
    /// Validates <paramref name="jsCode"/> for ES6 patterns unsupported by FMOD 1.08.
    /// </summary>
    /// <param name="jsCode">JavaScript source to check.</param>
    /// <param name="violations">Human-readable descriptions of any violations found.</param>
    /// <returns><c>true</c> if valid (no ES6 patterns detected).</returns>
    public static bool IsValid(string jsCode, out string[] violations)
    {
        var found = new List<string>();

        foreach (var (pattern, message) in Rules)
        {
            if (pattern.IsMatch(jsCode))
                found.Add(message);
        }

        violations = found.ToArray();
        return found.Count == 0;
    }
}
