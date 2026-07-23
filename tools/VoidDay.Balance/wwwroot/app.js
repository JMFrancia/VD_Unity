// VoidDay Balance Workbench — the browser client (M04).
//
// It is a CLIENT of the same reader/writer/runner the CLI uses; it holds NO economy logic. Every
// field of BalanceConfig is editable here and round-trips through the version save/load endpoints
// (plain JSON). Push-to-Unity funnels through the M02 writer via /api/write, which supports scalar
// edits + recipe insertion and refuses the rest — so the push modal shows either a change summary or
// the writer's refusal verbatim, never silently dropping an edit.
//
// No build step: htm + preact are vendored as one ESM file (spec M04). Charts (M06) use Chart.js 4.x,
// vendored as a UMD global (window.Chart, loaded by a <script> in index.html) — no CDN, no npm.
import {
  html, render, useState, useEffect, useRef,
} from '/vendor/htm-preact-standalone.module.js';

// ---- the vocabularies the game actually honours ----
const TABS = ['Global', 'Resources', 'Recipes', 'Stations', 'Upgrades', 'Levels', 'Orders'];
// Only these six EffectTypes have resolver teeth (spec); offering the others would author a no-op.
const EFFECT_TYPES = ['StationSpeed', 'StationYield', 'StationCost', 'StationQueueDepth', 'XpGain', 'StorageCap'];
const EFFECT_OPS = ['Flat', 'Pct', 'Mult'];
// LevelEntryKind minus StationType/Upgrade (BootValidator forbids granting those on a level).
const GRANT_KINDS = ['Money', 'Gems', 'StationCap', 'QueueDepth', 'OrderSlots'];

// ---- module-level touch hook: inputs mutate config in place and call this to re-render + mark dirty.
// App assigns it every render (cheap); this keeps every leaf input from having to thread a prop. ----
let touch = () => {};

