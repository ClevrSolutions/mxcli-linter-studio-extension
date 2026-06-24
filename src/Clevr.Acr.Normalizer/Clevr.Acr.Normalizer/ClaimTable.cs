namespace Clevr.Acr.Normalizer;

/// <summary>
/// Eén onderwerp in de cross-engine claim-tabel: welke bron WINT, en welke tegenhangers daarom
/// onderdrukt worden op BEIDE engines — mxcli-generic (op rule-id/rulename) én mxlint-Rego (op
/// rulenumber). <see cref="Impact"/> benoemt expliciet welke findings verloren gaan wanneer een
/// onderdrukte tegenhanger méér dekt dan de winnaar (bewuste keuze, niet weggepoetst).
/// </summary>
public sealed record EngineClaim(
    string Topic,
    string Winner,
    IReadOnlyList<string> SuppressMxcli,
    IReadOnlyList<string> SuppressMxlint,
    string Impact);

/// <summary>
/// Cross-engine precedentie/ontdubbeling op ONDERWERP-niveau. Vervangt de oude hardcoded
/// mxlint-denylist (die alleen de mxlint-kant raakte én 2 foute entries had — 001_0004/002_0007
/// verwezen naar niet-bestaande ACR-regels). Per onderwerp claimt één bron de check; de
/// tegenhangers worden onderdrukt in zowel <see cref="MxcliNormalizer"/> (SuppressMxcli) als
/// <see cref="MxlintNormalizer"/> (SuppressMxlint). De regel-LOGICA zelf blijft ongemoeid — dit is
/// puur de aggregatie-keuze welke bron getoond wordt.
///
/// SCOPE NU (bewijs): twee best-gemeten onderwerpen krijgen cross-engine (mxcli-)onderdrukking —
/// microflow-grootte en attribuut-telling. De drie security-onderwerpen zijn 1-op-1 GEMIGREERD uit
/// de oude denylist (mxlint-only, gedrag ongewijzigd) zodat het vervangen van de denylist die
/// overlappen niet terugbrengt. Naamgeving/guest aan de mxcli-kant en de 17 te internaliseren
/// regels volgen later, per onderwerp met goedgekeurde impact.
/// </summary>
public static class ClaimTable
{
    public static readonly IReadOnlyList<EngineClaim> Claims = new EngineClaim[]
    {
        // ── PROEF-onderwerp 1: microflow-grootte — CLEVR-MAINT-007 wint ──────────────────────────
        new("Microflow size", "CLEVR-MAINT-007",
            SuppressMxcli: new[] { "QUAL003", "CONV009" },
            SuppressMxlint: new[] { "005_0003" },
            Impact: "QUAL003 (>25 activiteiten) is een SUBSET van de winnaar → geen verlies. " +
                    "CONV009 (>15 activiteiten) is LOSSER → onderdrukken laat de microflows vallen die " +
                    "alleen CONV009 meldt (16–25 activiteiten). mxlint 005_0003 = identieke Rego-twin → geen verlies."),

        // ── GEÏNTERNALISEERD: microflow-structuur-batch (005_0002/0004/0005). Bij de bouw van MAINT-
        //    008/009 + PERF-COMMIT-IN-LOOP zijn destijds de suppressie-entries vergeten (alleen 005_0003
        //    kreeg er één) → 005_0004 dook zichtbaar dubbel op (103 naast MAINT-008). Hier hersteld. ──
        new("Complex microflow without annotations", "CLEVR-MAINT-008",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "005_0004" },
            Impact: "mxlint 005_0004 = identieke Rego-twin van CLEVR-MAINT-008 (zelfde complexiteits-/annotatie-meting; " +
                    "beide ~103 op TRB) → onderdrukken = geen verlies, heft de zichtbare dubbeling op. Geen mxcli-tegenhanger " +
                    "(mxcli kent geen annotatie-regel)."),
        new("Nested if statements", "CLEVR-MAINT-009",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "005_0005" },
            Impact: "mxlint 005_0005 = identieke Rego-twin van CLEVR-MAINT-009 → geen verlies. Zit niet in de huidige " +
                    "mxlint-pack (vuurt nu 0), maar wordt onderdrukt zodra een pack 'm wél bevat. Geen mxcli-tegenhanger."),
        // CUTOVER: gedeferd naar mxcli's EIGEN regel. Onze CLEVR-emissie staat in AcrScanService uit;
        // mxcli's regel levert de finding → NIET meer in SuppressMxcli (anders zou 't onderwerp helemaal
        // wegvallen). De mxlint-twin blijft onderdrukt (mxlint is backup; bij terugkeer defert 'ie ook naar mxcli).
        new("Commit in loop", "mxcli CONV011 (NoCommitInLoop)",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "005_0002" },
            Impact: "GEDEFERD naar mxcli CONV011 (zelfde onderwerp: commit-actie binnen een loop, N+1). Onze " +
                    "CLEVR-PERF-COMMIT-IN-LOOP-emissie is uit; CONV011 toont de finding (0 op TRB → geen dubbel, geen verlies). " +
                    "mxlint-twin 005_0002 blijft onderdrukt (backup defert ook naar mxcli)."),

        // ── PROEF-onderwerp 2: attribuut-telling — CLEVR-MAINT-001 (ACR_ENT_ATTRS) wint ─────────
        new("Entity attribute count", "CLEVR-MAINT-001 (ACR_ENT_ATTRS)",
            SuppressMxcli: new[] { "DESIGN001" },
            SuppressMxlint: new[] { "002_0002" }, // gemigreerd-correct: drempel 35 ⊂ winner (25) → geen verlies
            Impact: "DESIGN001 (>10 attrs) is STRENGER/breder → onderdrukken laat de entities vallen die " +
                    "alleen DESIGN001 meldt (11–25 attrs). mxlint 002_0002 (>35) is een subset → geen verlies."),

        // ── GEÏNTERNALISEERD: attribuut-default-waarde — CLEVR-MAINT-010 wint ───────────────────
        new("Attribute default value", "CLEVR-MAINT-010",
            SuppressMxcli: new[] { "CONV002" }, // mxcli NoEntityDefaultValues: 106 (alleen '0') ⊂ onze 283
            SuppressMxlint: new[] { "002_0009" },
            Impact: "mxlint 002_0009 = identiek (283) → geen verlies. mxcli CONV002 flagt alleen integer-'0'-" +
                    "defaults (106) — STRIKTE SUBSET van onze 283 (die ook 'false'/strings dekt) → onderdrukken = geen verlies."),

        // ── GEÏNTERNALISEERD: domein-model-batch (002_*). mxlint-tegenhangers DORMANT op Windows ──
        // CUTOVER: gedeferd naar mxcli MPR003. Onze CLEVR-MAINT-011-emissie is uit; MPR003 (NIET meer
        // onderdrukt) toont de finding. mxlint-twin 002_0001 blijft onderdrukt.
        new("Persistent entity count", "mxcli MPR003 (DomainModelSize)",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "002_0001" },
            Impact: "GEDEFERD naar mxcli MPR003 (zelfde onderwerp: >15 persistente entiteiten per module). Onze " +
                    "CLEVR-MAINT-011-emissie is uit; MPR003 toont de finding (vuurt op TRB) → precies één bron, geen dubbel. " +
                    "mxlint-twin 002_0001 blijft onderdrukt (backup defert ook naar mxcli)."),
        new("Inherit from Administration.Account", "CLEVR-PERF-001",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "002_0003" },
            Impact: "mxlint 002_0003 dormant; geen mxcli-tegenhanger die vuurt."),
        new("System entity association", "CLEVR-SEC-007",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "002_0005" },
            Impact: "mxlint 002_0005 dormant; geen mxcli-tegenhanger die vuurt."),
        // CUTOVER: gedeferd naar mxcli CONV017. Onze CLEVR-PERF-002-emissie is uit; CONV017 was al niet
        // onderdrukt → toont de finding (ELKE calculated-attr; breder dan onze >10-regel — bewust mxcli's meting).
        new("Too many virtual attributes", "mxcli CONV017 (NoCalculatedAttributes)",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "002_0006" },
            Impact: "GEDEFERD naar mxcli CONV017 (zelfde onderwerp: calculated/virtuele attributen). Onze CLEVR-PERF-002-" +
                    "emissie is uit; CONV017 toont de finding (meet ELKE calculated-attr, breder dan onze >10 — bewust mxcli's meting). " +
                    "mxlint-twin 002_0006 blijft onderdrukt."),
        // CUTOVER: gedeferd naar mxcli ACR_ENT_VALRULES/CONV015. Onze CLEVR-MAINT-012-emissie is uit.
        new("Domain validation rules", "mxcli ACR_ENT_VALRULES/CONV015",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "002_0007" },
            Impact: "GEDEFERD naar mxcli ACR_ENT_VALRULES/CONV015 (zelfde onderwerp: entity-validatieregels). Onze " +
                    "CLEVR-MAINT-012-emissie is uit; mxcli's regels tonen de finding. mxlint-twin 002_0007 blijft onderdrukt."),
        new("Default ReadWrite access", "CLEVR-MAINT-013",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "002_0008" },
            Impact: "mxlint 002_0008 dormant; mxcli CONV006/CONV007 vuren 0 + meten iets anders (create/delete, xpath) → geen onderdrukking."),
        // 002_0004 InheritFromNonSystem: BEWUST NIET geïnternaliseerd (Rego over-fires op 60 no-generalization-
        // entiteiten → 64 ruis). Niet in de claim-tabel; mxlint-twin is sowieso dormant.

        // ── GEÏNTERNALISEERD: security-/settings-/modules-batch (001_0005/0007/0008, 003_0001) ───
        // Alle vier MXLINT-ONLY (geen mxcli/ACR-tegenhanger — STAP 0). mxlint-twins onderdrukt.
        new("Admin user id not default (MxAdmin)", "CLEVR-SEC-008",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "001_0005" },
            Impact: "mxlint 001_0005 = identieke check (AdminUserName == 'MxAdmin') → geen verlies. Geen mxcli-tegenhanger."),
        new("Hash algorithm", "CLEVR-SEC-009",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "001_0007" },
            Impact: "mxlint 001_0007 leest input.Settings.HashAlgorithm — werkt NIET op deze export (HashAlgorithm zit " +
                    "in een Settings-LIJST, niet als mapping) → vuurt 0. Onze regel vindt het veld waar het staat → geen verlies."),
        new("Check security on user roles", "CLEVR-SEC-010",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "001_0008" },
            Impact: "mxlint 001_0008 = per user-role CheckSecurity-check → identiek gereproduceerd → geen verlies. Geen mxcli-tegenhanger."),
        new("Number of modules", "CLEVR-MAINT-014",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "003_0001" },
            Impact: "mxlint 003_0001 leest input.Modules[i].Attributes.FromAppStore — werkt NIET op deze export (FromAppStore " +
                    "zit vlak onder het module-item, niet onder Attributes) → telt 0 user-modules. Onze regel = item zónder " +
                    "'FromAppStore: true' → correcte telling, geen verlies."),

        // ── GEÏNTERNALISEERD: exposed constants met gevoelige naam — CLEVR-SEC-011 wint ─────────
        new("Exposed constants with sensitive data", "CLEVR-SEC-011",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "006_0001" },
            Impact: "mxlint 006_0001 heeft TWEE branches: (1) ELKE exposed constant = MEDIUM, (2) exposed + " +
                    "gevoelige naam = HIGH. CLEVR-SEC-011 reproduceert branch (2) VERBATIM (zelfde keyword-substring-" +
                    "logica) → geen verlies op het sensitive-onderwerp. Onderdrukken laat WEL de blanket-MEDIUM-branch " +
                    "vallen (élke exposed constant, ook met onschuldige naam) — bewuste keuze: die branch is ruis " +
                    "(flagt alle exposed constants). Geen mxcli-tegenhanger (STAP 0). NB: op TRB vuren beide branches 0."),

        // ── GEÏNTERNALISEERD: images zonder alt-text — CLEVR-REL-003 wint (LAATSTE: 17/17) ──────
        new("Images must have alt text", "CLEVR-REL-003",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "004_0002" },
            Impact: "mxlint 004_0002 draait CORRECT (testcases=113, 0 findings — TRB heeft geen CustomWidget/" +
                    "fullImage). CLEVR-REL-003 reproduceert de Rego verbatim (fullImage-widget zonder Texts$Translation " +
                    "met gezette Text) → 0 verlies. Categorie Accessibility→Reliability (geen ACR-bucket). Geen mxcli-tegenhanger."),

        // ── GEÏNTERNALISEERD: inline style-property — CLEVR-MAINT-015 wint ──────────────────────
        new("Inline style property used", "CLEVR-MAINT-015",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "004_0001" },
            Impact: "mxlint 004_0001 draait hier CORRECT (testcases=113, 33 findings over 14 pages) — geldige " +
                    "kruischeck. CLEVR-MAINT-015 reproduceert de Rego verbatim (walk → key 'Style', v != '') → 0 verlies. " +
                    "Geen mxcli-tegenhanger (STAP 0)."),

        // ── GEÏNTERNALISEERD: incomplete empty-string-check — CLEVR-REL-002 wint ────────────────
        new("Incomplete empty-string check", "CLEVR-REL-002",
            SuppressMxcli: System.Array.Empty<string>(), // GEEN mxcli-tegenhanger (bevestigd in de 60-regel-meting)
            SuppressMxlint: new[] { "005_0001" },
            Impact: "Alleen de mxlint-kant: REL-002 reproduceert 005_0001 verbatim (zelfde substring-logica) → geen verlies."),

        // ── GEMIGREERD uit de oude denylist (mxlint-only; gedrag ONGEWIJZIGD) ───────────────────
        new("Guest/anonymous access", "CLEVR-SEC-004 (ACR_SEC_GUEST)",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "001_0001" },
            Impact: "Gemigreerd uit de denylist; mxlint-only, ongewijzigd. (mxcli SEC004 nog niet onderdrukt — later.)"),
        new("Demo users", "CLEVR-SEC-003 (ACR_SEC_DEMOUSERS)",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "001_0002" },
            Impact: "Gemigreerd; mxlint-only, ongewijzigd."),
        new("Security checks/level", "CLEVR-SEC-001/002 (ACR_SEC_CHECKED/LEVEL)",
            SuppressMxcli: System.Array.Empty<string>(), SuppressMxlint: new[] { "001_0003" },
            Impact: "Gemigreerd; mxlint-only, ongewijzigd."),

        // NB: oude denylist-entries 001_0004 (StrongPasswordPolicy) en 002_0007 (AvoidUsingValidationRules)
        // zijn BEWUST NIET gemigreerd — we claimen geen ACR_SEC_PWPOLICY / ACR_ENT_VALRULES, dus die
        // mxlint-regels horen te tonen. Ze verschijnen nu weer (zoals bedoeld).
    };

    /// <summary>mxcli rule-ids (rulenames) die onderdrukt worden — voor <see cref="MxcliNormalizer"/>.</summary>
    public static readonly IReadOnlySet<string> SuppressedMxcli =
        Claims.SelectMany(c => c.SuppressMxcli).ToHashSet(StringComparer.Ordinal);

    /// <summary>mxlint rulenumbers die onderdrukt worden — voor <see cref="MxlintNormalizer"/>.</summary>
    public static readonly IReadOnlySet<string> SuppressedMxlint =
        Claims.SelectMany(c => c.SuppressMxlint).ToHashSet(StringComparer.Ordinal);
}
