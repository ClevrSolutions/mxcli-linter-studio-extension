// CLEVR ACR — web-kant van de C#-gehoste pane.
//
// Twee lagen, gescheiden zoals in Fase 1:
//  (1) RENDER-LAAG: pure presentatie. renderReport(root, violations, query) bouwt de
//      ACR-layout uit een Violation[]-array en weet NIET waar die vandaan komt.
//  (2) MESSAGE-BUS GLUE: ontvangt de violations van de C#-backend en rendert.
//
// ALLE improvements (ACR + generiek) staan in de zes ACR-categorieën. Generieke regels
// worden voor de WEERGAVE in een ACR-categorie geplaatst via GENERIC_CATEGORY_MAP
// (spec sectie 5); het interne Violation-type blijft ongewijzigd (category = engine-prefix).
// UI-tekst gebruikt consequent "Improvements"; de herkomst blijft per regel + in de
// telkaart zichtbaar (ACR / MxCLI Mxlint / Manual). De mxlint.com-Rego-bron is uit de UI
// verwijderd (alle regels geïnternaliseerd); de source==="mxlint"-takken hieronder zijn dode,
// onschadelijke guards (de engine levert geen mxlint-violations meer).

// ---------------------------------------------------------------- render-laag

const ACR_CATEGORIES = [
  "Project hygiene", "Maintainability", "Performance", "Architecture", "Reliability", "Security",
];

// Severity-weergavevolgorde: ACR-severities, dan mxcli engine-severities, dan overig.
const SEVERITY_ORDER = ["Minor", "Major", "Critical", "Blocker", "error", "warning", "info", "hint"];

// MANUAL CHECKS — controlevragen die de developer zelf beantwoordt (geen model-violation).
// Generiek + uitbreidbaar: voeg hier definities toe. Gedrag: een open/verlopen check verschijnt
// als normale improvement in z'n categorie; een geldig "ja" (binnen 30 dagen) verdwijnt en moet
// daarna opnieuw beantwoord worden (recheck). Antwoorden komen van C# (manual-checks.json).
const MANUAL_CHECK_EXPIRY_DAYS = 30;
const MANUAL_CHECKS = [
  {
    id: "MC-PERF-RECOMMENDER",
    category: "Performance",
    severity: "Major",
    question: "Have you reviewed the Best Practice Recommender (Performance) in Studio Pro and resolved or consciously assessed the relevant findings?",
    context: "The Best Practice Recommender currently covers performance only and is not machine-readable via mxcli or the Extensibility API, so it cannot be checked automatically — hence this manual check.",
  },
];

// DISPLAY-mapping (spec sectie 5): de ECHTE per-regel mxcli-categorie → ACR-categorie.
// mxcli geeft per regel een categorie (style/quality/correctness/performance/...) via
// `lint --list-rules`; die is veel preciezer dan de ruleId-prefix. Verantwoorde tabel:
const MXCLI_CATEGORY_TO_ACR = {
  security: "Security",
  naming: "Project hygiene",
  style: "Project hygiene",
  quality: "Maintainability",
  complexity: "Maintainability",
  maintainability: "Maintainability",
  design: "Architecture",
  architecture: "Architecture",
  correctness: "Reliability", // bewuste vertaling: runtime-correctheid ≈ betrouwbaarheid
  performance: "Performance",
};
const GENERIC_CATEGORY_FALLBACK = "Maintainability";

// Fallback-mapping op ruleId-prefix, alleen gebruikt als de mxcli-categorie ontbreekt
// (bv. --list-rules kon niet geladen worden). De prefix is grof, vandaar enkel fallback.
const GENERIC_PREFIX_FALLBACK = {
  SEC: "Security", ARCH: "Architecture", DESIGN: "Architecture", PERF: "Performance",
};

// mxlint (Rego) levert z'n eigen categorie letterlijk (spec §5). Map naar ACR;
// Accessibility → Maintainability (bewuste keuze). Sleutel = lowercase categorie.
const MXLINT_CATEGORY_TO_ACR = {
  security: "Security",
  maintainability: "Maintainability",
  performance: "Performance",
  accessibility: "Maintainability",
  microflows: "Maintainability",
  complexity: "Maintainability",
  error: "Reliability",
};

// Toon-categorie: ACR-regels houden hun registry-categorie; mxcli-generiek mapt op de
// mxcli-categorie (lastRuleCategories); mxlint mapt op z'n eigen message-categorie.
function displayCategory(v) {
  if (v.kind === "manual") return v.category; // manual check: z'n eigen ACR-categorie
  if (v.kind === "acr") return v.category;
  if (originOf(v) === "mxlint") {
    return MXLINT_CATEGORY_TO_ACR[(v.category || "").toLowerCase()] || GENERIC_CATEGORY_FALLBACK;
  }
  const mxcliCat = (lastRuleCategories[v.ruleId] || "").toLowerCase();
  if (mxcliCat && MXCLI_CATEGORY_TO_ACR[mxcliCat]) return MXCLI_CATEGORY_TO_ACR[mxcliCat];
  return GENERIC_PREFIX_FALLBACK[v.category] || GENERIC_CATEGORY_FALLBACK;
}

// Herkomst-label (voor de telkaart-uitsplitsing).
function originLabel(v) {
  if (v.kind === "manual") return "Manual checks";
  if (v.kind === "acr") return "ACR (calibrated)";
  if (v.source === "mxlint") return "Mxlint.com";
  return "MxCLI Mxlint"; // source === "mxcli"
}

// Korte badge-tekst per regel.
function originBadge(v) {
  if (v.kind === "manual") return "Manual";
  if (v.kind === "acr") return "ACR";
  if (v.source === "mxlint") return "Mxlint.com";
  return "MxCLI";
}

// Herkomst-sleutel voor de filter (acr / mxcli / mxlint / manual).
function originOf(v) {
  if (v.kind === "manual") return "manual";
  if (v.kind === "acr") return "acr";
  if (v.source === "mxlint") return "mxlint";
  return "mxcli";
}

// System-module = niet wijzigbaar door een developer → ruis. We verbergen die volledig
// in de WEERGAVE (lijst, alle telkaarten, totaal én het geëxporteerde rapport). De
// onderliggende Violation-data blijft compleet (data/UI-scheiding); we filteren puur voor
// de weergave, net als displayCategory en de bestaande filters. Bepaald op de module van de
// violation: de qualified name is "Module.Document", dus prefix "System." (of exact "System").
// De punt voorkomt een false-match op modules zoals "SystemX".
function isSystemModule(v) {
  const qn = v.documentQualifiedName || "";
  return qn === "System" || qn.startsWith("System.");
}

// App-store/marketplace-modules (DEEL 3): de C#-scan levert de set marktplaats-modulenamen mee
// (CATALOG.MODULES.Source niet leeg — zelfde mechanisme als FASE 1). Anders dan het System-filter
// (harde verberging) is dit een GEBRUIKERS-TOGGLE: standaard tonen, de gebruiker kan ze verbergen.
// De findings worden NIET gedempt — puur een weergave-toggle. Module = prefix vóór de eerste '.'.
let lastAppStoreModules = new Set();
let appStoreVisible = true; // default: tonen (we scannen alles; de gebruiker kiest)
let lastDeepScan = true;    // of de laatste scan de describe-route bevatte; default true = geen hint vóór een scan

// ── STREAMING-staat (deepscan druppelt findings binnen in batches) ──────────────────────────────
// De scan post findings in fases: eerst de FAST-batch (lint+catalog+security), daarna — bij deepscan —
// describe-findings per chunk. Zolang de scan loopt MOET glashelder zijn dat de tellingen tussentijds &
// onvolledig zijn (geen stille onvolledigheid): een voortgangsbanner + "partial"-markering op de totalen,
// tot de batch met final=true. scanIncomplete = een chunk gaf minder findings terug dan gevraagd (LUID).
let scanStreaming = false;
let scanProgress = null;   // { processed, total, label } van de laatste describe-batch
let scanIncomplete = false;
let scanStartMs = 0;       // scan-start (voor de getoonde duur bij de laatste batch)
function moduleOf(v) {
  const qn = v.documentQualifiedName || "";
  const dot = qn.indexOf(".");
  return dot >= 0 ? qn.slice(0, dot) : qn;
}
function isAppStoreModule(v) {
  return lastAppStoreModules.has(moduleOf(v));
}

// De voor de WEERGAVE zichtbare verzameling: alles behalve de System-module. ALLE
// render-/tel-/rapportpaden gebruiken deze i.p.v. lastViolations, zodat het filter overal
// consistent doorwerkt. De bestaande filters (categorie/severity/herkomst/tekst) en de
// reset-knop werken hier bovenop (zie passesFilters/rerender).
function baseViolations() {
  return allDisplayViolations()
    .filter((v) => !isSystemModule(v))
    .filter((v) => appStoreVisible || !isAppStoreModule(v)); // marktplaats-toggle (default tonen)
}

// De volledige set die de UI toont = engine-violations + OPEN manual checks (als synthetische
// improvements). Zo lopen open manual checks vanzelf door de hele pipeline (categorie,
// tellingen, filters, rapport, Exclude, Ask-Maia).
function allDisplayViolations() {
  return lastViolations.concat(openManualCheckViolations());
}