// ================= tiny API layer =================
async function api(method, path, body) {
  const res = await fetch(path, {
    method,
    headers: body ? { 'Content-Type': 'application/json' } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  const data = text ? JSON.parse(text) : null;
  if (!res.ok) throw new Error(data && data.error ? data.error : `${method} ${path} → ${res.status}`);
  return data;
}
const getVersions = () => api('GET', '/api/versions');
const getConfig = (name) => api('GET', `/api/config?name=${encodeURIComponent(name)}`);
const putConfig = (name, config) => api('PUT', `/api/config?name=${encodeURIComponent(name)}`, config);
const saveAsVersion = (name, config) => api('POST', '/api/versions', { name, config });
const deleteVersion = (name) => api('DELETE', `/api/versions?name=${encodeURIComponent(name)}`);
const runSim = (config) => api('POST', '/api/sim', { config, seed: 1 });
// M06: a multi-seed sweep — server returns { sweep: aggregate, seeds: [{seed,table,…}] }.
const runSweep = (config, seeds, profile) => api('POST', '/api/sim', { config, seeds: Number(seeds), profile });
const planWrite = (config, apply) => api('POST', '/api/write', { config, apply });
const getSessions = () => api('GET', '/api/sessions');
const getSession = (name) => api('GET', `/api/session?name=${encodeURIComponent(name)}`);

// ================= validation (mirrors BootValidator, client-side) =================
function validate(c) {
  const e = [];
  if (!c) return e;
  const resIds = new Set(c.Resources.map((r) => r.Id));
  const stationTypes = new Set(c.Stations.map((s) => s.StationType));
  const recipeIds = new Set(c.Recipes.map((r) => r.Id));

  dupCheck(c.Resources.map((r) => r.Id), 'resource id', e);
  dupCheck(c.Recipes.map((r) => r.Id), 'recipe id', e);
  dupCheck(c.Stations.map((s) => s.StationType), 'station type', e);
  dupCheck(c.Upgrades.map((u) => u.Id), 'upgrade id', e);

  c.Recipes.forEach((r) => {
    if (!r.Id) e.push('a recipe has an empty id');
    if (!stationTypes.has(r.StationType)) e.push(`recipe '${r.Id}': station '${r.StationType}' does not exist`);
    if (!r.Outputs || r.Outputs.length === 0) e.push(`recipe '${r.Id}': must have at least one output`);
    [...(r.Inputs || []), ...(r.Outputs || [])].forEach((q) => {
      if (!resIds.has(q.Resource)) e.push(`recipe '${r.Id}': resource '${q.Resource}' does not exist`);
      if (q.Amount <= 0) e.push(`recipe '${r.Id}': ingredient amount must be > 0`);
    });
  });

  c.Stations.forEach((s) => (s.RecipeIds || []).forEach((id) => {
    if (!recipeIds.has(id)) e.push(`station '${s.StationType}': recipe '${id}' does not exist`);
  }));

  const L = c.Levels || [];
  if (L.length) {
    if (L[0].XpThreshold !== 0) e.push('level 1 XpThreshold must be 0 (the level every run starts at)');
    if (L[0].Grants && L[0].Grants.length) e.push('level 1 must have no grants (it is never crossed)');
    for (let i = 1; i < L.length; i++) {
      if (L[i].XpThreshold <= L[i - 1].XpThreshold) {
        e.push(`level ${i + 1} XpThreshold (${L[i].XpThreshold}) must exceed level ${i}'s (${L[i - 1].XpThreshold})`);
      }
      let rewards = 0;
      (L[i].Grants || []).forEach((g) => {
        if (g.Kind === 'StationType' || g.Kind === 'Upgrade') e.push(`level ${i + 1}: cannot grant ${g.Kind}`);
        if (g.Amount <= 0) e.push(`level ${i + 1}: ${g.Kind} grant amount must be > 0`);
        if (g.Kind === 'StationCap' && !g.TargetStation) e.push(`level ${i + 1}: a StationCap grant must name a targetStation`);
        if (g.Kind === 'Money' || g.Kind === 'Gems') rewards++;
      });
      if (rewards > 1) e.push(`level ${i + 1}: at most one reward grant (Money or Gems)`);
    }
  }

  const six = new Set(EFFECT_TYPES);
  c.Upgrades.forEach((u) => {
    if (!u.Id) e.push('an upgrade has an empty id');
    (u.Tiers || []).forEach((t, ti) => (t.Effects || []).forEach((ef) => {
      if (!six.has(ef.Type)) e.push(`upgrade '${u.Id}' tier ${ti + 1}: effect type '${ef.Type}' is not one of the six with teeth`);
      if (ef.TriggerChance < 0 || ef.TriggerChance > 100) e.push(`upgrade '${u.Id}' tier ${ti + 1}: triggerChance must be within 0–100`);
    }));
  });
  return e;
}
function dupCheck(ids, label, e) {
  const seen = new Set();
  for (const id of ids) {
    if (seen.has(id)) e.push(`duplicate ${label} '${id}'`);
    seen.add(id);
  }
}

// ================= leaf inputs (mutate in place, then touch) =================
function Num({ obj, k, step, int }) {
  return html`<input type="number" step=${int ? '1' : (step || 'any')} value=${obj[k]}
    onInput=${(ev) => { obj[k] = ev.target.value === '' ? 0 : Number(ev.target.value); touch(); }} />`;
}
function Txt({ obj, k, ph }) {
  return html`<input type="text" placeholder=${ph || ''} value=${obj[k]}
    onInput=${(ev) => { obj[k] = ev.target.value; touch(); }} />`;
}
function Bool({ obj, k }) {
  return html`<input type="checkbox" checked=${!!obj[k]}
    onChange=${(ev) => { obj[k] = ev.target.checked; touch(); }} />`;
}
function Sel({ obj, k, options }) {
  return html`<select value=${obj[k]} onChange=${(ev) => { obj[k] = ev.target.value; touch(); }}>
    ${options.map((o) => html`<option value=${o}>${o}</option>`)}
  </select>`;
}
const Fld = ({ label, children }) => html`<div class="fld"><label>${label}</label>${children}</div>`;

// ================= tabs =================
function GlobalTab({ c }) {
  const g = c.Global;
  return html`
    <div class="card"><h3>Global</h3><div class="grid">
      ${Fld({ label: 'Grid Cols', children: html`<${Num} obj=${g} k="GridCols" int />` })}
      ${Fld({ label: 'Grid Rows', children: html`<${Num} obj=${g} k="GridRows" int />` })}
      ${Fld({ label: 'Cell Size', children: html`<${Num} obj=${g} k="CellSize" />` })}
      ${Fld({ label: 'Refund %', children: html`<${Num} obj=${g} k="RefundPercent" />` })}
      ${Fld({ label: 'Start Storage Cap', children: html`<${Num} obj=${g} k="StartingStorageCapacity" int />` })}
    </div></div>
    <div class="card"><h3>XP</h3><div class="grid">
      ${Fld({ label: 'Per Job Collected', children: html`<${Num} obj=${c.Xp} k="PerJobCollected" int />` })}
      ${Fld({ label: 'Per Station Built', children: html`<${Num} obj=${c.Xp} k="PerStationBuilt" int />` })}
    </div></div>
    <div class="card"><h3>Gems</h3><div class="grid">
      ${Fld({ label: 'Starting Gems', children: html`<${Num} obj=${c.Gems} k="StartingGems" int />` })}
      ${Fld({ label: 'Seconds / Gem', children: html`<${Num} obj=${c.Gems} k="SecondsPerGem" />` })}
      ${Fld({ label: 'Min Gem Cost', children: html`<${Num} obj=${c.Gems} k="MinGemCost" int />` })}
    </div></div>
    <div class="card"><h3>Starting Resources</h3>
      <div class="tblwrap"><table><thead><tr><th>Resource</th><th>Amount</th></tr></thead><tbody>
        ${g.StartingResources.map((q) => html`<tr>
          <td class="ro">${q.Resource}</td><td><${Num} obj=${q} k="Amount" int /></td></tr>`)}
      </tbody></table></div>
      <p class="hint">Editing this list is refused by the writer (push-to-Unity); it round-trips through save/load.</p>
    </div>
    <div class="card"><h3>Starting Stations <span class="muted small">(scene-owned, read-only)</span></h3>
      ${g.StartingStations.map((s) => html`<span class="chip">${s.Count}× ${s.StationType}</span>`)}
    </div>`;
}

function ResourcesTab({ c }) {
  return html`<div class="card"><h3>Resources <span class="muted small">(edit-only)</span></h3>
    <div class="tblwrap"><table>
      <thead><tr><th>Id</th><th>Display</th><th>Base Value</th><th>Sellable</th><th>Tier</th></tr></thead>
      <tbody>${c.Resources.map((r) => html`<tr>
        <td class="ro">${r.Id}</td><td class="ro">${r.DisplayName}</td>
        <td><${Num} obj=${r} k="BaseValue" int /></td>
        <td><${Bool} obj=${r} k="Sellable" /></td>
        <td><${Num} obj=${r} k="Tier" int /></td></tr>`)}</tbody>
    </table></div></div>`;
}

function IngredientChips({ list }) {
  if (!list || !list.length) return html`<span class="muted">—</span>`;
  return list.map((q) => html`<span class="chip">${q.Amount}× ${q.Resource}</span>`);
}

function RecipesTab({ c, force }) {
  const stationOpts = c.Stations.map((s) => s.StationType);
  const resourceOpts = c.Resources.map((r) => r.Id);
  return html`
    <div class="card"><h3>Recipes</h3>
      <div class="tblwrap"><table>
        <thead><tr><th>Id</th><th>Station</th><th>Inputs</th><th>Outputs</th><th>Duration (s)</th></tr></thead>
        <tbody>${c.Recipes.map((r) => html`<tr>
          <td class="ro">${r.Id}</td><td class="ro">${r.StationType}</td>
          <td><${IngredientChips} list=${r.Inputs} /></td>
          <td><${IngredientChips} list=${r.Outputs} /></td>
          <td><${Num} obj=${r} k="Duration" /></td></tr>`)}</tbody>
      </table></div>
      <p class="hint">Duration is writer-editable. Inputs/outputs are refused by the writer (they round-trip via save/load).</p>
    </div>
    <${AddRecipe} c=${c} stationOpts=${stationOpts} resourceOpts=${resourceOpts} force=${force} />`;
}

function AddRecipe({ c, stationOpts, resourceOpts, force }) {
  const [draft, setDraft] = useState(() => newRecipeDraft(stationOpts[0]));
  const addIng = (side) => { draft[side].push({ Resource: resourceOpts[0], Amount: 1 }); setDraft({ ...draft }); };
  const ingRows = (side) => draft[side].map((q, i) => html`<div class="row">
    <${Sel} obj=${q} k="Resource" options=${resourceOpts} />
    <${Num} obj=${q} k="Amount" int />
    <button onClick=${() => { draft[side].splice(i, 1); setDraft({ ...draft }); }}>✕</button>
  </div>`);
  const submit = () => {
    if (!draft.Id) { alert('recipe id is required'); return; }
    if (c.Recipes.some((r) => r.Id === draft.Id)) { alert(`recipe '${draft.Id}' already exists`); return; }
    c.Recipes.push(structuredClone(draft));
    setDraft(newRecipeDraft(stationOpts[0]));
    touch(); force();
  };
  return html`<div class="card"><h3>Add Recipe <span class="muted small">(writer-supported insertion)</span></h3>
    <div class="row">
      <label class="muted">id</label><${Txt} obj=${draft} k="Id" ph="field.beetGrow" />
      <label class="muted">station</label><${Sel} obj=${draft} k="StationType" options=${stationOpts} />
      <label class="muted">duration</label><${Num} obj=${draft} k="Duration" />
    </div>
    <div class="sub"><div class="row"><b class="small">Inputs</b>
      <button class="add" onClick=${() => addIng('Inputs')}>+ ingredient</button></div>${ingRows('Inputs')}</div>
    <div class="sub"><div class="row"><b class="small">Outputs</b>
      <button class="add" onClick=${() => addIng('Outputs')}>+ ingredient</button></div>${ingRows('Outputs')}</div>
    <button class="primary" onClick=${submit}>Add recipe</button>
  </div>`;
}
const newRecipeDraft = (station) => ({ Id: '', StationType: station || '', Inputs: [], Outputs: [], Duration: 5 });

function StationsTab({ c }) {
  return html`<div class="card"><h3>Stations <span class="muted small">(edit-only)</span></h3>
    <div class="tblwrap"><table>
      <thead><tr>
        <th>Type</th><th>Display</th><th>Buildable</th><th>Cost</th><th>Cap</th><th>Unlock</th>
        <th>Queue</th><th>W</th><th>H</th><th>Build s</th><th>Recipes</th><th>Upgrades</th>
      </tr></thead>
      <tbody>${c.Stations.map((s) => html`<tr>
        <td class="ro">${s.StationType}</td><td class="ro">${s.DisplayName}</td>
        <td><${Bool} obj=${s} k="Buildable" /></td>
        <td><${Num} obj=${s} k="BuildCost" int /></td>
        <td><${Num} obj=${s} k="Cap" int /></td>
        <td><${Num} obj=${s} k="UnlockLevel" int /></td>
        <td><${Num} obj=${s} k="QueueDepth" int /></td>
        <td><${Num} obj=${s} k="Width" int /></td>
        <td><${Num} obj=${s} k="Height" int /></td>
        <td><${Num} obj=${s} k="BuildSeconds" /></td>
        <td class="ro">${(s.RecipeIds || []).map((r) => html`<span class="chip">${r}</span>`)}</td>
        <td class="ro">${(s.UpgradeIds || []).map((u) => html`<span class="chip">${u}</span>`)}</td>
      </tr>`)}</tbody>
    </table></div></div>`;
}

function UpgradesTab({ c, force }) {
  return html`${c.Upgrades.map((u) => html`<${UpgradeCard} u=${u} force=${force} />`)}
    <p class="hint">Upgrade edits round-trip via save/load; the writer refuses pushing them to Unity (nested collection).</p>`;
}
function UpgradeCard({ u, force }) {
  const addTier = () => {
    u.Tiers.push({ Cost: 0, Effects: [newEffect()] });
    touch(); force();
  };
  return html`<div class="card"><h3>${u.Id} <span class="muted small">${u.DisplayName}</span></h3>
    <div class="grid">
      ${Fld({ label: 'Unlock Level', children: html`<${Num} obj=${u} k="UnlockLevel" int />` })}
    </div>
    ${u.Tiers.map((t, ti) => html`<div class="sub">
      <div class="row"><b class="small">Tier ${ti + 1}</b>
        <label class="muted">cost</label><${Num} obj=${t} k="Cost" int />
        <button class="add" onClick=${() => { t.Effects.push(newEffect()); touch(); force(); }}>+ effect</button>
      </div>
      ${t.Effects.map((ef, ei) => html`<div class="row">
        <${Sel} obj=${ef} k="Type" options=${EFFECT_TYPES} />
        <${Sel} obj=${ef} k="Op" options=${EFFECT_OPS} />
        <label class="muted">amt</label><${Num} obj=${ef} k="Amount" />
        <label class="muted">resource</label><${Txt} obj=${ef} k="Resource" ph="(optional)" />
        <label class="muted">trigger%</label><${Num} obj=${ef} k="TriggerChance" int />
        <button onClick=${() => { t.Effects.splice(ei, 1); touch(); force(); }}>✕</button>
      </div>`)}
    </div>`)}
    <button class="add" onClick=${addTier}>+ tier</button>
  </div>`;
}
const newEffect = () => ({
  Id: '', Type: EFFECT_TYPES[0], Op: 'Flat', Amount: 0, Resource: '', Range: 0,
  Trigger: 'None', TriggerChance: 100, ConditionType: 'None', ConditionArg: '', ConditionAmount: 0,
});

function LevelsTab({ c, force }) {
  const stationOpts = ['', ...c.Stations.map((s) => s.StationType)];
  const addLevel = () => {
    const last = c.Levels.length ? c.Levels[c.Levels.length - 1].XpThreshold : 0;
    c.Levels.push({ XpThreshold: last + 100, Grants: [] });
    touch(); force();
  };
  return html`<div class="card"><h3>Levels <span class="muted small">(index 0 = level 1)</span></h3>
    <div class="tblwrap"><table>
      <thead><tr><th>Level</th><th>XP Threshold</th><th>Grants</th></tr></thead>
      <tbody>${c.Levels.map((lv, i) => html`<tr>
        <td class="ro">${i + 1}</td>
        <td><${Num} obj=${lv} k="XpThreshold" int /></td>
        <td><${GrantEditor} lv=${lv} level=${i + 1} stationOpts=${stationOpts} force=${force} /></td>
      </tr>`)}</tbody>
    </table></div>
    <button class="add" onClick=${addLevel}>+ level row</button>
    <p class="hint">Adding a level row round-trips via save/load; the writer refuses pushing level edits to Unity.</p>
  </div>`;
}
function GrantEditor({ lv, level, stationOpts, force }) {
  if (level === 1) return html`<span class="muted">(none — level 1 is never crossed)</span>`;
  const add = () => { lv.Grants.push({ Kind: 'Money', TargetStation: null, Amount: 1 }); touch(); force(); };
  return html`<div>
    ${lv.Grants.map((g, gi) => html`<div class="row">
      <${Sel} obj=${g} k="Kind" options=${GRANT_KINDS} />
      <${Sel} obj=${g} k="TargetStation" options=${stationOpts} />
      <label class="muted">amt</label><${Num} obj=${g} k="Amount" int />
      <button onClick=${() => { lv.Grants.splice(gi, 1); touch(); force(); }}>✕</button>
    </div>`)}
    <button class="add small" onClick=${add}>+ grant</button>
  </div>`;
}

function OrdersTab({ c }) {
  const o = c.Orders;
  return html`<div class="card"><h3>Order Config</h3><div class="grid">
    ${Fld({ label: 'Slot Count', children: html`<${Num} obj=${o} k="SlotCount" int />` })}
    ${Fld({ label: 'Refill Seconds', children: html`<${Num} obj=${o} k="RefillSeconds" />` })}
    ${Fld({ label: 'Min Request Kinds', children: html`<${Num} obj=${o} k="MinRequestKinds" int />` })}
    ${Fld({ label: 'Max Request Kinds', children: html`<${Num} obj=${o} k="MaxRequestKinds" int />` })}
    ${Fld({ label: 'Max Qty @ L1', children: html`<${Num} obj=${o} k="MaxQuantityAtLevel1" />` })}
    ${Fld({ label: 'Max Qty / Level', children: html`<${Num} obj=${o} k="MaxQuantityPerLevel" />` })}
    ${Fld({ label: 'Tier Weight Base', children: html`<${Num} obj=${o} k="TierWeightBase" />` })}
    ${Fld({ label: 'Tier Weight / Level', children: html`<${Num} obj=${o} k="TierWeightPerLevel" />` })}
    ${Fld({ label: 'Cash Multiplier', children: html`<${Num} obj=${o} k="CashMultiplier" />` })}
    ${Fld({ label: 'XP Multiplier', children: html`<${Num} obj=${o} k="XpMultiplier" />` })}
  </div></div>`;
}

const TAB_VIEWS = {
  Global: GlobalTab, Resources: ResourcesTab, Recipes: RecipesTab, Stations: StationsTab,
  Upgrades: UpgradesTab, Levels: LevelsTab, Orders: OrdersTab,
};

// ================= modal =================
function Modal({ title, onClose, children, actions }) {
  return html`<div class="modal-bg" onClick=${onClose}>
    <div class="modal" onClick=${(e) => e.stopPropagation()}>
      <h3>${title}</h3>${children}
      <div class="actions">${actions}</div>
    </div>
  </div>`;
}

// ================= M06 reports: sweep charts + A/B comparison =================
// The browser holds NO economy logic here either: it POSTs a config to /api/sim with a seed count and
// renders the aggregate the server computes. Chart.js is a UMD global (window.Chart). A is the primary
// (baseline) config; B is the optional candidate. Charts 1 and 3 overlay both; the heatmap, composition
// and purchase-timeline show A (spec M06: "charts 1 and 3 overlay both").
const ACC = '#5aa6ff';      // config A
const AMB = '#e0a83b';      // config B
const BAND_A = 'rgba(90,166,255,0.18)';
const BAND_B = 'rgba(224,168,59,0.16)';
const OKC = '#6bbf8a';
const GRIDC = 'rgba(255,255,255,0.07)';
const TICKC = '#8b93a3';

const PROFILES = ['typical', 'perfect'];
const minutes = (s) => s / 60;
const round2 = (v) => Math.round(v * 100) / 100;

// Align two sweeps on the union of the levels they reached (a config that stalls short leaves gaps, not lies).
function levelUnion(...sweeps) {
  const s = new Set();
  sweeps.forEach((sw) => sw && sw.Levels.forEach((l) => s.add(l.Level)));
  return [...s].sort((a, b) => a - b);
}
function byLevel(sw) {
  const m = {};
  sw.Levels.forEach((l) => { m[l.Level] = l; });
  return m;
}
// Every pressure family either sweep recorded, sorted — the heatmap/delta columns.
function familyUnion(...sweeps) {
  const s = new Set();
  sweeps.forEach((sw) => sw && sw.Levels.forEach((l) => Object.keys(l.Pressure).forEach((f) => s.add(f))));
  return [...s].sort();
}

function baseOptions(yTitle, extra) {
  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: { labels: { color: TICKC, boxWidth: 12, font: { size: 11 } } },
      tooltip: { callbacks: {} },
    },
    scales: {
      x: { ticks: { color: TICKC }, grid: { color: GRIDC }, stacked: !!(extra && extra.stacked) },
      y: {
        beginAtZero: true, stacked: !!(extra && extra.stacked),
        title: { display: !!yTitle, text: yTitle, color: TICKC },
        ticks: { color: TICKC }, grid: { color: GRIDC },
      },
    },
    ...(extra && extra.options ? extra.options : {}),
  };
}

