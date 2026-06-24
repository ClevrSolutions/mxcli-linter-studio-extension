namespace Clevr.Acr.Normalizer;

/// <summary>
/// Een geparseerde page/snippet als PLAT, YAML-agnostisch objectboom-model (geen YamlDotNet hier —
/// de normalizer blijft dependency-vrij). De spike-reader (<c>PageYamlReader</c>) zet de YAML om naar:
///   - mapping  → <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/> (string-sleutels)
///   - sequence → <see cref="System.Collections.Generic.List{T}"/>
///   - scalar   → string
/// zodat de regel-logica (de patroon-gevoelige boom-walk) hier PUUR en unit-testbaar is. Herbruikbaar:
/// 004_0002 (alt-text) loopt straks dezelfde boom af op de CustomWidget/WidgetObject/Translation-takken.
/// </summary>
public sealed class PageModel
{
    public string Module = "";
    public string Name = "";
    public string DocType = "Page"; // "Page" | "Snippet"
    public object? Root;            // Dictionary<string, object?> | List<object?> | string | null
}

/// <summary>
/// CLEVR-EIGEN regels op de PAGE/SNIPPET-export. Eerste regel: mxlint 004_0001 InlineStylePropertyUsed.
/// VERBATIM uit de .rego: <c>walk(input)</c> → elk pad waarvan de LAATSTE sleutel exact "Style" is en
/// de waarde geen lege string is (<c>v != ""</c>). LET OP de set-semantiek: de Rego's <c>errors</c> is
/// een SET van error-STRINGS, en die string is <c>sprintf(... input.Name, v)</c>. Identieke (Name, value)
/// vallen dus samen tot ÉÉN finding. We dedupliceren daarom per page op DISTINCTE style-waarde — dat is
/// precies wat de werkende mxlint-twin op TRB telt (33, niet de 86 ruwe voorkomens). MEDIUM → Major.
/// </summary>
public static class PageRules
{
    public const string Engine = "page"; // alleen debug

    public const string InlineStyleRuleId = "CLEVR-MAINT-015";
    public const string InlineStyleAcrCode = "InlineStylePropertyUsed";
    public const string InlineStyleEngineRuleKey = "CLEVR_PAGE_INLINE_STYLE";
    public const string InlineStyleCategory = "Maintainability"; // letterlijk uit de .rego
    public const string InlineStyleSeverity = "Major";           // mxlint MEDIUM → Major (voorstel)

