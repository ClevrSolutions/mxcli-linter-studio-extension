using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

// Synthetic tests for the mxcli-CATALOG route (the 7 migrated robust rules). They prove the pure rule
// logic over typed catalog rows; the live SQL outcome is verified separately against TRB.
public class CatalogRulesTests
{
    // ── MAINT-007 microflow size (ActivityCount > 25) ──
    [Fact]
    public void MicroflowSize_FlagsOver25_AcceptsMxcliMetric()
    {
        var mfs = new[]
        {
            new CatalogRules.Microflow("M.Big", "M", 26),
            new CatalogRules.Microflow("M.Edge", "M", 25),   // not > 25
            new CatalogRules.Microflow("M.Huge", "M", 103),
        };
        var v = CatalogRules.MicroflowSize(mfs);
        Assert.Equal(2, v.Count);
        Assert.All(v, x => Assert.Equal("CLEVR-MAINT-007", x.RuleId));
        Assert.All(v, x => Assert.Equal("Maintainability", x.Category));
        Assert.All(v, x => Assert.Equal("Major", x.Severity));
    }

    // ── MAINT-010 default value (incl. implicit) ──
    [Fact]
    public void AttributeDefaultValues_FlagsNonEmpty_NotEmptyOrNull()
    {
        var attrs = new[]
        {
            new CatalogRules.Attribute("M.E", "M", "WithDefault", "false", false),
            new CatalogRules.Attribute("M.E", "M", "NoDefault", "", false),
            new CatalogRules.Attribute("M.E", "M", "NullDefault", null, false),
        };
        var v = Assert.Single(CatalogRules.AttributeDefaultValues(attrs));
        Assert.Equal("CLEVR-MAINT-010", v.RuleId);
        Assert.Equal("WithDefault", v.ElementName);
        Assert.Equal("M.E", v.DocumentQualifiedName);
    }

    // ── MAINT-014 module count (> 20 user modules) ──
    [Fact]
    public void ModuleCount_FlagsOver20UserModules_AppStoreExcluded()
    {
        var mods = new List<CatalogRules.Module>();
        for (int i = 0; i < 21; i++) mods.Add(new CatalogRules.Module($"User{i}", null));
        for (int i = 0; i < 10; i++) mods.Add(new CatalogRules.Module($"Store{i}", "Marketplace v1.0"));
        var v = Assert.Single(CatalogRules.ModuleCount(mods));
        Assert.Equal("CLEVR-MAINT-014", v.RuleId);
        Assert.Contains("21 user modules", v.Reason);
    }

    [Fact]
    public void ModuleCount_NotFlaggedAtThreshold()
    {
        var mods = new List<CatalogRules.Module>();
        for (int i = 0; i < 20; i++) mods.Add(new CatalogRules.Module($"User{i}", ""));
        for (int i = 0; i < 50; i++) mods.Add(new CatalogRules.Module($"Store{i}", "Marketplace v1.0"));
        Assert.Empty(CatalogRules.ModuleCount(mods));
    }

    // ── SEC-011 exposed constants (reuses ConstantRules sensitive-name logic) ──
    [Fact]
    public void ExposedConstants_FlagsExposedSensitive_NotInnocentNorUnexposed()
    {
        var cs = new[]
        {
            new CatalogRules.Constant("M.ApiPassword", "M", "ApiPassword", true),   // exposed + sensitive → fire
            new CatalogRules.Constant("M.MaxDays", "M", "MaxDays", true),            // exposed, innocent → no
            new CatalogRules.Constant("M.AdminSecret", "M", "AdminSecret", false),   // sensitive, not exposed → no
        };
        var v = Assert.Single(CatalogRules.ExposedConstants(cs));
        Assert.Equal("CLEVR-SEC-011", v.RuleId);
        Assert.Equal("M.ApiPassword", v.DocumentQualifiedName);
    }

    // ── PERF-001 inherit Administration.Account ──
    [Fact]
    public void InheritAdmin_FlagsOnlyAdministrationAccount()
    {
        var ents = new[]
        {
            new CatalogRules.Entity("M.Acc", "M", "Administration.Account"),
            new CatalogRules.Entity("M.Usr", "M", "System.User"),
            new CatalogRules.Entity("M.Plain", "M", null),
        };
        var v = Assert.Single(CatalogRules.InheritAdmin(ents));
        Assert.Equal("CLEVR-PERF-001", v.RuleId);
        Assert.Equal("M.Acc", v.DocumentQualifiedName);
    }