// Two transparent line datasets forming a shaded p10–p90 band (spec M06: "two line datasets with fill: '-1'").
function bandDatasets(labels, sw, sel, band, tag) {
  const map = byLevel(sw);
  const p90 = labels.map((n) => (map[n] ? sel(map[n]).P90 : null));
  const p10 = labels.map((n) => (map[n] ? sel(map[n]).P10 : null));
  return [
    { type: 'line', label: `${tag} p90`, data: p90, borderColor: 'transparent', pointRadius: 0, fill: false, spanGaps: false },
    { type: 'line', label: `${tag} p10–p90`, data: p10, borderColor: 'transparent', pointRadius: 0, backgroundColor: band, fill: '-1', spanGaps: false },
  ];
}
function medianSeries(labels, sw, sel) {
  const map = byLevel(sw);
  return labels.map((n) => (map[n] ? sel(map[n]).Median : null));
}

// Chart 1 — time per level: median bar + p10–p90 band (the headline).
function makeTimePerLevel(A, B) {
  const lv = levelUnion(A, B);
  const labels = lv.map((n) => `L${n}`);
  const durMin = (l) => ({ Median: minutes(l.Duration.Median), P10: minutes(l.Duration.P10), P90: minutes(l.Duration.P90) });
  const ds = [];
  ds.push(...bandDatasets(lv, A, durMin, BAND_A, 'A'));
  ds.push({ type: 'bar', label: B ? 'A median' : 'median (min)', data: medianSeries(lv, A, durMin), backgroundColor: ACC });
  if (B) {
    ds.push(...bandDatasets(lv, B, durMin, BAND_B, 'B'));
    ds.push({ type: 'bar', label: 'B median', data: medianSeries(lv, B, durMin), backgroundColor: AMB });
  }
  return { type: 'bar', data: { labels, datasets: ds }, options: baseOptions('minutes') };
}

