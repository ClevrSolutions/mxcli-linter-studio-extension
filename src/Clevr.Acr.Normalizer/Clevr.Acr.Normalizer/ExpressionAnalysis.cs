using System.Text.RegularExpressions;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// Pure analyse van Mendix-microflow-EXPRESSIE-strings (de nieuwe "expressie-route": expressies
/// staan als platte string in de bson-AST, niet als sub-AST). Geen IO.
///
/// Eerste regel: REDUNDANTE EMPTY-STRING-CHECK. In Mendix is een lege string al gelijk aan
/// <c>empty</c>, dus een check als <c>$x/Attr != empty and $x/Attr != ''</c> bevat een overbodige
/// <c>!= ''</c> — vaak een teken van verwarring over Mendix-emptiness. CONSERVATIEF: we flaggen
/// alleen wanneer OP HETZELFDE pad ($x/Attr) ZOWEL een empty-check (<c>= empty</c>/<c>!= empty</c>)
/// ALS een lege-string-check (<c>= ''</c>/<c>!= ''</c>/<c>= ""</c>/<c>!= ""</c>) voorkomt in dezelfde
/// expressie. Een losse <c>!= ''</c> (zonder de empty-check) is GEEN violation (kan legitiem zijn);
/// een losse <c>!= empty</c> (de 396 idiomatisch-correcte) evenmin.
/// </summary>
public static class ExpressionAnalysis
{
    // Pad = $var optioneel gevolgd door /Member-keten. Vergelijkingsoperator: = of != (NIET >=,<=,
    // want die beginnen met >/< — '!?=' matcht alleen '=' of '!='). \bempty\b voor het keyword.
    private static readonly Regex EmptyCheck = new(
        @"(\$[A-Za-z_][\w/]*)\s*!?=\s*empty\b", RegexOptions.Compiled);

    // Lege-string-literal: '' (twee single quotes) of "" (twee double quotes).
    private static readonly Regex EmptyStringCheck = new(
        "(\\$[A-Za-z_][\\w/]*)\\s*!?=\\s*(?:''|\"\")", RegexOptions.Compiled);

    /// <summary>
    /// De paden waarop in deze expressie ZOWEL een empty-check ALS een lege-string-check staat
    /// (= de redundantie). Lege lijst als er geen redundantie is. Whitespace wordt genormaliseerd.
    /// </summary>
    public static IReadOnlyList<string> RedundantEmptyStringPaths(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return Array.Empty<string>();
        var norm = Regex.Replace(expression, @"\s+", " ");

        var emptyPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in EmptyCheck.Matches(norm)) emptyPaths.Add(m.Groups[1].Value);
        if (emptyPaths.Count == 0) return Array.Empty<string>();

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in EmptyStringCheck.Matches(norm))
        {
            var path = m.Groups[1].Value;
            if (emptyPaths.Contains(path) && seen.Add(path)) result.Add(path);
        }
        return result;
    }

    /// <summary>Bevat deze expressie minstens één redundante empty-string-check?</summary>
    public static bool HasRedundantEmptyStringCheck(string? expression)
        => RedundantEmptyStringPaths(expression).Count > 0;

    // Regel D: REDUNDANTE BOOLEAN-VERGELIJKING. Een operand (pad) vergeleken met de literal
    // true/false (= of !=) is overbodig: vergelijken met een boolean-literal type-checkt in Mendix
    // alléén voor een boolean operand, dus '$x = true' kan gewoon '$x' zijn (en '$x = false' →
    // 'not $x'). Conservatief: operand moet een $pad zijn (geen functie-resultaten e.d.).
    private static readonly Regex BooleanCompare = new(
        @"(?:(\$[A-Za-z_][\w/]*)\s*!?=\s*(?:true|false)\b)|(?:\b(?:true|false)\s*!?=\s*(\$[A-Za-z_][\w/]*))",
        RegexOptions.Compiled);

    /// <summary>De paden die in deze expressie redundant met true/false worden vergeleken (distinct).</summary>
    public static IReadOnlyList<string> RedundantBooleanOperands(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return Array.Empty<string>();
        var norm = Regex.Replace(expression, @"\s+", " ");
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in BooleanCompare.Matches(norm))
        {
            var operand = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (operand.Length > 0 && seen.Add(operand)) result.Add(operand);
        }
        return result;
    }

    public static bool HasRedundantBooleanComparison(string? expression)
        => RedundantBooleanOperands(expression).Count > 0;

    // INCOMPLETE EMPTY-STRING-CHECK (mxlint 005_0001, complement van REL-001). Een check als
    // <c>$x != ''</c> ZONDER een bijbehorende <c>$x != empty</c> is incompleet: '' dekt alleen de
    // truncated-string-kant, niet de database-NULL (empty). VERBATIM uit de .rego: verwijder alle
    // SPATIES (alleen ' ', net als de .rego's replace(v," ","")), dan: bevat "!=''" ÉN niet "!=empty".
    // Per-EXPRESSIE substring-check (niet per pad) — exact zoals de Rego, inclusief de grofheid dat
    // een willekeurig "!=empty" elders in dezelfde expressie 'm al "compleet" maakt.
    public static bool IsIncompleteEmptyStringCheck(string? expression)
    {
        if (string.IsNullOrEmpty(expression)) return false;
        var s = expression.Replace(" ", "");
        return s.Contains("!=''", StringComparison.Ordinal)
            && !s.Contains("!=empty", StringComparison.Ordinal);
    }
}
