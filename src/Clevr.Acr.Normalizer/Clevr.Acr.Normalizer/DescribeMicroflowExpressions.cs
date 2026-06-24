namespace Clevr.Acr.Normalizer;

/// <summary>
/// PURE extractor voor de mxcli-DESCRIBE-route. <c>mxcli describe microflow</c> levert de volledige
/// microflow als MDL-tekst (control-flow + expressies, compleet — geen drops). Deze klasse zet die
/// MDL om naar (a) complete logische STATEMENTS en (b) split-CONDITIES met hun caption, zodat de
/// BESTAANDE, bewezen expressie-regels er ongewijzigd op kunnen draaien (geen nieuwe regel-logica):
///   - REL-002 (incomplete empty-string)  → <see cref="ExpressionRules.IncompleteEmptyStringCheck"/>
///   - MAINT-009 (nested-if)               → <see cref="MicroflowStructureRules.NestedIfStatements"/>
///
/// KERNFIX (FASE 2): de describe-MDL WRAPT lange condities over meerdere regels, bv.
///   <c>if $X != empty</c> / <c>and</c> / <c>$X != '' then</c>
/// Een naïeve per-regel-lezing zag <c>$X != ''</c> los → vals "incomplete". De assembler hieronder
/// voegt gewrapte regels samen tot één statement (tot een afsluitende <c>;</c> of een kale <c> then</c>)
/// vóór het predicaat draait, zodat een complete <c>!= empty and != ''</c>-check ook als compleet telt.
/// </summary>
public static class DescribeMicroflowExpressions
{
    /// <summary>Eén geassembleerde MDL-statement: volledige tekst, of het een split-header is, en de caption.</summary>
    public readonly record struct Statement(string Text, bool IsSplit, string Caption);

    /// <summary>
    /// Loopt de describe-MDL af en levert complete logische statements. Decoratie (@position/@anchor/
    /// @caption/comments) en structuur (begin/else/end if;) tellen niet als statement; de meest recente
    /// @caption wordt onthouden en aan de eerstvolgende split gekoppeld. Multi-line-wraps worden samengevoegd.
    /// </summary>
    public static IEnumerable<Statement> Assemble(string? mdl)
    {
        if (string.IsNullOrEmpty(mdl)) yield break;

        var inBody = false;
        var buf = "";
        var isSplit = false;
        var caption = "";

        foreach (var raw in mdl.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (!inBody)
            {
                if (line == "begin") inBody = true;
                continue;
            }
            if (buf.Length == 0 && line == "end;") break; // einde microflow-body (buiten een statement)

            // decoratie wordt nooit deel van een expressie.
            if (line.StartsWith("@", System.StringComparison.Ordinal))
            {
                if (buf.Length == 0 && line.StartsWith("@caption", System.StringComparison.Ordinal))
                    caption = ParseCaption(line);
                continue;
            }
            if (line.StartsWith("/", System.StringComparison.Ordinal) || line.StartsWith("*", System.StringComparison.Ordinal))
                continue;

            if (buf.Length == 0)
            {
                // pure structuur-regels (alleen tussen statements) overslaan.
                if (line == "else" || line.StartsWith("end if", System.StringComparison.Ordinal)
                    || line.StartsWith("end for", System.StringComparison.Ordinal) || line == "end;")
                    continue;
                buf = line;
                isSplit = line.StartsWith("if ", System.StringComparison.Ordinal) || line.StartsWith("elseif ", System.StringComparison.Ordinal);
            }
            else
            {
                buf += " " + line;
            }

            if (EndsStatement(buf))
            {
                yield return new Statement(buf, isSplit, caption);
                if (isSplit) caption = ""; // caption hoort bij deze split; reset voor de volgende
                buf = "";
                isSplit = false;
            }
        }

        if (buf.Length > 0) yield return new Statement(buf, isSplit, caption);
    }

    /// <summary>REL-002-route: complete statements als (Microflow, Expression). Het predicaat filtert.</summary>
    public static IEnumerable<(string Microflow, string Expression)> Extract(string microflowQualifiedName, string? mdl)
    {
        foreach (var s in Assemble(mdl))
            yield return (microflowQualifiedName, s.Text);
    }

    /// <summary>
    /// MAINT-009-route: per split de (Microflow, Caption, Condition). Condition = de statement-tekst
    /// zonder de omhullende <c>if</c>/<c>elseif</c> + <c> then</c>, zodat de bestaande nested-if-regex
    /// op exact de conditie-expressie draait (zoals de YAML-route).
    /// </summary>
    public static IEnumerable<(string Microflow, string Caption, string Expression)> ExtractSplits(string microflowQualifiedName, string? mdl)
    {
        foreach (var s in Assemble(mdl))
        {
            if (!s.IsSplit) continue;
            var cond = s.Text;
            if (cond.StartsWith("if ", System.StringComparison.Ordinal)) cond = cond[3..];
            else if (cond.StartsWith("elseif ", System.StringComparison.Ordinal)) cond = cond[7..];
            if (cond.EndsWith(" then", System.StringComparison.Ordinal)) cond = cond[..^5];
            cond = cond.Trim();
            yield return (microflowQualifiedName, s.Caption, cond);
        }
    }

    /// <summary>
    /// Structuur-tellingen voor MAINT-008 uit de describe-MDL (NIEUWE describe-metriek, bewust anders
    /// dan de oude YAML-telling): Actions = niet-split-statements, Splits = split-headers (IsSplit),
    /// Annotations = aantal <c>@annotation</c>-regels. Hergebruikt de assembler voor actions/splits.
    /// </summary>
    public static (int Actions, int Splits, int Annotations) StructureCounts(string? mdl)
    {
        var actions = 0;
        var splits = 0;
        foreach (var s in Assemble(mdl))
        {
            if (s.IsSplit) splits++; else actions++;
        }

        var annotations = 0;
        if (!string.IsNullOrEmpty(mdl))
            foreach (var raw in mdl.Replace("\r", "").Split('\n'))
                if (raw.TrimStart().StartsWith("@annotation", System.StringComparison.Ordinal))
                    annotations++;

        return (actions, splits, annotations);
    }

    /// <summary>Een statement eindigt op ';' of op een KALE ' then' (laatste token == "then" = split-header).</summary>
    private static bool EndsStatement(string buf)
    {
        var t = buf.TrimEnd();
        if (t.EndsWith(";", System.StringComparison.Ordinal)) return true;
        return t.EndsWith(" then", System.StringComparison.Ordinal) || t == "then";
    }

    private static string ParseCaption(string captionLine)
    {
        var a = captionLine.IndexOf('\'');
        if (a < 0) return "";
        var b = captionLine.LastIndexOf('\'');
        return b > a ? captionLine[(a + 1)..b] : "";
    }
}