// Chart 2 — time composition: acting vs waiting per level (A), stacked.
function makeComposition(A) {
  const lv = levelUnion(A);
  const labels = lv.map((n) => `L${n}`);
  const map = byLevel(A);
  return {
    type: 'bar',
    data: {
      labels,
      datasets: [
        { label: 'acting (min)', data: lv.map((n) => minutes(map[n].Acting.Median)), backgroundColor: ACC },
        { label: 'waiting (min)', data: lv.map((n) => minutes(map[n].Waiting.Median)), backgroundColor: TICKC },
      ],
    },
    options: baseOptions('minutes', { stacked: true }),
  };
}

// Chart 3 — money entry/exit per level, with band; A/B overlays both configs' exit + entry lines.
function makeMoney(A, B) {
  const lv = levelUnion(A, B);
  const labels = lv.map((n) => `L${n}`);
  const ds = [];
  if (!B) {
    ds.push(...bandDatasets(lv, A, (l) => l.MoneyExit, BAND_A, 'exit'));
    ds.push({ type: 'line', label: '$ exit (median)', data: medianSeries(lv, A, (l) => l.MoneyExit), borderColor: AMB, backgroundColor: AMB, pointRadius: 2, fill: false });
    ds.push({ type: 'line', label: '$ entry (median)', data: medianSeries(lv, A, (l) => l.MoneyEntry), borderColor: OKC, backgroundColor: OKC, pointRadius: 2, fill: false });
  } else {
    ds.push({ type: 'line', label: 'A $ exit', data: medianSeries(lv, A, (l) => l.MoneyExit), borderColor: ACC, backgroundColor: ACC, pointRadius: 2, fill: false });
    ds.push({ type: 'line', label: 'A $ entry', data: medianSeries(lv, A, (l) => l.MoneyEntry), borderColor: ACC, borderDash: [4, 3], pointRadius: 0, fill: false });
    ds.push({ type: 'line', label: 'B $ exit', data: medianSeries(lv, B, (l) => l.MoneyExit), borderColor: AMB, backgroundColor: AMB, pointRadius: 2, fill: false });
    ds.push({ type: 'line', label: 'B $ entry', data: medianSeries(lv, B, (l) => l.MoneyEntry), borderColor: AMB, borderDash: [4, 3], pointRadius: 0, fill: false });
  }
  return { type: 'line', data: { labels, datasets: ds }, options: baseOptions('$') };
}

