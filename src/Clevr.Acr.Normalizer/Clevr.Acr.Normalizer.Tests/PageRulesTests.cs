using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

// Synthetic POSITIVE tests for the inline-style rule (mxlint 004_0001). Constructed plain object trees
// (the YAML-agnostic model the spike reader produces) prove the walk + emit logic, independent of TRB.
public class PageRulesTests
{
    private static Dictionary<string, object?> Map(params (string K, object? V)[] kv)
    {
        var d = new Dictionary<string, object?>(System.StringComparer.Ordinal);
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }
    private static List<object?> Seq(params object?[] items) => new(items);

    private static PageModel Page(object? root, string name = "MyPage", string docType = "Page")
        => new() { Module = "MyMod", Name = name, DocType = docType, Root = root };

    [Fact]
    public void Fires_OnNonEmptyStyle_Nested()
    {
        // Style buried under Appearance, deep in the widget tree.
        var root = Map(
            ("Name", "MyPage"),
            ("Widgets", Seq(
                Map(("Appearance", Map(("Style", "float:right;"))))
            )));
        var v = Assert.Single(PageRules.InlineStyleUsed(new[] { Page(root) }));
        Assert.Equal("CLEVR-MAINT-015", v.RuleId);
        Assert.Equal("Major", v.Severity);
        Assert.Equal("Maintainability", v.Category);
        Assert.Equal("MyMod.MyPage", v.DocumentQualifiedName);
        Assert.Equal("Page", v.DocumentType);
        Assert.Contains("float:right;", v.Reason);
    }

    [Fact]
    public void DoesNotFire_OnEmptyStyle()
    {
        var root = Map(("Name", "MyPage"), ("Appearance", Map(("Style", ""))));
        Assert.Empty(PageRules.InlineStyleUsed(new[] { Page(root) }));
    }

    [Fact]
    public void DoesNotFire_WhenStyleAbsent()
    {
        var root = Map(("Name", "MyPage"), ("Appearance", Map(("Class", "btn"))));
        Assert.Empty(PageRules.InlineStyleUsed(new[] { Page(root) }));
    }

    [Fact]
    public void DoesNotMatch_KeyContainingStyleButNotExactly()
    {
        // "DynamicClasses" / "MyStyle" must NOT match — the .rego requires the last key == exactly "Style".
        var root = Map(("Name", "MyPage"), ("MyStyle", "x"), ("StyleClass", "y"), ("DynamicClasses", "z"));
        Assert.Empty(PageRules.InlineStyleUsed(new[] { Page(root) }));
    }

    [Fact]
    public void DedupsIdenticalValuesPerPage_DistinctValuesKept()
    {
        // Rego set-of-error-strings semantics: identical (Name, value) collapse to ONE finding; two
        // DIFFERENT values → two findings. (This is why TRB's mxlint twin reports 33, not 86.)
        var sameValueTwice = Map(
            ("Name", "MyPage"),
            ("A", Map(("Style", "margin:1px;"))),
            ("B", Map(("Style", "margin:1px;"))));
        Assert.Single(PageRules.InlineStyleUsed(new[] { Page(sameValueTwice) }));

        var twoDistinctValues = Map(
            ("Name", "MyPage"),
            ("A", Map(("Style", "margin:1px;"))),
            ("B", Map(("Style", "float:right;"))));
        var vs = PageRules.InlineStyleUsed(new[] { Page(twoDistinctValues) });
        Assert.Equal(2, vs.Count);
        Assert.Equal(2, vs.Select(v => v.Fingerprint).Distinct().Count());
    }

    [Fact]
    public void Snippet_DocTypeReflected()
    {
        var root = Map(("Name", "MySnip"), ("Appearance", Map(("Style", "display:none;"))));
        var v = Assert.Single(PageRules.InlineStyleUsed(new[] { Page(root, "MySnip", "Snippet") }));
        Assert.Equal("Snippet", v.DocumentType);
    }

    [Fact]
    public void ClaimTable_SuppressesMxlintTwin()
        => Assert.Contains("004_0001", ClaimTable.SuppressedMxlint);

    // ===== 004_0002 ImagesWithAltText (CLEVR-REL-003) — mirrors the .rego's own test fixtures =====