// ---- Manual checks: verloop-/state-logica (render-laag; de antwoorden komen van C#).
function daysSince(dateStr) {
  if (!dateStr) return Infinity;
  const then = new Date(dateStr + "T00:00:00");
  if (isNaN(then.getTime())) return Infinity;
  return Math.floor((new Date() - then) / 86400000);
}
function addDays(dateStr, n) {
  const d = new Date(dateStr + "T00:00:00");
  if (isNaN(d.getTime())) return dateStr;
  d.setDate(d.getDate() + n);
  // Lokale datum-componenten (NIET toISOString → die shift naar UTC = soms een dag eraf).
  const y = d.getFullYear(), m = String(d.getMonth() + 1).padStart(2, "0"), day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

// State per definitie: unanswered / no / expired (verlopen "ja") → OPEN; valid-yes → verdwenen.
function manualCheckState(def) {
  const answer = manualAnswers.find((a) => a.id === def.id) || null;
  if (!answer) return { def, answer: null, status: "unanswered", open: true };
  if ((answer.answer || "").toLowerCase() === "no") return { def, answer, status: "no", open: true };
  // "yes": geldig binnen 30 dagen, anders verlopen.
  const days = daysSince(answer.date);
  const recheckDate = addDays(answer.date, MANUAL_CHECK_EXPIRY_DAYS);
  if (days >= MANUAL_CHECK_EXPIRY_DAYS) return { def, answer, status: "expired", open: true, recheckDate, daysOverdue: days - MANUAL_CHECK_EXPIRY_DAYS };
  return { def, answer, status: "valid-yes", open: false, recheckDate, recheckInDays: MANUAL_CHECK_EXPIRY_DAYS - days };
}
function manualChecksAll() { return MANUAL_CHECKS.map(manualCheckState); }
function answeredManualChecks() { return manualChecksAll().filter((s) => !s.open); }

// Korte state-omschrijving voor de weergave.
function manualStateLabel(s) {
  if (s.status === "unanswered") return "Unanswered";
  if (s.status === "no") return `Answered "No" on ${s.answer.date} by ${s.answer.excludedBy || s.answer.answeredBy || "?"} — still open`;
  if (s.status === "expired") return `Answered "Yes" on ${s.answer.date} — recheck overdue since ${s.recheckDate}`;
  return `Answered "Yes" on ${s.answer.date} — recheck due ${s.recheckDate}`;
}

// Eén open manual check → synthetische "violation" in het bestaande contract.
function manualCheckViolation(s) {
  const fp = "manual:" + s.def.id;
  return {
    ruleId: s.def.id,
    kind: "manual",
    source: "clevr-manual",
    category: s.def.category,
    severity: s.def.severity,
    documentType: "Manual check",
    documentQualifiedName: s.def.id,
    elementName: "",
    reason: s.def.question,
    suggestion: s.def.context,
    fingerprint: fp,
    manual: s, // state meedragen voor de weergave
  };
}
function openManualCheckViolations() {
  return manualChecksAll().filter((s) => s.open).map(manualCheckViolation);
}

// Exclusions (Fase 6, spec §3): bewust onderdrukte improvements MET reden, opgeslagen door
// C# in $project/.clevr-acr/exclusions.json en hierheen gestuurd. Match op fingerprint
// (sha1(ruleId|documentQualifiedName|elementName), al op elke Violation). Net als het
// System-filter verdwijnen uitgesloten improvements uit de standaard-lijst/-tellingen, maar
// ze blijven zichtbaar in de "Excluded"-sectie + het rapport. Data/UI-scheiding: de Violation
// blijft compleet; dit is puur een weergavefilter bovenop baseViolations().
function excludedFingerprintSet() {
  return new Set(exclusions.map((e) => e.fingerprint));
}

// De standaard zichtbare set: niet-System én niet-uitgesloten.
function activeViolations() {
  const ex = excludedFingerprintSet();
  return baseViolations().filter((v) => !ex.has(v.fingerprint));
}

// Hoeveel huidige (niet-System) improvements delen de fingerprint van v? >1 betekent dat een
// exclusion op v ook die andere punten raakt (gebundelde mxlint-violations delen één
// fingerprint — bevinding 7). We tonen dit vóór bevestiging zodat het geen verrassing is.
function sharedFingerprintCount(v) {
  return baseViolations().filter((x) => x.fingerprint === v.fingerprint).length;
}

// Bouwt het overzicht van uitgesloten improvements, GEGROEPEERD PER REGEL (zoals de hoofdlijst).
// Per exclusion-entry (= één fingerprint): de geraakte huidige violations (matched) of leeg
// (stale → fingerprint matcht niets meer; zichtbaar markeren, spec §3). Een regel-groep bundelt
// alle entries met dezelfde ruleId.
//   groups: [{ ruleId, entries:[{exclusion, violations, isStale}], findingCount, staleEntries }]
//   matchedCount = totaal uitgesloten findings (sum violations); staleCount = totaal stale entries.
function excludedView() {
  const byFp = new Map();
  for (const v of baseViolations()) {
    if (!byFp.has(v.fingerprint)) byFp.set(v.fingerprint, []);
    byFp.get(v.fingerprint).push(v);
  }
  const ruleMap = new Map();
  let matchedCount = 0, staleCount = 0;
  for (const e of exclusions) {
    const vs = byFp.get(e.fingerprint) || [];
    const isStale = vs.length === 0;
    if (isStale) staleCount++; else matchedCount += vs.length;
    if (!ruleMap.has(e.ruleId)) ruleMap.set(e.ruleId, { ruleId: e.ruleId, entries: [], findingCount: 0, staleEntries: 0 });
    const g = ruleMap.get(e.ruleId);
    g.entries.push({ exclusion: e, violations: vs, isStale });
    g.findingCount += vs.length;
    if (isStale) g.staleEntries++;
  }
  const groups = [...ruleMap.values()].sort((a, b) => a.ruleId.localeCompare(b.ruleId));
  return { groups, matchedCount, staleCount };
}

// Korte preview uit de reason van de eerste instantie (~60 tekens), afgekapt.
// Bewust de VOLLEDIGE reason (geen document-strip-heuristiek): voorspelbaarder, en
// minder nodig nu elke regel ook een naam toont. Uit de bestaande Violation-data.
function previewText(text, max = 60) {
  if (!text) return "";
  const t = text.trim();
  return t.length <= max ? t : t.slice(0, max).trimEnd() + "…";
}

// Leesbare naam per regel: ACR-regels gebruiken hun acrCode; generieke mxcli-regels
// de naam uit de mxcli-catalogus (lastRuleNames, uit `mxcli lint --list-rules`).
function ruleName(rule) {
  if (rule.kind === "manual") return "Manual check";
  if (rule.kind === "acr") return rule.acrCode || "";
  return lastRuleNames[rule.ruleId] || "";
}

function el(tag, opts = {}) {
  const node = document.createElement(tag);
  if (opts.className) node.className = opts.className;
  if (opts.text !== undefined) node.textContent = opts.text;
  for (const c of opts.children || []) node.appendChild(c);
  for (const [k, v] of Object.entries(opts.attrs || {})) node.setAttribute(k, v);
  return node;
}

function matches(v, q) {
  if (!q) return true;
  return [v.ruleId, v.acrCode, lastRuleNames[v.ruleId], v.category, displayCategory(v),
          v.severity, originBadge(v), v.documentType, v.documentQualifiedName,
          v.elementName, v.reason, v.suggestion, v.source]
    .filter(Boolean).join(" ").toLowerCase().includes(q);
}

// Groepeer per regel (ruleId): één vermelding met alle instanties eronder.
function groupByRule(items) {
  const map = new Map();
  for (const v of items) {
    if (!map.has(v.ruleId)) map.set(v.ruleId, { rule: v, items: [] });
    map.get(v.ruleId).items.push(v);
  }
  return [...map.values()];
}

// Navigatie (Fase 4): vraag de C#-backend het document in Studio Pro te openen. De
// render-laag stuurt alleen de bestaande Violation-velden mee; C# resolt + navigeert.
function openInStudioPro(v) {
  post("OpenDocument", {
    documentId: v.documentId || "",
    documentQualifiedName: v.documentQualifiedName || "",
    documentType: v.documentType || "",
  });
}

// ---- Ask Maia: prompt-generatie (render-laag; gebruikt alleen bestaande Violation-velden).

// Leesbaar engine-label voor in de prompt (Maia presteert het best met expliciete context).
function sourceEngineLabel(v) {
  if (v.kind === "acr") return "CLEVR ACR";
  if (v.source === "mxlint") return "mxlint.com (Rego best-practice engine)";
  return "mxcli (Mendix lint)";
}

// Grote regels (bv. de mxlint default-value-regel met honderden punten) zouden de prompt
// laten exploderen → cap op de eerste N punten, met een "... and N more"-notitie.
const MAIA_RULE_CAP = 50;

function ruleLabelFor(rule) {
  const name = ruleName(rule);
  return name ? `${rule.ruleId} (${name})` : rule.ruleId;
}

// Eén finding als compacte, voor Maia leesbare blok-regel.
function maiaFindingBlock(n, v) {
  const where = v.elementName
    ? `${v.documentType} ${v.documentQualifiedName} › ${v.elementName}`
    : `${v.documentType} ${v.documentQualifiedName}`;
  let s = `${n}. ${where}\n   Issue: ${v.reason || "(no description)"}`;
  if (v.suggestion) s += `\n   Suggested fix: ${v.suggestion}`;
  return s;
}

// Prompt voor een HELE regel met al z'n punten (gecapt).
function maiaPromptForRule(rule, items) {
  const label = ruleLabelFor(rule);
  const n = items.length;
  const lines = [
    `I have ${n} code-quality finding${n === 1 ? "" : "s"} for rule ${label} in my Mendix app, please help me resolve them.`,
    "",
    `Rule: ${label}`,
    `Category: ${displayCategory(rule)}`,
    `Severity: ${rule.severity || "n/a"}`,
    `Source engine: ${sourceEngineLabel(rule)}`,
  ];
  if (rule.documentationUrl) lines.push(`Documentation: ${rule.documentationUrl}`);
  lines.push("", `Findings (${n}):`);
  items.slice(0, MAIA_RULE_CAP).forEach((v, i) => lines.push(maiaFindingBlock(i + 1, v)));
  if (n > MAIA_RULE_CAP) lines.push(`... and ${n - MAIA_RULE_CAP} more`);
  lines.push("", "Please explain how to fix these and, where possible, give the concrete steps in Mendix Studio Pro.");
  return lines.join("\n");
}

// Prompt voor één individueel punt.
function maiaPromptForFinding(rule, v) {
  const where = v.elementName
    ? `${v.documentType} ${v.documentQualifiedName} › ${v.elementName}`
    : `${v.documentType} ${v.documentQualifiedName}`;
  const lines = [
    "I have a code-quality finding in my Mendix app, please help me resolve it.",
    "",
    `Rule: ${ruleLabelFor(rule)}`,
    `Category: ${displayCategory(v)}`,
    `Severity: ${v.severity || "n/a"}`,
    `Source engine: ${sourceEngineLabel(v)}`,
    `Document: ${where}`,
    `Issue: ${v.reason || "(no description)"}`,
  ];
  if (v.suggestion) lines.push(`Suggested fix: ${v.suggestion}`);
  if (v.documentationUrl) lines.push(`Documentation: ${v.documentationUrl}`);
  lines.push("", "Please explain how to fix this and, where possible, give the concrete steps in Mendix Studio Pro.");
  return lines.join("\n");
}

// Kopieer naar klembord met betrouwbare fallback (WebView2 blokkeert navigator.clipboard
// soms / vereist focus → val terug op een verborgen textarea + execCommand).
async function copyToClipboard(text) {
  try {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch (e) { /* val door naar fallback */ }
  try {
    const ta = document.createElement("textarea");
    ta.value = text;
    ta.setAttribute("readonly", "");
    ta.style.position = "fixed";
    ta.style.top = "-1000px";
    ta.style.opacity = "0";
    document.body.appendChild(ta);
    ta.focus();
    ta.select();
    const ok = document.execCommand("copy");
    document.body.removeChild(ta);
    return ok;
  } catch (e) { return false; }
}

function copyMaiaPrompt(text, anchor) {
  copyToClipboard(text).then((ok) => {
    notify(ok ? "Maia prompt copied — paste it into Maia"
              : "Could not copy the Maia prompt to the clipboard.", !ok, anchor);
  });
}

// "Copy Maia prompt"-knop (alleen in het paneel). promptFn() levert de prompt lui op
// (pas bij klik samengesteld). stopPropagation/preventDefault zodat een knop binnen een
// <summary> de uitklap niet togglet.
function maiaButton(promptFn) {
  const btn = el("button", { className: "acr-maia-btn", text: "Copy Maia prompt", attrs: { type: "button", title: "Generate an English prompt for Maia and copy it to the clipboard" } });
  btn.addEventListener("click", (e) => {
    e.preventDefault();
    e.stopPropagation();
    copyMaiaPrompt(promptFn(), btn);
  });
  return btn;
}

// ---- Exclude: knop + verplichte-reden-dialoog (Fase 6).

function excludeButton(v) {
  const btn = el("button", { className: "acr-exclude-btn", text: "Exclude", attrs: { type: "button", title: "Exclude this improvement (a reason is required)" } });
  btn.addEventListener("click", (e) => { e.preventDefault(); e.stopPropagation(); openExcludeDialog(v); });
  return btn;
}

// Regel-niveau: sluit ALLE punten onder de regel in één keer uit met DEZELFDE reden.
function excludeRuleButton(rule, items) {
  const btn = el("button", { className: "acr-exclude-rule-btn", text: "Exclude rule", attrs: { type: "button", title: "Exclude all findings for this rule with one reason" } });
  btn.addEventListener("click", (e) => { e.preventDefault(); e.stopPropagation(); openExcludeRuleDialog(rule, items); });
  return btn;
}

// Herbruikbare modale dialoog die een reden/toelichting VERPLICHT (confirm-knoppen blijven
// disabled tot er tekst staat; ook een tweede vangnet). Met `confirmButtons` (array van
// {label, className, onConfirm(text, btn)}) kunnen er meerdere uitkomsten zijn (bv. Ja/Nee bij
// een manual check); anders één knop via `confirmLabel`/`onConfirm`. onConfirm draait vóór het
// sluiten, zodat een toast aan de knop verankerd kan worden.
function openReasonDialog({ title, metaText, noteText, fieldLabel, placeholder, confirmLabel, onConfirm, confirmButtons }) {
  const overlay = el("div", { className: "acr-modal-overlay" });
  const box = el("div", { className: "acr-modal" });
  box.addEventListener("click", (e) => e.stopPropagation());

  box.appendChild(el("h3", { text: title }));
  if (metaText) box.appendChild(el("div", { className: "acr-modal-meta", text: metaText }));
  if (noteText) box.appendChild(el("div", { className: "acr-modal-warn", text: noteText }));
  box.appendChild(el("div", { className: "acr-modal-fieldlabel", text: fieldLabel || "Reason (required):" }));
  const ta = el("textarea", { className: "acr-modal-input", attrs: { rows: "3", placeholder: placeholder || "Why is this intentionally not fixed? (shared with the team via version control)" } });
  box.appendChild(ta);

  const close = () => overlay.remove();
  const specs = confirmButtons && confirmButtons.length
    ? confirmButtons
    : [{ label: confirmLabel || "Exclude", className: "acr-modal-confirm", onConfirm }];
  const buttons = [];
  const actions = el("div", { className: "acr-modal-actions" });
  const cancel = el("button", { className: "acr-modal-cancel", text: "Cancel", attrs: { type: "button" } });
  cancel.addEventListener("click", close);
  actions.appendChild(cancel);
  for (const spec of specs) {
    const btn = el("button", { className: spec.className || "acr-modal-confirm", text: spec.label, attrs: { type: "button" } });
    btn.disabled = true;
    btn.addEventListener("click", () => {
      const text = ta.value.trim();
      if (!text) return; // dubbele vangnet
      spec.onConfirm(text, btn);
      close();
    });
    buttons.push(btn);
    actions.appendChild(btn);
  }
  ta.addEventListener("input", () => { const empty = ta.value.trim().length === 0; buttons.forEach((b) => (b.disabled = empty)); });
  overlay.addEventListener("click", close); // klik buiten de box sluit
  box.appendChild(actions);

  overlay.appendChild(box);
  document.body.appendChild(overlay);
  ta.focus();
}

// ---- Manual checks: Answer-knop + Ja/Nee-dialoog (hergebruikt openReasonDialog).
function answerButton(state) {
  const label = state.status === "unanswered" ? "Answer" : "Re-answer";
  const btn = el("button", { className: "acr-answer-btn", text: label, attrs: { type: "button", title: "Answer this manual check (an explanation/reason is required)" } });
  btn.addEventListener("click", (e) => { e.preventDefault(); e.stopPropagation(); openManualCheckDialog(state); });
  return btn;
}

function openManualCheckDialog(state) {
  const def = state.def;
  const prev = state.answer ? ` Previously answered "${state.answer.answer}" on ${state.answer.date}.` : "";
  openReasonDialog({
    title: "Answer manual check",
    metaText: def.question,
    noteText: def.context + prev,
    fieldLabel: "Explanation (Yes) or reason (No) — required:",
    placeholder: "Yes: what did you review/assess?  No: why not yet?",
    confirmButtons: [
      { label: "Answer: Yes", className: "acr-modal-confirm-yes", onConfirm: (note, btn) => {
          post("AnswerManualCheck", { id: def.id, answer: "yes", note });
          notify("Manual check answered — Yes", false, btn);
        } },
      { label: "Answer: No", className: "acr-modal-confirm-no", onConfirm: (note, btn) => {
          post("AnswerManualCheck", { id: def.id, answer: "no", note });
          notify("Manual check answered — No (stays open)", false, btn);
        } },
    ],
  });
}

// Eén punt uitsluiten. Bij een gedeelde fingerprint tonen we dat de uitsluiting N punten raakt.
function openExcludeDialog(v) {
  const shared = sharedFingerprintCount(v);
  const label = ruleName(v) ? `${v.ruleId} (${ruleName(v)})` : v.ruleId;
  const where = v.elementName ? `${v.documentType} ${v.documentQualifiedName} › ${v.elementName}` : `${v.documentType} ${v.documentQualifiedName}`;
  openReasonDialog({
    title: "Exclude improvement",
    metaText: `${label} — ${where}`,
    noteText: shared > 1
      ? `Note: this exclusion shares one fingerprint with ${shared} findings on this document and will hide all ${shared}. (mxlint bundles per-attribute findings under one fingerprint.)`
      : "",
    confirmLabel: "Exclude",
    onConfirm: (reason, confirmBtn) => {
      post("AddExclusion", {
        fingerprint: v.fingerprint, ruleId: v.ruleId,
        documentQualifiedName: v.documentQualifiedName, elementName: v.elementName || "",
        reason,
      });
      notify(shared > 1 ? `Excluded — hides ${shared} findings on this document` : "Improvement excluded", false, confirmBtn);
    },
  });
}

// Hele regel uitsluiten met één reden. We schrijven één exclusion per UNIEKE fingerprint
// (gebundelde mxlint-punten delen er één → niet dubbel). De telling in de dialoog = het aantal
// unieke fingerprints dat wordt weggeschreven; bij bundeling lichten we het verschil toe.
function openExcludeRuleDialog(rule, items) {
  const byFp = new Map();
  for (const v of items) if (!byFp.has(v.fingerprint)) byFp.set(v.fingerprint, v);
  const uniqueCount = byFp.size;
  const label = ruleName(rule) ? `${rule.ruleId} (${ruleName(rule)})` : rule.ruleId;
  const bundleNote = items.length > uniqueCount
    ? ` Some findings share a fingerprint (mxlint bundles per-attribute findings); ${items.length} findings map to ${uniqueCount} exclusion entries.`
    : "";
  openReasonDialog({
    title: "Exclude rule",
    metaText: label,
    noteText: `This will exclude all ${uniqueCount} findings for this rule with the same reason.${bundleNote}`,
    placeholder: "Why is this rule intentionally not addressed? (shared with the team via version control)",
    confirmLabel: "Exclude rule",
    onConfirm: (reason, confirmBtn) => {
      const specs = [...byFp.values()].map((v) => ({
        fingerprint: v.fingerprint, ruleId: v.ruleId,
        documentQualifiedName: v.documentQualifiedName, elementName: v.elementName || "",
      }));
      post("AddExclusions", { reason, items: specs }); // batch: één bestand-write voor de hele regel
      notify(`Excluded rule ${rule.ruleId} — ${uniqueCount} ${uniqueCount === 1 ? "entry" : "entries"}`, false, confirmBtn);
    },
  });
}

function ruleDetails({ rule, items }, interactive = true) {
  const sev = rule.severity || "";
  const isAcr = rule.kind === "acr";
  const det = el("details", { className: `acr-rule sevbar-${sev}` });

  const summary = el("summary");
  summary.appendChild(el("span", { className: `sev sev-${sev}`, text: sev }));
  summary.appendChild(el("span", {
    className: `origin-badge${isAcr ? " origin-acr" : ""}`,
    text: originBadge(rule),
  }));
  summary.appendChild(el("span", { className: "acr-ruleid", text: rule.ruleId }));
  // Leesbare naam naast het id — voor ACR de acrCode, voor generiek de catalogusnaam.
  const name = ruleName(rule);
  if (name) summary.appendChild(el("span", { className: "acr-acrcode", text: name }));
  // Representatieve detailtekst op de dichtgeklapte kop (reason van het eerste geval).
  const preview = previewText(items[0]?.reason);
  if (preview) summary.appendChild(el("span", { className: "acr-rule-preview", text: preview }));
  summary.appendChild(el("span", { className: "acr-rule-count", text: `${items.length} improvements` }));
  // Regel-niveau acties (alleen in het paneel): Ask Maia + de hele regel uitsluiten.
  if (interactive) {
    summary.appendChild(maiaButton(() => maiaPromptForRule(rule, items)));
    summary.appendChild(excludeRuleButton(rule, items));
  }
  det.appendChild(summary);

  const body = el("div", { className: "acr-rule-body" });
  for (const v of items) {
    const inst = el("div", { className: "acr-instance" });
    if (v.kind === "manual") {
      // Manual check: toon de huidige state i.p.v. een documentregel (geen navigatie).
      inst.appendChild(el("div", { className: "acr-manual-state", text: manualStateLabel(v.manual) }));
    } else {
      const doc = el("div", { className: "acr-doc" + (interactive ? " acr-doc-clickable" : "") });
      doc.appendChild(el("span", { className: "doctype", text: `${v.documentType}: ` }));
      doc.appendChild(el("span", { className: "qname", text: v.documentQualifiedName }));
      if (v.elementName) {
        doc.appendChild(el("span", { text: " › " }));
        doc.appendChild(el("span", { className: "elem", text: v.elementName }));
      }
      // Klik op de documentregel → open het document in Studio Pro (alleen in het paneel;
      // het geëxporteerde HTML-rapport heeft geen webview, dus daar geen klik-actie).
      if (interactive) {
        doc.title = "Open this document in Studio Pro";
        doc.appendChild(el("span", { className: "acr-open-hint", text: " ↗ open" }));
        doc.addEventListener("click", () => { lastActionAnchor = doc; openInStudioPro(v); });
      }
      inst.appendChild(doc);
    }
    inst.appendChild(el("div", { className: "acr-reason", text: v.reason }));
    if (v.suggestion) inst.appendChild(el("div", { className: "acr-suggestion", text: v.suggestion }));
    // Acties op puntniveau (alleen in het paneel): Answer (manual check) + Ask Maia + Exclude.
    if (interactive) {
      const actions = el("div", { className: "acr-instance-actions" });
      if (v.kind === "manual") actions.appendChild(answerButton(v.manual));
      actions.appendChild(maiaButton(() => maiaPromptForFinding(rule, v)));
      actions.appendChild(excludeButton(v));
      inst.appendChild(actions);
    }
    if (v.documentationUrl) {
      const wrap = el("div", { className: "acr-docslink" });
      const a = el("a", { text: "Documentation", attrs: { href: v.documentationUrl, target: "_blank", rel: "noreferrer" } });
      // In het paneel openen we de URL betrouwbaar via C# (Process.Start); in het rapport
      // blijft het een gewone werkende link.
      if (interactive) {
        a.addEventListener("click", (e) => { e.preventDefault(); lastActionAnchor = a; post("OpenUrl", { url: v.documentationUrl }); });
      }
      wrap.appendChild(a);
      inst.appendChild(wrap);
    }
    body.appendChild(inst);
  }
  det.appendChild(body);
  return det;
}

function countRow(label, count, labelNode) {
  const row = el("div", { className: "acr-countrow" });
  row.appendChild(labelNode || el("span", { className: "label", text: label }));
  row.appendChild(el("span", { className: "count", text: String(count) }));
  return row;
}

// Gecombineerd filter: een improvement is zichtbaar als hij aan ALLE actieve dimensies
// voldoet (AND): herkomst (checkbox-UI), categorie (klikbare telkaart), severity (idem)
// en vrije tekst. Binnen categorie/severity geldt OR (leeg = geen filter op die dimensie).
function passesFilters(v, q) {
  return originEnabled.has(originOf(v))
    && (categoryEnabled.size === 0 || categoryEnabled.has(displayCategory(v)))
    && (severityEnabled.size === 0 || severityEnabled.has(v.severity))
    && matches(v, q);
}

// De volledige verzameling severities (uit ALLE improvements, niet de gefilterde set),
// in vaste volgorde. Zo blijven alle severity-rijen klikbaar voor multi-select, ook als
// het actieve filter er een paar tot 0 reduceert (de counts bewegen wél mee).
function severityUniverse() {
  const present = new Set();
  for (const v of activeViolations()) present.add(v.severity);
  return [
    ...SEVERITY_ORDER.filter((s) => present.has(s)),
    ...[...present].filter((s) => !SEVERITY_ORDER.includes(s)).sort(),
  ];
}

function toggleSet(set, key) {
  if (set.has(key)) set.delete(key); else set.add(key);
  rerender();
}

// Klikbare telkaart-rij: toggelt een filterwaarde; krijgt 'acr-selected' als hij actief is.
function clickableRow(labelNode, count, selected, onClick) {
  const row = el("div", { className: "acr-countrow acr-clickable" + (selected ? " acr-selected" : "") });
  row.appendChild(labelNode);
  row.appendChild(el("span", { className: "count", text: String(count) }));
  row.addEventListener("click", onClick);
  return row;
}

// Telkaart: per categorie, per severity, en per herkomst — over de GEFILTERDE improvements
// (de counts bewegen mee met alle filters). Met interactive=true zijn de categorie- en
// severity-rijen klikbaar als filter; interactive=false (HTML-rapport) levert statische rijen.
function renderSummary(all, interactive = true) {
  // Per categorie (alle zes, via displayCategory) — klikbaar als filter.
  const catCard = el("div", { className: "acr-card" });
  catCard.appendChild(el("h3", { text: "Improvements per category" }));
  for (const c of ACR_CATEGORIES) {
    const count = all.filter((v) => displayCategory(v) === c).length;
    if (interactive) {
      const label = el("span", { className: "label", text: c });
      catCard.appendChild(clickableRow(label, count, categoryEnabled.has(c), () => toggleSet(categoryEnabled, c)));
    } else {
      catCard.appendChild(countRow(c, count));
    }
  }
  catCard.appendChild(totalRow(all.length));

  // Per severity (gemengde schalen: ACR + mxcli + mxlint, letterlijk) — klikbaar als filter.
  // De rijen komen uit de VOLLEDIGE set (severityUniverse) zodat ze blijven voor multi-select;
  // de counts komen uit de gefilterde set en bewegen dus mee.
  const sevCard = el("div", { className: "acr-card" });
  sevCard.appendChild(el("h3", { text: "Improvements per severity" }));
  for (const s of severityUniverse()) {
    const count = all.filter((v) => v.severity === s).length;
    const label = el("span", { className: "label" });
    label.appendChild(el("span", { className: `sev sev-${s}`, text: s === "" ? "(none)" : s }));
    if (interactive) {
      sevCard.appendChild(clickableRow(label, count, severityEnabled.has(s), () => toggleSet(severityEnabled, s)));
    } else {
      sevCard.appendChild(countRow(null, count, label));
    }
  }
  sevCard.appendChild(totalRow(all.length));

  // Per herkomst (de uitsplitsing): ACR / MxCLI Mxlint / Manual.
  const originCard = el("div", { className: "acr-card" });
  originCard.appendChild(el("h3", { text: "Improvements per source" }));
  const origins = new Map();
  for (const v of all) {
    const o = originLabel(v);
    origins.set(o, (origins.get(o) || 0) + 1);
  }
  for (const o of ["ACR (calibrated)", "MxCLI Mxlint", "Manual checks"]) {
    if (origins.has(o)) originCard.appendChild(countRow(o, origins.get(o)));
  }
  originCard.appendChild(totalRow(all.length));

  const summary = el("div", { className: "acr-summary acr-summary-3", children: [catCard, sevCard, originCard] });
  // Snelle-scan-hint (niet-opdringerig): de describe-route liep niet, dus de diepe microflow-/expressie-
  // analyse ontbreekt. Communiceert het VERSCHIL (niet "kapot"), zodat de gebruiker weet dat 'r meer is.
  if (!lastDeepScan) {
    return el("div", { children: [
      summary,
      el("p", { className: "acr-scan-note",
        text: "Quick scan — the deep microflow & expression analysis (complexity, nested ifs, empty-string checks, default ReadWrite access) is NOT included. Run a Deepscan for the full analysis." }),
    ] });
  }
  return summary;
}

function totalRow(n) {
  const row = el("div", { className: "acr-countrow acr-total" });
  // Tijdens het streamen is het totaal een TUSSENSTAND: label + count expliciet als "partial" markeren,
  // zodat een gebruiker een tussenstand NOOIT voor het eindtotaal aanziet.
  row.appendChild(el("span", { className: "label", text: scanStreaming ? "Total (so far)" : "Total" }));
  const count = el("span", { className: "count", text: scanStreaming ? `${n}…` : String(n) });
  if (scanStreaming) count.title = "Scan in progress — this count is partial and will keep rising.";
  row.appendChild(count);
  return row;
}

function categoryGroup(category, items, interactive = true) {
  const section = el("div", { className: "acr-group" });
  const head = el("div", { className: "acr-group-head" });
  head.appendChild(el("h3", { text: category }));
  head.appendChild(el("span", { className: "acr-group-count", text: `${items.length} improvements` }));
  section.appendChild(head);
  // Sorteer regels: ACR eerst (gekalibreerd), dan generiek; binnen elk op ruleId.
  const rules = groupByRule(items).sort((a, b) => {
    const ak = a.rule.kind === "acr" ? 0 : 1, bk = b.rule.kind === "acr" ? 0 : 1;
    return ak - bk || a.rule.ruleId.localeCompare(b.rule.ruleId);
  });
  for (const r of rules) section.appendChild(ruleDetails(r, interactive));
  return section;
}

function renderReport(root, violations, query) {
  const q = (query || "").trim().toLowerCase();
  // Filter op alle dimensies samen (herkomst + categorie + severity + vrije tekst). De
  // telkaarten hieronder tellen de GEFILTERDE set, zodat de aantallen matchen met wat zichtbaar is.
  const all = violations.filter((v) => passesFilters(v, q));
  // Scrollpositie bewaren over een streaming-herrender (anders springt het paneel elke ~20-30s naar boven).
  const scroller = document.scrollingElement || document.documentElement;
  const keepScroll = scanStreaming ? scroller.scrollTop : null;
  root.innerHTML = "";
  if (scanStreaming) root.appendChild(streamingBanner());
  root.appendChild(renderSummary(all));

  if (all.length === 0) {
    root.appendChild(el("div", { className: "acr-empty", text: "No improvements match the filter." }));
  } else {
    for (const c of ACR_CATEGORIES) {
      const items = all.filter((v) => displayCategory(v) === c);
      if (items.length) root.appendChild(categoryGroup(c, items));
    }
  }

  // Excluded: toggle + sectie (altijd, ook als er geen actieve improvements zijn).
  const ev = excludedView();
  if (ev.groups.length) {
    const bar = el("div", { className: "acr-excluded-toggle" });
    const staleNote = ev.staleCount ? ` + ${ev.staleCount} stale` : "";
    const btn = el("button", {
      attrs: { type: "button" },
      text: `${showExcluded ? "Hide" : "Show"} excluded (${ev.matchedCount}${staleNote})`,
    });
    btn.addEventListener("click", () => { showExcluded = !showExcluded; rerender(); });
    bar.appendChild(btn);
    root.appendChild(bar);
    if (showExcluded) {
      const sec = renderExcludedSection(true);
      if (sec) root.appendChild(sec);
    }
  }

  // Answered manual checks: toggle + sectie (de geldig-beantwoorde checks die verdwenen zijn).
  const answered = answeredManualChecks();
  if (answered.length) {
    const bar = el("div", { className: "acr-excluded-toggle" });
    const btn = el("button", {
      attrs: { type: "button" },
      text: `${showAnswered ? "Hide" : "Show"} answered checks (${answered.length})`,
    });
    btn.addEventListener("click", () => { showAnswered = !showAnswered; rerender(); });
    bar.appendChild(btn);
    root.appendChild(bar);
    if (showAnswered) {
      const sec = renderAnsweredChecksSection(true);
      if (sec) root.appendChild(sec);
    }
  }

  if (keepScroll != null) scroller.scrollTop = keepScroll; // streaming: scrollpositie herstellen
}

// Onmiskenbare voortgangsbanner zolang de scan loopt. Inline-styled (niet afhankelijk van externe CSS-
// timing, net als de toast) zodat de "tussenstand is partial"-boodschap ALTIJD zichtbaar is. Verdwijnt
// zodra de laatste batch binnen is (scanStreaming=false → geen banner meer).
function streamingBanner() {
  const pct = scanProgress && scanProgress.total
    ? Math.min(100, Math.round((scanProgress.processed / scanProgress.total) * 100)) : null;
  const detail = scanProgress
    ? `${scanProgress.label}${pct != null ? ` (${pct}%)` : ""}`
    : "running the deep microflow & expression analysis…";
  const banner = el("div", { className: "acr-streaming-banner" });
  banner.style.cssText =
    "display:flex;align-items:center;gap:.6em;margin:0 0 12px;padding:10px 14px;border-radius:8px;" +
    "background:#fff7e6;border:1px solid #f0c36d;color:#7a5b00;font-weight:600;";
  const spin = el("span", { text: "⏳" });
  spin.style.cssText = "font-size:1.1em;";
  const txt = el("span", { text: `Scanning… ${detail} — counts below are PARTIAL until the scan finishes.` });
  banner.appendChild(spin);
  banner.appendChild(txt);
  if (scanIncomplete) {
    const w = el("div", { text: "⚠ Some elements could not be described — final results may be incomplete (see the .clevr-acr log)." });
    w.style.cssText = "flex-basis:100%;color:#a00;font-weight:600;margin-top:4px;";
    banner.appendChild(w);
  }
  return banner;
}

// "Answered manual checks"-sectie (gedeeld door paneel en rapport): de geldig (binnen 30 dagen)
// met "ja" beantwoorde checks die uit de open-lijst verdwenen zijn — met antwoord, datum, wie,
// toelichting en de recheck-datum. interactive=true voegt Re-answer/Clear toe (paneel).
function renderAnsweredChecksSection(interactive) {
  const answered = answeredManualChecks();
  if (!answered.length) return null;

  const wrap = el("div", { className: "acr-excluded" });
  const head = el("div", { className: "acr-section-head" });
  head.appendChild(el("h2", { text: "Answered manual checks" }));
  head.appendChild(el("span", { className: "acr-section-count", text: `${answered.length} answered (valid)` }));
  wrap.appendChild(head);
  wrap.appendChild(el("div", { className: "acr-section-note", text: "Consciously assessed — re-checked every 30 days; shared with the team via version control." }));

  for (const s of answered) {
    const card = el("div", { className: "acr-excluded-card" });
    const top = el("div", { className: "acr-excluded-top" });
    top.appendChild(el("span", { className: "acr-ruleid", text: s.def.id }));
    top.appendChild(el("span", { className: "acr-acrcode", text: s.def.category }));
    top.appendChild(el("span", { className: "acr-excluded-applies", text: `answered "Yes" on ${s.answer.date} — recheck due ${s.recheckDate}` }));
    if (interactive) {
      const re = el("button", { className: "acr-unexclude-btn", text: "Re-answer", attrs: { type: "button", title: "Answer this manual check again" } });
      re.addEventListener("click", () => openManualCheckDialog(s));
      const clr = el("button", { className: "acr-unexclude-btn", text: "Clear answer", attrs: { type: "button", title: "Clear the answer — the check becomes open again" } });
      clr.addEventListener("click", () => { notify("Manual check answer cleared", false, clr); post("ClearManualCheck", { id: s.def.id }); });
      top.appendChild(re);
      top.appendChild(clr);
    }
    card.appendChild(top);
    card.appendChild(el("div", { className: "acr-excluded-reason", text: s.def.question }));
    card.appendChild(el("div", { className: "acr-excluded-meta", text: `Answer: ${s.answer.note} — by ${s.answer.answeredBy || "?"}` }));
    wrap.appendChild(card);
  }
  return wrap;
}

// Rendert de "Excluded improvements"-sectie (gedeeld door paneel en rapport), GEGROEPEERD
// PER REGEL (zoals de hoofdlijst): een regel-kop (ruleId + naam + aantal + "Remove rule
// exclusion") met de uitgesloten punten eronder. Stale entries staan in hun eigen regel-groep,
// met "stale"-markering. interactive=true voegt de remove-knoppen toe (paneel); false = rapport.
function renderExcludedSection(interactive) {
  const { groups, matchedCount, staleCount } = excludedView();
  if (!groups.length) return null;

  const wrap = el("div", { className: "acr-excluded" });
  const head = el("div", { className: "acr-section-head" });
  head.appendChild(el("h2", { text: "Excluded improvements" }));
  head.appendChild(el("span", {
    className: "acr-section-count",
    text: `${matchedCount} excluded${staleCount ? ` · ${staleCount} stale` : ""}`,
  }));
  wrap.appendChild(head);
  wrap.appendChild(el("div", {
    className: "acr-section-note",
    text: "Intentionally not fixed — each with a reason, shared with the team via version control.",
  }));

  for (const g of groups) wrap.appendChild(excludedRuleGroup(g, interactive));
  return wrap;
}

// Eén regel-groep in de excluded-sectie: kop (ruleId + naam + aantal + Remove rule exclusion)
// met de exclusion-entries van die regel eronder (matched + stale).
function excludedRuleGroup(g, interactive) {
  const sec = el("div", { className: "acr-excluded-rule" });
  const head = el("div", { className: "acr-excluded-rule-head" });
  head.appendChild(el("span", { className: "acr-ruleid", text: g.ruleId }));
  const name = lastRuleNames[g.ruleId];
  if (name) head.appendChild(el("span", { className: "acr-acrcode", text: name }));
  const staleNote = g.staleEntries ? ` · ${g.staleEntries} stale` : "";
  head.appendChild(el("span", { className: "acr-excluded-rule-count", text: `${g.findingCount} excluded${staleNote}` }));
  if (interactive) head.appendChild(removeRuleExclusionButton(g));
  sec.appendChild(head);

  for (const e of g.entries) sec.appendChild(excludedCard(e.exclusion, e.violations, e.isStale, interactive));
  return sec;
}

// "Remove rule exclusion": zet ALLE uitgesloten punten van de regel in één keer terug
// (incl. stale entries). Bevestiging vooraf, dan een batch-RemoveExclusions.
function removeRuleExclusionButton(g) {
  const btn = el("button", { className: "acr-unexclude-rule-btn", text: "Remove rule exclusion", attrs: { type: "button", title: "Restore all excluded findings for this rule" } });
  btn.addEventListener("click", () => {
    const fingerprints = g.entries.map((e) => e.exclusion.fingerprint);
    const restoreMsg = g.findingCount > 0
      ? `This will restore all ${g.findingCount} excluded findings for this rule.`
      : `This will clear all ${g.staleEntries} stale exclusion ${g.staleEntries === 1 ? "entry" : "entries"} for this rule.`;
    const staleClause = (g.findingCount > 0 && g.staleEntries)
      ? ` It also clears ${g.staleEntries} stale ${g.staleEntries === 1 ? "entry" : "entries"}.`
      : "";
    openConfirmDialog({
      title: "Remove rule exclusion",
      message: restoreMsg + staleClause,
      confirmLabel: "Remove rule exclusion",
      onConfirm: (confirmBtn) => {
        post("RemoveExclusions", { fingerprints });
        notify(`Restored rule ${g.ruleId} — ${g.findingCount} ${g.findingCount === 1 ? "finding" : "findings"}`, false, confirmBtn);
      },
    });
  });
  return btn;
}

// Lichte bevestigingsdialoog (geen invoer) — voor acties die geen reden vragen maar wél
// bevestiging (bv. een hele regel terugzetten). onConfirm(confirmBtn) draait vóór het sluiten.
function openConfirmDialog({ title, message, confirmLabel, onConfirm }) {
  const overlay = el("div", { className: "acr-modal-overlay" });
  const box = el("div", { className: "acr-modal" });
  box.addEventListener("click", (e) => e.stopPropagation());
  box.appendChild(el("h3", { text: title }));
  box.appendChild(el("div", { className: "acr-modal-message", text: message }));
  const actions = el("div", { className: "acr-modal-actions" });
  const cancel = el("button", { className: "acr-modal-cancel", text: "Cancel", attrs: { type: "button" } });
  const confirm = el("button", { className: "acr-modal-confirm", text: confirmLabel || "Confirm", attrs: { type: "button" } });
  const close = () => overlay.remove();
  cancel.addEventListener("click", close);
  overlay.addEventListener("click", close);
  confirm.addEventListener("click", () => { onConfirm(confirm); close(); });
  actions.appendChild(cancel);
  actions.appendChild(confirm);
  box.appendChild(actions);
  overlay.appendChild(box);
  document.body.appendChild(overlay);
  confirm.focus();
}

function excludedCard(e, violations, isStale, interactive) {
  const card = el("div", { className: "acr-excluded-card" + (isStale ? " stale" : "") });
  const top = el("div", { className: "acr-excluded-top" });
  top.appendChild(el("span", { className: "acr-ruleid", text: e.ruleId }));
  const name = lastRuleNames[e.ruleId];
  if (name) top.appendChild(el("span", { className: "acr-acrcode", text: name }));
  const where = e.elementName ? `${e.documentQualifiedName} › ${e.elementName}` : e.documentQualifiedName;
  if (where) top.appendChild(el("span", { className: "acr-excluded-where", text: where }));
  if (isStale) {
    top.appendChild(el("span", { className: "acr-stale-badge", text: "stale — no longer matches" }));
  } else if (violations.length > 1) {
    top.appendChild(el("span", { className: "acr-excluded-applies", text: `applies to ${violations.length} findings` }));
  }
  if (interactive) {
    const rm = el("button", { className: "acr-unexclude-btn", text: "Remove exclusion", attrs: { type: "button", title: "Remove this exclusion — the improvement reappears" } });
    rm.addEventListener("click", () => { notify("Exclusion removed", false, rm); post("RemoveExclusion", { fingerprint: e.fingerprint }); });
    top.appendChild(rm);
  }
  card.appendChild(top);

  card.appendChild(el("div", { className: "acr-excluded-reason", text: `Reason: ${e.reason}` }));
  const meta = [e.excludedBy ? `by ${e.excludedBy}` : "", e.date].filter(Boolean).join(" · ");
  if (meta) card.appendChild(el("div", { className: "acr-excluded-meta", text: meta }));

  if (violations.length) {
    const items = el("div", { className: "acr-excluded-items" });
    for (const v of violations) items.appendChild(el("div", { className: "acr-excluded-item", text: previewText(v.reason, 120) }));
    card.appendChild(items);
  }
  return card;
}

// Bouwt een standalone HTML-rapport (embedded CSS, CLEVR-look) uit de VOLLEDIGE laatste
// scan — dezelfde render-functies als het paneel, dus consistent met wat de developer
// ziet. Exporteert alle improvements (niet de live-filter): een rapport is compleet.
function projectName() {
  const wd = (lastMeta.workingDirectory || "").replace(/[\\/]+$/, "");
  const seg = wd.split(/[\\/]/).filter(Boolean);
  return seg.length ? seg[seg.length - 1] : "Mendix project";
}

function buildReportHtml() {
  const root = el("div", { className: "acr-root" });

  // Rapport-kop: titel + project + datum + herkomst-totalen. System-module én uitgesloten
  // improvements weggefilterd (zelfde regel als de UI) zodat het rapport het zichtbare
  // overzicht weerspiegelt; de uitgesloten staan apart onderaan.
  const reportViolations = activeViolations();
  const oc = { acr: 0, mxcli: 0, mxlint: 0, manual: 0 };
  for (const v of reportViolations) oc[originOf(v)]++;
  const ev = excludedView();
  const exNote = ev.matchedCount ? ` · ${ev.matchedCount} excluded` : "";
  const header = el("div", { className: "acr-header" });
  const brand = el("div", { className: "acr-brand" });
  if (clevrLogoDataUri) brand.appendChild(el("img", { className: "acr-logo", attrs: { src: clevrLogoDataUri, alt: "CLEVR" } }));
  const wrap = el("div");
  wrap.appendChild(el("h1", { className: "acr-title", text: "CLEVR ACR Review" }));
  wrap.appendChild(el("div", {
    className: "acr-subtitle",
    text: `${projectName()} · ${reportViolations.length} improvements ` +
          `(${oc.acr} ACR / ${oc.mxcli} MxCLI Mxlint / ${oc.manual} Manual)${exNote} · ` +
          `generated ${new Date().toLocaleString("en-GB")}`,
  }));
  brand.appendChild(wrap);
  header.appendChild(brand);
  root.appendChild(header);

  root.appendChild(renderSummary(reportViolations, false));
  for (const c of ACR_CATEGORIES) {
    const items = reportViolations.filter((v) => displayCategory(v) === c);
    if (items.length) root.appendChild(categoryGroup(c, items, false));
  }
  // Uitgesloten improvements + reden: aparte sectie (transparant naar PO/volgende developer).
  const excludedSection = renderExcludedSection(false);
  if (excludedSection) root.appendChild(excludedSection);
  // Beantwoorde manual checks (geldig "ja") + antwoord/datum/recheck: aparte sectie.
  const answeredSection = renderAnsweredChecksSection(false);
  if (answeredSection) root.appendChild(answeredSection);
  // Vereiste-melding ook in het rapport (zelfde regel als de paneel-footer).
  root.appendChild(el("div", { className: "acr-footer", text: `Generated by CLEVR ACR · requires Mendix Studio Pro 11 or higher · ${new Date().toLocaleString("en-GB")}` }));
  // In het rapport alle regels uitgeklapt tonen (geen interactie nodig op papier/pdf).
  root.querySelectorAll("details").forEach((d) => d.setAttribute("open", ""));

  const css = document.querySelector("style")?.textContent || "";
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<title>CLEVR ACR Review — ${projectName()}</title>
<style>
${css}
body { margin: 16px; max-width: 1200px; }
</style>
</head>
<body>
${root.outerHTML}
</body>
</html>`;
}

// ---------------------------------------------------------------- message-bus glue

let lastViolations = [];
let lastRuleNames = {}; // ruleId → naam (uit mxcli --list-rules), voor generieke regels
let lastRuleCategories = {}; // ruleId → mxcli-categorie (uit --list-rules), basis voor displayCategory
let lastMeta = {}; // scan-metadata (workingDirectory, rawCount, exitCode) voor de rapport-kop
let exclusions = []; // [{fingerprint, ruleId, documentQualifiedName, elementName, reason, excludedBy, date}] — van C#
let showExcluded = false; // toggle voor de "Excluded"-sectie in het paneel
let manualAnswers = []; // [{id, answer, note, answeredBy, date}] — van C# (manual-checks.json)
let showAnswered = false; // toggle voor de "Answered manual checks"-sectie
// CLEVR-logo als data-URI: het paneel toont het PNG via een gewone <img src> (web-server),
// maar het GEËXPORTEERDE rapport is een los bestand → daar embedden we het logo als data-URI
// zodat het rapport standalone blijft. Eénmalig opgehaald bij het openen van de pane.
let clevrLogoDataUri = "";

async function loadLogo() {
  try {
    const res = await fetch("./clevr-logo.png");
    if (!res.ok) return;
    const blob = await res.blob();
    clevrLogoDataUri = await new Promise((resolve, reject) => {
      const r = new FileReader();
      r.onloadend = () => resolve(r.result);
      r.onerror = reject;
      r.readAsDataURL(blob);
    });
  } catch (e) { /* het logo is cosmetisch — zonder is het rapport nog volledig */ }
}

// Engine-filter (herkomst). Manual staat erbij ook al komt er nog geen data binnen.
const ORIGINS = [
  { key: "acr", label: "ACR" },
  { key: "mxcli", label: "MxCLI Mxlint" },
  { key: "manual", label: "Manual checks" },
];
const originEnabled = new Set(ORIGINS.map((o) => o.key));

// Categorie- en severity-filter (klikbare telkaarten). Leeg = geen filter op die
// dimensie (alles tonen). Niet-leeg = OR binnen de dimensie; AND tussen dimensies
// (samen met het herkomst- en tekstfilter).
const categoryEnabled = new Set();
const severityEnabled = new Set();

function post(message, data) {
  window.chrome.webview.postMessage({ message, data });
}

// Bouwt de herkomst-filter (één keer per scan), met totaal-count per herkomst uit de
// VOLLEDIGE scan. Een herkomst zonder data (bv. Manual checks) wordt grijs/uitgeschakeld.
function buildEngineFilter() {
  const container = document.getElementById("engineFilter");
  container.innerHTML = "";
  container.appendChild(el("span", { className: "acr-filter-label", text: "Source:" }));
  const base = activeViolations();
  for (const o of ORIGINS) {
    const count = base.filter((v) => originOf(v) === o.key).length;
    const wrap = el("label", { className: "acr-origin-toggle" + (count === 0 ? " disabled" : "") });
    const cb = el("input", { attrs: { type: "checkbox" } });
    cb.checked = originEnabled.has(o.key);
    if (count === 0) cb.disabled = true;
    cb.addEventListener("change", () => {
      if (cb.checked) originEnabled.add(o.key); else originEnabled.delete(o.key);
      rerender();
    });
    wrap.appendChild(cb);
    wrap.appendChild(el("span", { text: ` ${o.label} (${count})` }));
    container.appendChild(wrap);
  }

  // Marktplaats-toggle (DEEL 3): toon/verberg findings uit app-store-modules. Alleen aanbieden als er
  // app-store-findings zíjn. Telt toggle-ONAFHANKELIJK (System-gefilterd, maar zonder de app-store-
  // filter) — anders zou 'verbergen' de teller op 0 zetten. Default: tonen.
  const asCount = allDisplayViolations().filter((v) => !isSystemModule(v) && isAppStoreModule(v)).length;
  if (asCount > 0) {
    const wrap = el("label", { className: "acr-origin-toggle", attrs: { title: "Marketplace/app-store modules are scanned too; toggle to show or hide them." } });
    const cb = el("input", { attrs: { type: "checkbox" } });
    cb.checked = appStoreVisible;
    cb.addEventListener("change", () => { appStoreVisible = cb.checked; rerender(); });
    wrap.appendChild(cb);
    wrap.appendChild(el("span", { text: ` Marketplace modules (${asCount})` }));
    container.appendChild(wrap);
  }

  container.hidden = false;
}

function setStatus(text, isError) {
  const s = document.getElementById("status");
  s.className = isError ? "acr-status err" : "acr-status";
  s.textContent = text;
}

// Anker voor de volgende toast die uit een C#-antwoord komt (OpenDocument/OpenUrl/Export):
// de klik gebeurde eerder, dus we onthouden het geklikte element tot het antwoord arriveert.
let lastActionAnchor = null;
let toastTimer = null;

// Plaats de toast vlak boven het anker (of eronder als er bovenaan geen ruimte is),
// geklemd binnen het zichtbare gebied. Fixed → viewport-coördinaten, dus scroll-onafhankelijk.
function positionToast(toast, anchor) {
  const m = 8;
  const tw = toast.offsetWidth, th = toast.offsetHeight;
  let left, top;
  if (anchor && anchor.isConnected && anchor.getBoundingClientRect) {
    const r = anchor.getBoundingClientRect();
    left = r.left;
    top = r.top - th - m;
    if (top < m) top = r.bottom + m; // geen ruimte boven → eronder
  } else {
    left = (window.innerWidth - tw) / 2;
    top = 16;
  }
  left = Math.max(m, Math.min(left, window.innerWidth - tw - m));
  top = Math.max(m, Math.min(top, window.innerHeight - th - m));
  toast.style.left = `${left}px`;
  toast.style.top = `${top}px`;
}

// Toon een korte toast nabij het anker; faadt vanzelf weg na ~2.8s. Eén toast tegelijk.
function showToast(text, isError, anchor) {
  const old = document.getElementById("acr-toast");
  if (old) old.remove();
  if (toastTimer) { clearTimeout(toastTimer); toastTimer = null; }

  const toast = el("div", { className: "acr-toast" + (isError ? " err" : ""), text });
  toast.id = "acr-toast";
  // Zet de layout-bepalende stijlen inline vóór het meten, zodat de afmetingen (en dus de
  // positionering/clamp) NIET afhangen van de timing/aanwezigheid van de externe CSS.
  toast.style.position = "fixed";
  toast.style.maxWidth = "min(420px, 90vw)";
  toast.style.left = "0px";
  toast.style.top = "0px";
  document.body.appendChild(toast);
  positionToast(toast, anchor);
  requestAnimationFrame(() => toast.classList.add("show"));

  toastTimer = setTimeout(() => {
    toast.classList.remove("show");
    setTimeout(() => toast.remove(), 400); // na de fade-out opruimen
  }, 2800);
}

// Actie-feedback: directe toast nabij de actie + de statusregel als achtervang/laatste-status.
function notify(text, isError, anchor) {
  setStatus(text, isError);
  showToast(text, isError, anchor);
}

function rerender() {
  // System-module én uitgesloten improvements weggefilterd vóór alle weergave
  // (lijst + telkaarten + totaal). De uitgesloten staan in hun eigen sectie.
  renderReport(document.getElementById("report"), activeViolations(), document.getElementById("filter").value);
}

// Eén SAMENGEVOEGDE set (lastViolations) over beide engines. Een scan vervangt alleen
// de violations van z'n eigen herkomst; de andere engine blijft staan, zodat je beide
// kunt draaien en samen ziet (Fase 3B).
function replaceOrigin(newViolations, originsToReplace) {
  lastViolations = lastViolations
    .filter((v) => !originsToReplace.includes(originOf(v)))
    .concat(newViolations || []);
}

function setMergedStatus(extra) {
  const base = activeViolations(); // System én uitgesloten tellen niet mee in het zichtbare totaal
  const oc = { acr: 0, mxcli: 0, mxlint: 0, manual: 0 };
  for (const v of base) oc[originOf(v)]++;
  const ev = excludedView();
  const exNote = ev.matchedCount ? ` · ${ev.matchedCount} excluded` : "";
  setStatus(
    `${base.length} improvements ` +
    `(${oc.acr} ACR / ${oc.mxcli} MxCLI Mxlint / ${oc.manual} Manual)${exNote}` +
    (extra ? ` — ${extra}` : ""),
    false,
  );
}

// Is er iets te tonen/rapporteren? Open improvements (incl. open manual checks), of een
// answered-/excluded-sectie. Bepaalt de zichtbaarheid van filter/export/reset.
function hasAnythingToShow() {
  return baseViolations().length > 0 || answeredManualChecks().length > 0 || excludedView().groups.length > 0;
}

function refreshAfterScan() {
  const has = hasAnythingToShow();
  document.getElementById("filter").hidden = !has;
  document.getElementById("exportBtn").hidden = !has;
  document.getElementById("resetBtn").hidden = !has;
  buildEngineFilter();
  rerender();
}

// Reset ALLE filters in één keer (categorie, severity, herkomst, tekst) → volledig overzicht.
function resetFilters() {
  categoryEnabled.clear();
  severityEnabled.clear();
  for (const o of ORIGINS) originEnabled.add(o.key);
  document.getElementById("filter").value = "";
  buildEngineFilter(); // herstelt de checkbox-staat (alles aangevinkt)
  rerender();
}

// mxcli (gestreamd): elke batch komt als "AcrViolations" binnen. De FAST-batch (phase="fast") vervangt
// acr+mxcli (clean slate, zoals voorheen) en draagt de metadata; describe-batches (phase="describe")
// APPENDEN hun chunk-findings aan de groeiende set. final=true op de laatste batch → tellingen definitief.
// Backward-compat: een payload zónder 'phase'/'streaming' (de oude niet-gestreamde vorm) telt als fast+final.
function handleMxcliResult(data) {
  let payload;
  try { payload = typeof data === "string" ? JSON.parse(data) : data; }
  catch (e) { setStatus("Could not parse mxcli payload: " + e.message, true); return; }

  if (!payload || payload.ok === false) {
    setStatus(payload && payload.error ? payload.error : "mxcli scan failed (unknown error).", true);
    scanStreaming = false; scanProgress = null; // staat opschonen; bestaande resultaten blijven staan
    return;
  }

  const phase = payload.phase || "fast";           // geen phase = oude niet-gestreamde vorm → fast
  const isFinal = payload.final !== false;          // geen final = niet-gestreamd → meteen definitief

  if (phase === "describe") {
    // APPEND: voeg de chunk-findings toe aan de groeiende set (NIET vervangen — anders weg fast-findings).
    lastViolations = lastViolations.concat(payload.violations || []);
  } else {
    // FAST-batch (of niet-gestreamd): clean-slate vervangen + metadata zetten.
    replaceOrigin(payload.violations, ["acr", "mxcli"]);
    lastRuleNames = { ...lastRuleNames, ...(payload.ruleNames || {}) };
    lastRuleCategories = { ...lastRuleCategories, ...(payload.ruleCategories || {}) };
    lastMeta = { workingDirectory: payload.workingDirectory, rawCount: payload.rawCount, exitCode: payload.exitCode };
    lastAppStoreModules = new Set(payload.appStoreModules || []);
    lastDeepScan = !!payload.deepScan;
  }

  // Voortgang + onvolledigheid (LUID): een chunk die minder teruggaf dan gevraagd = niet stil minder findings.
  const p = payload.progress;
  if (p) {
    scanProgress = { processed: p.processed, total: p.total, label: p.label };
    if (p.requested && p.returned < p.requested) scanIncomplete = true;
  }
  scanStreaming = !isFinal;

  refreshAfterScan();

  if (scanStreaming) {
    // Tussentijds: toon voortgang, NOOIT als eindstand presenteren.
    const running = activeViolations().length;
    const lbl = scanProgress ? ` — ${scanProgress.label}` : "";
    setStatus(`Scanning…${lbl} · ${running} improvements so far (partial)`, false);
  } else {
    // Laatste batch: definitief + duur.
    const secs = scanStartMs ? ` — done in ${((Date.now() - scanStartMs) / 1000).toFixed(0)}s` : "";
    const warn = scanIncomplete ? " ⚠ some elements could not be described (see .clevr-acr log) — results may be incomplete" : "";
    setMergedStatus(`scan complete${secs}${warn}`);
  }
}

// mxlint (async): vervangt alleen mxlint; ACR + mxcli blijven staan.
function handleMxlintResult(data) {
  let payload;
  try { payload = typeof data === "string" ? JSON.parse(data) : data; }
  catch (e) { setStatus("Could not parse mxlint payload: " + e.message, true); return; }

  if (!payload || payload.ok === false) {
    setStatus(payload && payload.error ? payload.error : "mxlint scan failed (unknown error).", true);
    return; // bestaande (mxcli-)resultaten blijven staan
  }
  replaceOrigin(payload.violations, ["mxlint"]);
  // mxlint-regelnamen (rulenumber → rulename) mergen, zodat de render-laag ze net als bij
  // mxcli naast het nummer toont (via ruleName()/lastRuleNames). Geen render-wijziging nodig.
  lastRuleNames = { ...lastRuleNames, ...(payload.ruleNames || {}) };
  // De Rego-engine is uitgeschakeld als findings-bron (alle regels geïnternaliseerd); deze stap
  // ververst alleen nog modelsource via de export. Toon dat i.p.v. een lint-telling.
  if (payload.regoEngineDisabled)
    setMergedStatus(`model export refreshed (Rego engine disabled — rules internalised)`);
  else
    setMergedStatus(`mxlint: ${(payload.violations || []).length} (lint exit ${payload.lintExit})`);
  refreshAfterScan();
}

// Vangnet: rond de streaming-staat ALTIJD af bij ScanFinished, ook als de laatste batch (final=true)
// niet binnenkwam — bv. een deepscan zonder user-microflows/-entiteiten (geen describe-batch), of een
// afgebroken stream. Voorkomt dat de banner/"partial"-markering blijft hangen. Idempotent.
function finalizeStreaming() {
  if (!scanStreaming) return;
  scanStreaming = false;
  scanProgress = null;
  refreshAfterScan();
  const secs = scanStartMs ? ` — done in ${((Date.now() - scanStartMs) / 1000).toFixed(0)}s` : "";
  const warn = scanIncomplete ? " ⚠ some elements could not be described (see .clevr-acr log) — results may be incomplete" : "";
  setMergedStatus(`scan complete${secs}${warn}`);
}

function handleMessage(event) {
  const { message, data } = event.data;
  if (message === "AcrViolations") handleMxcliResult(data);
  else if (message === "MxlintViolations") handleMxlintResult(data);
  else if (message === "ScanProgress") setStatus(data, false);   // voortgangstekst tijdens de scan
  else if (message === "ScanError") { notify("Scan failed: " + data, true, scanAnchor()); }
  else if (message === "ScanFinished") { finalizeStreaming(); setScanning(false); } // her-enable knop + verberg spinner + streaming afronden
  else if (message === "ReportSaved") notify("Report saved (and opened): " + data, false, lastActionAnchor);
  else if (message === "ReportError") notify("Report export failed: " + data, true, lastActionAnchor);
  else if (message === "DocumentOpened") notify("Opened in Studio Pro: " + data, false, lastActionAnchor);
  else if (message === "DocumentOpenError") notify("Could not open document: " + data, true, lastActionAnchor);
  else if (message === "UrlOpened") notify("Documentation opened: " + data, false, lastActionAnchor);
  else if (message === "UrlError") notify("Could not open documentation link: " + data, true, lastActionAnchor);
  else if (message === "Exclusions") handleExclusions(data);
  else if (message === "ExclusionError") notify("Exclusion failed: " + data, true, lastActionAnchor);
  else if (message === "ManualCheckAnswers") handleManualChecks(data);
  else if (message === "ManualCheckError") notify("Manual check failed: " + data, true, lastActionAnchor);
}

// Herrender zodra er iets te tonen is: open improvements (incl. open manual checks),
// beantwoorde checks (voor de toggle) of exclusions. Voorkomt lege telkaarten vóór de eerste
// scan, maar laat open manual checks wél meteen verschijnen (die zijn er ook zonder scan).
function renderIfAnything() {
  if (baseViolations().length || answeredManualChecks().length || excludedView().groups.length) {
    refreshAfterScan();
    setMergedStatus();
  }
}

// Exclusions-lijst van C# ontvangen → opslaan + herrenderen. Tolerant voor object of kale array.
function handleExclusions(data) {
  let payload;
  try { payload = typeof data === "string" ? JSON.parse(data) : data; }
  catch (e) { notify("Could not parse exclusions: " + e.message, true, lastActionAnchor); return; }
  exclusions = Array.isArray(payload) ? payload : (payload && payload.exclusions) || [];
  renderIfAnything();
}

// Manual-check-antwoorden van C# ontvangen → opslaan + herrenderen (open checks verschijnen
// als improvements; geldig "ja" verdwijnt naar de answered-sectie).
function handleManualChecks(data) {
  let payload;
  try { payload = typeof data === "string" ? JSON.parse(data) : data; }
  catch (e) { notify("Could not parse manual checks: " + e.message, true, lastActionAnchor); return; }
  manualAnswers = Array.isArray(payload) ? payload : (payload && payload.answers) || [];
  renderIfAnything();
}

window.chrome.webview.addEventListener("message", handleMessage);
post("MessageListenerRegistered");
post("RequestExclusions");    // laad de exclusions zodra de pane opent
post("RequestManualChecks");  // én de manual-check-antwoorden (open checks verschijnen meteen)
loadLogo();                   // CLEVR-logo als data-URI voor het geëxporteerde rapport

// Eén "Scan"-knop (samengevoegd uit "Scan for improvements" + "Run mxlint scan"). C# dwingt de
// juiste volgorde af: EERST mxlint export+lint (ververst modelsource/), DÁN mxcli + de CLEVR-eigen
// regels (security-export + expressie-pass) die op die verse modelsource leunen. Dit lost meteen de
// stale-modelsource-valkuil op. De losse routes (RunAcrScan/RunMxlintScan) blijven intern bestaan.
const scanBtn = document.getElementById("scanBtn");
const deepScanBtn = document.getElementById("deepScanBtn");
const scanSpinner = document.getElementById("scanSpinner");

// Anker voor toasts die uit de scan-flow komen: de actieve scan-knop.
function scanAnchor() { return scanBtn; }

// Schakelt de "bezig"-staat: BEIDE scan-knoppen disabled + spinner zichtbaar zolang de scan loopt.
function setScanning(on, text) {
  scanBtn.disabled = on;
  deepScanBtn.disabled = on;
  scanSpinner.hidden = !on;
  if (text) setStatus(text, false);
}

// Gedeelde scan-start. deep=false → snelle scan (RunFullScan); deep=true → Deepscan (RunDeepScan,
// met zichtbare duur-waarschuwing zodat de gebruiker niet verrast wordt).
function startScan(deep) {
  // Streaming-staat resetten vóór een nieuwe scan; de eerste binnenkomende batch zet 'm weer.
  scanStreaming = false; scanProgress = null; scanIncomplete = false;
  scanStartMs = Date.now();
  setScanning(true, deep
    ? "Deep analysis — scanning all microflows & entities. This can take ~3 minutes…"
    : "Starting scan… (this can take up to a minute)");
  // Exclusions + manual checks opnieuw ophalen (dekt: project pas ná het openen gekozen).
  post("RequestExclusions");
  post("RequestManualChecks");
  post(deep ? "RunDeepScan" : "RunFullScan");
}

scanBtn.addEventListener("click", () => startScan(false));
deepScanBtn.addEventListener("click", () => startScan(true));

document.getElementById("filter").addEventListener("input", rerender);

document.getElementById("resetBtn").addEventListener("click", resetFilters);

document.getElementById("exportBtn").addEventListener("click", (e) => {
  if (!hasAnythingToShow()) return;
  lastActionAnchor = e.currentTarget;
  setStatus("Generating report…", false);
  // De UI bouwt de HTML; C# schrijft 'm weg + opent (en meldt het pad terug).
  post("ExportHtml", { html: buildReportHtml() });
});