// Chart 5 — purchase timeline: for each remedy, the level at which it is first bought (p10–p90 floating bar).
function makePurchaseTimeline(A) {
  const ps = A.Purchases;
  const labels = ps.map((p) => `${p.Kind} ${p.Target.replace(/^field\.|^silo\.|Job\(|\)$/g, '')}`);
  const opts = baseOptions('level', {
    options: {
      indexAxis: 'y',
      plugins: {
        legend: { display: false },
        tooltip: { callbacks: { label: (ctx) => {
          const p = ps[ctx.dataIndex];
          return [`median L${round2(p.FirstLevel.Median)}`, `p10 L${round2(p.FirstLevel.P10)} – p90 L${round2(p.FirstLevel.P90)}`,
            `${p.SeedsBought} seed(s) · for ${p.ForPressure}`];
        } } },
      },
    },
  });
  // Horizontal chart: swap the axis roles so x is the level.
  opts.scales = {
    x: { beginAtZero: true, title: { display: true, text: 'level', color: TICKC }, ticks: { color: TICKC }, grid: { color: GRIDC } },
    y: { ticks: { color: TICKC, font: { size: 11 } }, grid: { color: GRIDC } },
  };
  return {
    type: 'bar',
    data: { labels, datasets: [{ label: 'first-bought level', data: ps.map((p) => [p.FirstLevel.P10, p.FirstLevel.P90]), backgroundColor: BAND_A, borderColor: ACC, borderWidth: 1, borderSkipped: false }] },
    options: opts,
  };
}

function ChartBox({ title, make, deps, span, height }) {
  const cvs = useRef(null);
  const chart = useRef(null);
  useEffect(() => {
    if (chart.current) { chart.current.destroy(); chart.current = null; }
    if (cvs.current && window.Chart) chart.current = new window.Chart(cvs.current, make());
    return () => { if (chart.current) { chart.current.destroy(); chart.current = null; } };
  }, deps);
  return html`<div class="card ${span ? 'span2' : ''}"><h3>${title}</h3>
    <div class="chartwrap" style="height:${height || 260}px"><canvas ref=${cvs}></canvas></div></div>`;
}

// Chart 4 — pressure heatmap (level × family, gross seconds lost). An HTML/CSS table, not a Chart.js chart:
// it needs a 2-D colour grid with no extra vendored plugin, and reads better than any line chart would.
function Heatmap({ A }) {
  const lv = levelUnion(A);
  const fams = familyUnion(A);
  const map = byLevel(A);
  let max = 0;
  lv.forEach((n) => fams.forEach((f) => { const v = map[n].Pressure[f]; if (v) max = Math.max(max, v.Median); }));
  const cellBg = (v) => {
    if (!v || max <= 0) return 'transparent';
    const a = 0.12 + 0.75 * Math.sqrt(v / max); // sqrt so mid values stay legible
    return `rgba(224,96,96,${a})`;
  };
  return html`<div class="card span2"><h3>Pressure heatmap — level × category (gross seconds lost)</h3>
    <div class="tblwrap"><table class="heat">
      <thead><tr><th>Lvl</th>${fams.map((f) => html`<th>${f}</th>`)}</tr></thead>
      <tbody>${lv.map((n) => html`<tr><td class="lvl">L${n}</td>
        ${fams.map((f) => {
          const v = map[n].Pressure[f] ? map[n].Pressure[f].Median : 0;
          return html`<td class="cell" style="background:${cellBg(v)}" title="L${n} ${f}: ${round2(v)}s (median of ${map[n].SeedsReached} seeds)">${v >= 1 ? Math.round(v) : ''}</td>`;
        })}</tr>`)}</tbody>
    </table></div>
    <p class="hint">Gross of gem relief (M03 invariant). Parametrised keys (Capacity:field, Supply:corn) are aggregated into families.</p>
  </div>`;
}