    // ── SEC-007 system association, scoped to user modules ──
    [Fact]
    public void SystemAssociations_FlagsUserModuleOnly_NotAppStoreNorSystem()
    {
        var assocs = new[]
        {
            new CatalogRules.Association("TRB.Groep_UserRole", "TRB", "Groep_UserRole", "System.UserRole"),       // user module → fire
            new CatalogRules.Association("SAML20.X_Session", "SAML20", "X_Session", "System.Session"),            // app-store → no
            new CatalogRules.Association("System.A_B", "System", "A_B", "System.User"),                           // System itself → no
            new CatalogRules.Association("TRB.Plain_Assoc", "TRB", "Plain_Assoc", "TRB.Other"),                   // not System → no
        };
        var userModules = new HashSet<string>(System.StringComparer.Ordinal) { "TRB" };
        var v = Assert.Single(CatalogRules.SystemAssociations(assocs, userModules));
        Assert.Equal("CLEVR-SEC-007", v.RuleId);
        Assert.Equal("Critical", v.Severity);
        Assert.Equal("Groep_UserRole", v.ElementName);
        Assert.Equal("TRB", v.DocumentQualifiedName);
    }

    // ── SEC-009 hash algorithm ──
    [Theory]
    [InlineData("BCrypt")]
    [InlineData("SSHA256")]
    [InlineData("")]
    public void HashAlgorithm_SafeOrAbsent_NotFlagged(string alg) => Assert.Empty(CatalogRules.HashAlgorithm(alg));

    [Fact]
    public void HashAlgorithm_WeakFlagged()
    {
        var v = Assert.Single(CatalogRules.HashAlgorithm("MD5"));
        Assert.Equal("CLEVR-SEC-009", v.RuleId);
        Assert.Equal("MD5", v.ElementName);
    }

    // ── SEC-005 anon create on persistent (PERMISSIONS CREATE + persistent + anon role) ──
    [Fact]
    public void AnonymousCreate_FlagsPersistentOnly_ByAnonRole()
    {
        var anon = new HashSet<string>(System.StringComparer.Ordinal) { "M.Guest" };
        var perms = new[]
        {
            new CatalogRules.Permission("M.Guest", "CREATE", "M.PersistEnt", ""),     // anon create persistent → fire
            new CatalogRules.Permission("M.Guest", "CREATE", "M.NonPersist", ""),     // anon create non-persistent → no
            new CatalogRules.Permission("M.Admin", "CREATE", "M.PersistEnt2", ""),    // non-anon → no
        };
        var persistent = new HashSet<string>(System.StringComparer.Ordinal) { "M.PersistEnt", "M.PersistEnt2" };
        var v = Assert.Single(CatalogRules.AnonymousCreateOnPersistent(anon, perms, persistent));
        Assert.Equal("CLEVR-SEC-005", v.RuleId);
        Assert.Equal("M.PersistEnt", v.DocumentQualifiedName);
    }

    [Fact]
    public void AnonymousCreate_EmptyAnonSet_NoFindings()
        => Assert.Empty(CatalogRules.AnonymousCreateOnPersistent(
            new HashSet<string>(), new[] { new CatalogRules.Permission("M.Guest", "CREATE", "M.E", "") },
            new HashSet<string>(System.StringComparer.Ordinal) { "M.E" }));

    // ── SEC-006 anon-editable unlimited string (PERMISSIONS MEMBER_WRITE + unlimited set + anon) ──
    [Fact]
    public void AnonymousEditUnlimited_FlagsUnlimitedMemberWrite_NotLimited()
    {
        var anon = new HashSet<string>(System.StringComparer.Ordinal) { "M.Guest" };
        var perms = new[]
        {
            new CatalogRules.Permission("M.Guest", "MEMBER_WRITE", "M.Ent", "M.Ent.UnlimitedStr"), // unlimited → fire
            new CatalogRules.Permission("M.Guest", "MEMBER_WRITE", "M.Ent", "M.Ent.LimitedStr"),   // not in unlimited set → no
            new CatalogRules.Permission("M.Guest", "MEMBER_READ", "M.Ent", "M.Ent.UnlimitedStr"),  // read, not write → no
        };
        var unlimited = new HashSet<string>(System.StringComparer.Ordinal) { "M.Ent.UnlimitedStr" };
        var v = Assert.Single(CatalogRules.AnonymousEditableUnlimitedString(anon, perms, unlimited));
        Assert.Equal("CLEVR-SEC-006", v.RuleId);
        Assert.Equal("M.Ent", v.DocumentQualifiedName);
        Assert.Equal("UnlimitedStr", v.ElementName);
    }
}