    // An image CustomWidget: property 1 = the fullImage marker; property 2 (optional) carries the
    // translation. altText=null → no Text key (variation_1); altText="" or text → Text key present.
    private static Dictionary<string, object?> ImageWidget(string name, bool withTranslationProp, string? altText)
    {
        var fullImageProp = Map(("$Type", "CustomWidgets$WidgetProperty"),
            ("Value", Map(("$Type", "CustomWidgets$WidgetValue"), ("PrimitiveValue", "fullImage"), ("TextTemplate", null))));

        var props = new List<object?> { fullImageProp };
        if (withTranslationProp)
        {
            var translation = altText is null
                ? Map(("$Type", "Texts$Translation"), ("LanguageCode", "en_US"))                 // no Text key
                : Map(("$Type", "Texts$Translation"), ("LanguageCode", "en_US"), ("Text", altText)); // Text set
            props.Add(Map(("$Type", "CustomWidgets$WidgetProperty"),
                ("Value", Map(("$Type", "CustomWidgets$WidgetValue"), ("PrimitiveValue", ""),
                    ("TextTemplate", Map(("$Type", "Forms$ClientTemplate"),
                        ("Template", Map(("$Type", "Texts$Text"), ("Items", Seq(translation))))))))));
        }
        return Map(("$Type", "CustomWidgets$CustomWidget"), ("Name", name),
            ("Object", Map(("$Type", "CustomWidgets$WidgetObject"), ("Properties", props))));
    }

    private static object? PageWith(params object?[] widgets)
        => Map(("Name", "MyPage"), ("Widgets", Seq(widgets)));

    [Fact]
    public void AltText_Fires_WhenImageHasNoTranslationProp() // variation_2
    {
        var v = Assert.Single(PageRules.ImagesWithoutAltText(new[] { Page(PageWith(ImageWidget("image1", withTranslationProp: false, altText: null))) }));
        Assert.Equal("CLEVR-REL-003", v.RuleId);
        Assert.Equal("Major", v.Severity);
        Assert.Equal("Reliability", v.Category);
        Assert.Equal("image1", v.ElementName);
        Assert.Equal("MyMod.MyPage", v.DocumentQualifiedName);
    }

    [Fact]
    public void AltText_Fires_WhenTranslationHasNoTextKey() // variation_1
        => Assert.Single(PageRules.ImagesWithoutAltText(new[] { Page(PageWith(ImageWidget("image1", withTranslationProp: true, altText: null))) }));

    [Fact]
    public void AltText_DoesNotFire_WhenTranslationHasText() // allow
        => Assert.Empty(PageRules.ImagesWithoutAltText(new[] { Page(PageWith(ImageWidget("image1", withTranslationProp: true, altText: "a description of the image"))) }));

    [Fact]
    public void AltText_DoesNotFire_WhenTextKeyPresentButEmpty()
        // Verbatim: the Rego counts Text:"" as "set" (truthy/defined) → only an ABSENT Text key fires.
        => Assert.Empty(PageRules.ImagesWithoutAltText(new[] { Page(PageWith(ImageWidget("image1", withTranslationProp: true, altText: ""))) }));

    [Fact]
    public void AltText_DoesNotFire_WhenNotAnImageWidget()
    {
        // A CustomWidget whose property is not a fullImage → not an image → never flagged.
        var nonImage = Map(("$Type", "CustomWidgets$CustomWidget"), ("Name", "textbox"),
            ("Object", Map(("$Type", "CustomWidgets$WidgetObject"),
                ("Properties", Seq(Map(("$Type", "CustomWidgets$WidgetProperty"),
                    ("Value", Map(("$Type", "CustomWidgets$WidgetValue"), ("PrimitiveValue", "default")))))))));
        Assert.Empty(PageRules.ImagesWithoutAltText(new[] { Page(PageWith(nonImage)) }));
    }

    [Fact]
    public void AltText_DedupsSameWidgetNamePerPage()
    {
        // Two image widgets with the same Name both missing alt → one finding (Rego set-of-strings).
        var img = ImageWidget("image1", withTranslationProp: false, altText: null);
        var img2 = ImageWidget("image1", withTranslationProp: false, altText: null);
        Assert.Single(PageRules.ImagesWithoutAltText(new[] { Page(PageWith(img, img2)) }));
    }

    [Fact]
    public void AltText_ClaimTable_SuppressesMxlintTwin()
        => Assert.Contains("004_0002", ClaimTable.SuppressedMxlint);
}