function SeedStrip({ label, seeds, onOpen }) {
  return html`<div class="card"><h3>${label} — ${seeds.length} seeds (click to open a run)</h3>
    <div class="seedstrip">${seeds.map((s) => html`<button title="stop: ${s.stop}"
      onClick=${() => onOpen(s)}>#${s.seed} · L${s.levelReached} · ${s.totalMinutes.toFixed(1)}m</button>`)}</div>
  </div>`;
}

// Per-level A→B delta table. Self-vs-self must read all-zero (the control that proves the tool measures the
// game, not seed noise) — so an exact zero renders as "—", not "+0.00".
function DeltaTable({ A, B }) {
  const lv = levelUnion(A, B);
  const fams = familyUnion(A, B);
  const ma = byLevel(A); const mb = byLevel(B);
  const cell = (a, b, digits) => {
    if (a == null || b == null) return html`<td class="zero">·</td>`;
    const d = b - a;
    const cls = d > 0 ? 'up' : (d < 0 ? 'down' : 'zero');
    const txt = d === 0 ? '—' : `${d > 0 ? '▲ +' : '▼ '}${d.toFixed(digits)}`;
    return html`<td class=${cls}>${txt}</td>`;
  };
  const get = (m, n, sel) => (m[n] ? sel(m[n]) : null);
  return html`<div class="card span2"><h3>Per-level delta — B minus A (▲ up, ▼ down)</h3>
    <div class="tblwrap"><table class="delta">
      <thead><tr><th>Lvl</th><th>Δ duration (min)</th><th>Δ $ exit</th>
        ${fams.map((f) => html`<th>Δ ${f} (s)</th>`)}</tr></thead>
      <tbody>${lv.map((n) => html`<tr>
        <td class="ro">L${n}</td>
        ${cell(get(ma, n, (l) => minutes(l.Duration.Median)), get(mb, n, (l) => minutes(l.Duration.Median)), 2)}
        ${cell(get(ma, n, (l) => l.MoneyExit.Median), get(mb, n, (l) => l.MoneyExit.Median), 0)}
        ${fams.map((f) => cell(
          ma[n] ? (ma[n].Pressure[f] ? ma[n].Pressure[f].Median : 0) : null,
          mb[n] ? (mb[n].Pressure[f] ? mb[n].Pressure[f].Median : 0) : null, 1))}
      </tr>`)}</tbody>
    </table></div></div>`;
}

function Reports({ versions }) {
  const [aName, setAName] = useState('baseline');
  const [bName, setBName] = useState('');
  const [seeds, setSeeds] = useState(30);
  const [profile, setProfile] = useState('typical');
  const [data, setData] = useState(null); // { A:{sweep,seeds}, B:{sweep,seeds}|null }
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [openSeed, setOpenSeed] = useState(null);

  const run = async () => {
    setBusy(true); setError(null); setData(null);
    try {
      const cfgA = await getConfig(aName);
      const A = await runSweep(cfgA, seeds, profile);
      let B = null;
      if (bName) { const cfgB = await getConfig(bName); B = await runSweep(cfgB, seeds, profile); }
      setData({ A, B });
    } catch (e) { setError(e.message); }
    setBusy(false);
  };

  const A = data && data.A ? data.A.sweep : null;
  const B = data && data.B ? data.B.sweep : null;
  const deps = [data];

  return html`
    <div class="rep-controls">
      ${Fld({ label: 'A (baseline)', children: html`<select value=${aName} onChange=${(e) => setAName(e.target.value)}>
        ${versions.map((v) => html`<option value=${v}>${v}</option>`)}</select>` })}
      ${Fld({ label: 'B (candidate, optional)', children: html`<select value=${bName} onChange=${(e) => setBName(e.target.value)}>
        <option value="">— none —</option>${versions.map((v) => html`<option value=${v}>${v}</option>`)}</select>` })}
      ${Fld({ label: 'Seeds', children: html`<input type="number" value=${seeds} min="1" max="200"
        onInput=${(e) => setSeeds(e.target.value === '' ? 1 : Number(e.target.value))} />` })}
      ${Fld({ label: 'Profile', children: html`<select value=${profile} onChange=${(e) => setProfile(e.target.value)}>
        ${PROFILES.map((p) => html`<option value=${p}>${p}</option>`)}</select>` })}
      <button class="primary" onClick=${run} disabled=${busy}>${busy ? 'Running…' : `Run ${seeds}-seed sweep`}</button>
    </div>
    ${error ? html`<div class="errs"><b>Sweep error</b><div>${error}</div></div>` : null}
    ${busy ? html`<p class="muted">Running ${seeds} seeds${bName ? ' × 2 configs' : ''}…</p>` : null}
    ${A ? html`
      <div class="legend">
        <span><span class="swatch" style="background:${ACC}"></span>${A.ConfigName} (A) — median ${A.TotalMinutes.Median.toFixed(1)}m, reached L${A.LevelReached.Median}</span>
        ${B ? html`<span><span class="swatch" style="background:${AMB}"></span>${B.ConfigName} (B) — median ${B.TotalMinutes.Median.toFixed(1)}m, reached L${B.LevelReached.Median}</span>` : null}
      </div>
      <div class="chart-grid">
        <${ChartBox} title="Time per level (median + p10–p90)" make=${() => makeTimePerLevel(A, B)} deps=${deps} />
        <${ChartBox} title="Money — entry & exit per level" make=${() => makeMoney(A, B)} deps=${deps} />
        <${ChartBox} title="Time composition — acting vs waiting (A)" make=${() => makeComposition(A)} deps=${deps} />
        <${ChartBox} title="Purchase timeline — first-bought level (A)" make=${() => makePurchaseTimeline(A)} deps=${deps} height=${Math.max(160, 28 * A.Purchases.length + 40)} />
        <${Heatmap} A=${A} />
        ${B ? html`<${DeltaTable} A=${A} B=${B} />` : null}
      </div>
      <${SeedStrip} label=${`${A.ConfigName} (A)`} seeds=${data.A.seeds} onOpen=${(s) => setOpenSeed({ which: 'A', s })} />
      ${B ? html`<${SeedStrip} label=${`${B.ConfigName} (B)`} seeds=${data.B.seeds} onOpen=${(s) => setOpenSeed({ which: 'B', s })} />` : null}
    ` : (!busy ? html`<p class="muted">Pick a version and run a sweep. Add a B config to overlay and compare.</p>` : null)}
    ${openSeed ? html`<${Modal} title=${`Seed #${openSeed.s.seed} (${openSeed.which}) — reproduces \`balance sim --seed ${openSeed.s.seed}\``}
      onClose=${() => setOpenSeed(null)} actions=${html`<button class="primary" onClick=${() => setOpenSeed(null)}>Close</button>`}>
      <pre>${openSeed.s.table}</pre></${Modal}>` : null}`;
}

// ================= live session view (M07) =================
// The agent iterates in the terminal (`eval --session … --rationale …`); this view polls the active session
// directory and re-renders as iterations land. It holds NO economy logic: the loss curve comes straight from
// journal.jsonl, and the pressure heatmap / per-level times come from re-simming config.current via /api/sim.
const LIVE_SEEDS = 5;   // a quick preview sweep on each new iteration — not the 30-seed Reports sweep.

function makeLossCurve(journal) {
  const labels = journal.map((r) => `#${r.Iteration}`);
  const data = journal.map((r) => r.Loss);
  return {
    type: 'line',
    data: { labels, datasets: [{ label: 'loss', data, borderColor: ACC, backgroundColor: ACC, pointRadius: 3, fill: false, tension: 0.15, spanGaps: false }] },
    options: baseOptions('loss'),
  };
}

function GoalSummary({ goal }) {
  if (!goal || !goal.Targets) return null;
  return html`<div class="card"><h3>Goal — ${goal.Name}</h3>
    <div class="tblwrap"><table>
      <thead><tr><th>Metric</th><th>Scope</th><th>Bound</th><th>Weight</th></tr></thead>
      <tbody>${goal.Targets.map((t) => {
    const scope = t.Level != null ? `L${t.Level}` : (t.Levels ? `L${t.Levels}` : 'total');
    const bound = t.Metric === 'pressure.rank' ? `rank≤${t.MaxRank != null ? t.MaxRank : 1}`
      : [t.Category, t.Min != null ? `min ${t.Min}` : null, t.Max != null ? `max ${t.Max}` : null].filter(Boolean).join(', ');
    return html`<tr><td>${t.Metric}</td><td>${scope}</td><td>${bound || '—'}</td><td>${t.Weight != null ? t.Weight : 1}</td></tr>`;
  })}</tbody>
    </table></div></div>`;
}

function IterationLog({ journal }) {
  return html`<div class="card span2"><h3>Iterations — ${journal.length} recorded (from journal.jsonl)</h3>
    <div class="tblwrap"><table class="delta">
      <thead><tr><th>#</th><th>Loss</th><th>Patch</th><th>Rationale</th></tr></thead>
      <tbody>${journal.map((r) => {
    const patch = (r.Patch && r.Patch.length) ? r.Patch.map((p) => `${p.Path}=${p.Value}`).join(', ') : '—';
    return html`<tr><td class="ro">${r.Iteration}</td><td>${round2(r.Loss)}</td>
      <td class="ro"><code>${patch}</code></td><td>${r.Rationale}</td></tr>`;
  })}</tbody>
    </table></div></div>`;
}

function Session() {
  const [sessions, setSessions] = useState([]);
  const [name, setName] = useState(null);
  const [data, setData] = useState(null);       // { name, goal, current, journal }
  const [sweep, setSweep] = useState(null);      // preview sweep of config.current
  const [live, setLive] = useState(true);
  const [error, setError] = useState(null);
  const simmedFor = useRef(-1);                  // journal length we last re-simmed for (avoid redundant sims)

  // Poll the session list + the active session on an interval; pick the newest if none chosen.
  useEffect(() => {
    let stop = false;
    const tick = async () => {
      try {
        const list = await getSessions();
        if (stop) return;
        setSessions(list);
        const active = name || list[0] || null;
        if (name !== active) setName(active);
        if (active) { const d = await getSession(active); if (!stop) { setData(d); setError(null); } }
        else setData(null);
      } catch (e) { if (!stop) setError(e.message); }
    };
    tick();
    if (!live) return () => { stop = true; };
    const id = setInterval(tick, 2000);
    return () => { stop = true; clearInterval(id); };
  }, [name, live]);

  // When a new iteration lands (journal grew), re-sim config.current for the heatmap + per-level times.
  useEffect(() => {
    if (!data || !data.current) return;
    const n = data.journal ? data.journal.length : 0;
    if (n === simmedFor.current) return;
    simmedFor.current = n;
    (async () => {
      try { const r = await runSweep(data.current, LIVE_SEEDS, 'typical'); setSweep(r.sweep); }
      catch (e) { setError(e.message); }
    })();
  }, [data]);

  const journal = (data && data.journal) || [];
  const deps = [journal.length];

  return html`
    <div class="rep-controls">
      ${Fld({ label: 'Session', children: html`<select value=${name || ''} onChange=${(e) => { setName(e.target.value); simmedFor.current = -1; }}>
        ${sessions.length ? sessions.map((s) => html`<option value=${s}>${s}</option>`) : html`<option value="">— none —</option>`}
      </select>` })}
      <label class="muted"><input type="checkbox" checked=${live} onChange=${(e) => setLive(e.target.checked)} /> live (poll 2s)</label>
      ${data ? html`<span class="muted">${journal.length} iteration(s)${journal.length ? ` · loss ${round2(journal[0].Loss)} → ${round2(journal[journal.length - 1].Loss)}` : ''}</span>` : null}
    </div>
    ${error ? html`<div class="errs"><b>Session error</b><div>${error}</div></div>` : null}
    ${!data ? html`<p class="muted">No active session. Run <code>balance session start --name &lt;slug&gt; --goal &lt;file&gt;</code> in the terminal, then iterate with <code>eval --session</code>.</p>` : html`
      <div class="chart-grid">
        <${GoalSummary} goal=${data.goal} />
        ${journal.length ? html`<${ChartBox} title="Loss curve — per iteration (from journal.jsonl)" make=${() => makeLossCurve(journal)} deps=${deps} />` : html`<div class="card"><p class="muted">No iterations yet — waiting for the agent's first <code>eval --session</code>.</p></div>`}
        ${sweep ? html`<${ChartBox} title=${`Time per level — config.current (${LIVE_SEEDS}-seed preview)`} make=${() => makeTimePerLevel(sweep, null)} deps=${[sweep]} />` : null}
        <${IterationLog} journal=${journal} />
        ${sweep ? html`<${Heatmap} A=${sweep} />` : null}
      </div>`}`;
}

// ================= app =================
function App() {
  const [versions, setVersions] = useState([]);
  const [name, setName] = useState(null);
  const [config, setConfig] = useState(null);
  const [dirty, setDirty] = useState(false);
  const [mode, setMode] = useState('edit'); // 'edit' | 'reports' | 'session'
  const [tab, setTab] = useState('Global');
  const [, setTick] = useState(0);
  const [modal, setModal] = useState(null); // {kind, ...}
  const force = () => setTick((t) => t + 1);
  touch = () => { setDirty(true); force(); };

  const refreshVersions = async () => setVersions(await getVersions());
  const load = async (n) => {
    const cfg = await getConfig(n);
    setConfig(cfg); setName(n); setDirty(false); force();
  };
  useEffect(() => { (async () => { await refreshVersions(); await load('baseline'); })().catch(err); }, []);

  const errors = validate(config);
  const invalid = errors.length > 0;

  const doSave = async () => {
    if (invalid) return;
    await putConfig(name, config); setDirty(false); force();
    setModal({ kind: 'toast', msg: `Saved '${name}'.` });
  };
  const doSaveAs = async () => {
    if (invalid) return;
    const n = (window.prompt('Save as new version name:', name + '-copy') || '').trim();
    if (!n) return;
    try {
      await saveAsVersion(n, config);
      await refreshVersions();
      setName(n); setDirty(false); force();
      setModal({ kind: 'toast', msg: `Saved new version '${n}'. Original untouched.` });
    } catch (e) { setModal({ kind: 'toast', msg: `Error: ${e.message}` }); }
  };
  const doDelete = async () => {
    if (!window.confirm(`Delete version '${name}'?`)) return;
    try {
      await deleteVersion(name);
      await refreshVersions();
      await load('baseline');
      setModal({ kind: 'toast', msg: `Deleted '${name}'.` });
    } catch (e) { setModal({ kind: 'toast', msg: `Error: ${e.message}` }); }
  };
  const doSim = async () => {
    setModal({ kind: 'busy', msg: 'Running sim…' });
    try { const r = await runSim(config); setModal({ kind: 'sim', table: r.table, result: r.result }); }
    catch (e) { setModal({ kind: 'toast', msg: `Sim error: ${e.message}` }); }
  };
  const doPush = async () => {
    if (invalid) return;
    setModal({ kind: 'busy', msg: 'Planning write…' });
    try {
      const p = await planWrite(config, false);
      if (p.refused) setModal({ kind: 'refused', msg: p.refused });
      else if (!p.changes.length && !p.insertions.length) setModal({ kind: 'toast', msg: 'No changes to push — assets already match.' });
      else setModal({ kind: 'push', plan: p });
    } catch (e) { setModal({ kind: 'toast', msg: `Write error: ${e.message}` }); }
  };
  const confirmPush = async () => {
    setModal({ kind: 'busy', msg: 'Writing to Unity…' });
    try {
      const p = await planWrite(config, true);
      setModal({ kind: 'toast', msg: `Applied ${p.changes.length} change(s) + ${p.insertions.length} insertion(s) to Assets/.` });
    } catch (e) { setModal({ kind: 'toast', msg: `Write error: ${e.message}` }); }
  };

  if (!config) return html`<main><p class="muted">Loading baseline…</p></main>`;
  const View = TAB_VIEWS[tab];
  const reports = mode === 'reports';
  const session = mode === 'session';
  const edit = mode === 'edit';

  return html`
    <header>
      <h1>VoidDay Balance</h1>
      <div class="toolbar">
        <button class=${edit ? 'primary' : ''} onClick=${() => setMode('edit')}>Edit</button>
        <button class=${reports ? 'primary' : ''} onClick=${() => setMode('reports')}>Reports</button>
        <button class=${session ? 'primary' : ''} onClick=${() => setMode('session')}>Session</button>
      </div>
      ${edit ? html`<div class="toolbar">
        <label class="muted">version</label>
        <select value=${name} onChange=${(e) => load(e.target.value)}>
          ${versions.map((v) => html`<option value=${v}>${v}</option>`)}
        </select>
        ${dirty ? html`<span class="dirty">● unsaved</span>` : null}
      </div>` : null}
      <div class="grow"></div>
      ${edit ? html`<div class="toolbar">
        <button onClick=${doSave} disabled=${invalid || !dirty}>Save</button>
        <button onClick=${doSaveAs} disabled=${invalid}>Save as…</button>
        <button class="danger" onClick=${doDelete} disabled=${name === 'baseline'}>Delete</button>
        <button onClick=${doSim}>Run sim</button>
        <button class="primary" onClick=${doPush} disabled=${invalid}>Push to Unity…</button>
      </div>` : null}
    </header>
    ${edit ? html`<nav>${TABS.map((t) => html`<button class=${t === tab ? 'active' : ''} onClick=${() => setTab(t)}>${t}</button>`)}</nav>` : null}
    <main class=${!edit ? 'reports' : ''}>
      ${reports ? html`<${Reports} versions=${versions} />`
    : session ? html`<${Session} />`
    : html`
      ${invalid ? html`<div class="errs"><b>${errors.length} validation error(s)</b> — fix before saving or pushing.
        <ul>${errors.map((x) => html`<li>${x}</li>`)}</ul></div>` : null}
      <${View} c=${config} force=${force} />`}
    </main>
    ${modal ? html`<${ModalHost} modal=${modal} close=${() => setModal(null)} confirmPush=${confirmPush} />` : null}`;
}

function ModalHost({ modal, close, confirmPush }) {
  if (modal.kind === 'toast') {
    return html`<${Modal} title="" onClose=${close} actions=${html`<button class="primary" onClick=${close}>OK</button>`}>
      <p>${modal.msg}</p></${Modal}>`;
  }
  if (modal.kind === 'busy') {
    return html`<div class="modal-bg"><div class="modal"><p>${modal.msg}</p></div></div>`;
  }
  if (modal.kind === 'refused') {
    return html`<${Modal} title="Push refused by the writer" onClose=${close}
      actions=${html`<button class="primary" onClick=${close}>OK</button>`}>
      <p class="muted">The M02 writer supports scalar edits and recipe insertion only. This config asks for
        something it cannot do surgically, so nothing was written:</p>
      <pre>${modal.msg}</pre>
      <p class="hint">The edit is safe in your saved version — it just cannot round-trip into the Unity assets.</p>
    </${Modal}>`;
  }
  if (modal.kind === 'push') {
    const p = modal.plan;
    return html`<${Modal} title="Push to Unity — change summary" onClose=${close}
      actions=${html`<button onClick=${close}>Cancel</button>
        <button class="primary" onClick=${confirmPush}>Confirm & write</button>`}>
      <p class="muted">${p.changes.length} scalar change(s), ${p.insertions.length} insertion(s). This edits
        files under <code>Assets/</code>.</p>
      ${p.changes.map((c) => html`<div class="change">${c.asset} <b>${c.field}</b>:
        <span class="old">${c.old}</span> → <span class="new">${c.new}</span></div>`)}
      ${p.insertions.map((i) => html`<div class="change">+ new recipe <b>${i.recipe}</b> (station ${i.station})</div>`)}
    </${Modal}>`;
  }
  if (modal.kind === 'sim') {
    return html`<${Modal} title="Simulation (seed 1) — raw table" onClose=${close}
      actions=${html`<button class="primary" onClick=${close}>Close</button>`}>
      <p class="muted small">Reached level ${modal.result.LevelReached} · ${(modal.result.TotalSeconds / 60).toFixed(1)} min ·
        stop: ${modal.result.Stop}. Charts arrive in M06 — this is the raw runner output.</p>
      <pre>${modal.table}</pre></${Modal}>`;
  }
  return null;
}

function err(e) { console.error(e); document.getElementById('app').innerHTML = `<main><pre style="color:#e46060">${e && e.stack || e}</pre></main>`; }

render(html`<${App} />`, document.getElementById('app'));