    public static IReadOnlyList<Violation> InlineStyleUsed(IEnumerable<PageModel> pages)
    {
        var result = new List<Violation>();
        foreach (var page in pages)
        {
            if (page.Name.Length == 0) continue;
            var styles = new List<string>();
            CollectStyleValues(page.Root, styles);

            var qn = $"{page.Module}.{page.Name}";
            // Set-semantiek van de Rego: dedup per page op style-waarde (eerste-voorkomen-volgorde).
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var style in styles)
            {
                if (!seen.Add(style)) continue;
                result.Add(new Violation
                {
                    RuleId = InlineStyleRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = InlineStyleAcrCode,
                    Engine = Engine, Category = InlineStyleCategory, Severity = InlineStyleSeverity,
                    DocumentType = page.DocType, DocumentQualifiedName = qn, ElementName = "",
                    Reason = $"{page.DocType} '{page.Name}' has an inline 'Style' property with value '{style}'. Inline styles are hard to override from CSS and complicate theming.",
                    Suggestion = "Use a generic class defined by the theme instead of an inline style.",
                    Fingerprint = Fingerprint.Compute(InlineStyleRuleId, qn, style),
                });
            }
        }
        return result;
    }

    /// <summary>
    /// Loopt de objectboom af en verzamelt elke waarde onder een sleutel die exact "Style" heet en een
    /// niet-lege string is. Spiegelt <c>last == "Style"; v != ""</c> uit de .rego. (Style is in de export
    /// altijd een string-scalar; niet-string-waarden komen niet voor en worden hier niet geflagd.)
    /// </summary>
    private static void CollectStyleValues(object? node, List<string> acc)
    {
        switch (node)
        {
            case Dictionary<string, object?> map:
                foreach (var kv in map)
                {
                    if (kv.Key == "Style" && kv.Value is string s && s.Length > 0) acc.Add(s);
                    CollectStyleValues(kv.Value, acc);
                }
                break;
            case List<object?> list:
                foreach (var item in list) CollectStyleValues(item, acc);
                break;
        }
    }

    // ============================================================================
    // mxlint 004_0002 ImagesWithAltText (laatste regel — 17/17). ONTBREEKT-check: vuurt als een
    // image-widget GEEN alt-text heeft. False-positive-RICHTING is het gevaar — een gemiste
    // translation-tak zou een image die WEL alt-text heeft ten onrechte flaggen. Daarom loopt de
    // check ALLE eigen Object.Properties + ALLE Items af (exact zoals de .rego, niets gemist).
    //
    // CATEGORIE-KEUZE (knop voor Michel): de .rego is "Accessibility" — geen van onze zes ACR-
    // categorieën. Gemapt op Reliability (de app bedient álle gebruikers betrouwbaar, incl.
    // screenreaders). Eén constante om bij te stellen. mxlint MEDIUM → Major.
    // ============================================================================
    public const string AltTextRuleId = "CLEVR-REL-003";
    public const string AltTextAcrCode = "ImagesWithAltText";
    public const string AltTextEngineRuleKey = "CLEVR_PAGE_IMAGE_ALT_TEXT";
    public const string AltTextCategory = "Reliability"; // ← knop voor Michel (.rego: Accessibility)
    public const string AltTextSeverity = "Major";       // mxlint MEDIUM → Major (voorstel)

    public static IReadOnlyList<Violation> ImagesWithoutAltText(IEnumerable<PageModel> pages)
    {
        var result = new List<Violation>();
        foreach (var page in pages)
        {
            if (page.Name.Length == 0) continue;
            var missing = new List<string>(); // namen van image-widgets zónder alt-text
            CollectImagesMissingAlt(page.Root, missing);

            var qn = $"{page.Module}.{page.Name}";
            // Set-semantiek (zoals 004_0001): de Rego's error-string bevat alleen widgetName → dedup
            // per (page, widgetName).
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var widgetName in missing)
            {
                if (!seen.Add(widgetName)) continue;
                result.Add(new Violation
                {
                    RuleId = AltTextRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = AltTextAcrCode,
                    Engine = Engine, Category = AltTextCategory, Severity = AltTextSeverity,
                    DocumentType = page.DocType, DocumentQualifiedName = qn, ElementName = widgetName,
                    Reason = $"Image widget '{widgetName}' in {page.DocType.ToLowerInvariant()} '{page.Name}' has no alt text set in any translation. Screen readers cannot describe it.",
                    Suggestion = "Set an alt text (Text) for the image so screen readers can describe it.",
                    Fingerprint = Fingerprint.Compute(AltTextRuleId, qn, widgetName),
                });
            }
        }
        return result;
    }

    /// <summary>
    /// Walk (zoals de Rego's <c>walk(input, [_, widget])</c>): elk knooppunt dat een
    /// CustomWidgets$CustomWidget is, met een Object van type CustomWidgets$WidgetObject, waarvan
    /// minstens één Property een <c>Value.PrimitiveValue == "fullImage"</c> heeft (= image-widget).
    /// Vuurt als GEEN van diens EIGEN Properties' <c>Value.TextTemplate.Template.Items</c> een
    /// <c>Texts$Translation</c> met een gezette (aanwezige) <c>Text</c>-sleutel bevat.
    /// </summary>
    private static void CollectImagesMissingAlt(object? node, List<string> acc)
    {
        if (node is Dictionary<string, object?> map)
        {
            if (Str(Get(map, "$Type")) == "CustomWidgets$CustomWidget"
                && Get(map, "Object") is Dictionary<string, object?> obj
                && Str(Get(obj, "$Type")) == "CustomWidgets$WidgetObject"
                && Get(obj, "Properties") is List<object?> props)
            {
                var isImage = false;
                foreach (var p in props)
                    if (p is Dictionary<string, object?> pm
                        && Get(pm, "Value") is Dictionary<string, object?> vm
                        && Str(Get(vm, "PrimitiveValue")) == "fullImage")
                    { isImage = true; break; }

                if (isImage && !HasAltText(props))
                    acc.Add(Str(Get(map, "Name")) ?? "");
            }

            // walk de hele boom (ook geneste CustomWidgets worden zo elk apart geëvalueerd).
            foreach (var v in map.Values) CollectImagesMissingAlt(v, acc);
        }
        else if (node is List<object?> list)
        {
            foreach (var item in list) CollectImagesMissingAlt(item, acc);
        }
    }

    /// <summary>
    /// True als ENIGE eigen Property een Texts$Translation heeft met aanwezige Text-sleutel. Loopt
    /// ALLE Properties + ALLE Items af (false-positive-veilig: we missen geen tak waar de alt-text staat).
    /// "Text gezet" = de sleutel is aanwezig (verbatim: de Rego's <c>text_translation.Text</c> slaagt zodra
    /// gedefinieerd; ook "" telt als gezet — alleen een AFWEZIGE Text-sleutel = ontbrekend).
    /// </summary>
    private static bool HasAltText(List<object?> props)
    {
        foreach (var p in props)
        {
            if (p is not Dictionary<string, object?> pm) continue;
            if (Get(pm, "Value") is not Dictionary<string, object?> vm) continue;
            if (Get(vm, "TextTemplate") is not Dictionary<string, object?> tt) continue;
            if (Get(tt, "Template") is not Dictionary<string, object?> tmpl) continue;
            if (Get(tmpl, "Items") is not List<object?> items) continue;
            foreach (var it in items)
                if (it is Dictionary<string, object?> im
                    && Str(Get(im, "$Type")) == "Texts$Translation"
                    && im.ContainsKey("Text"))
                    return true;
        }
        return false;
    }

    private static object? Get(Dictionary<string, object?> map, string key)
        => map.TryGetValue(key, out var v) ? v : null;
    private static string? Str(object? o) => o as string;
}
