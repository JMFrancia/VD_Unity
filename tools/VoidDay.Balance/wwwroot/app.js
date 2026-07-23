// VoidDay Balance Workbench — the browser client (M04).
//
// It is a CLIENT of the same reader/writer/runner the CLI uses; it holds NO economy logic. Every
// field of BalanceConfig is editable here and round-trips through the version save/load endpoints
// (plain JSON). Push-to-Unity funnels through the M02 writer via /api/write, which supports scalar
// edits + recipe insertion and refuses the rest — so the push modal shows either a change summary or
// the writer's refusal verbatim, never silently dropping an edit.
//
// No build step: htm + preact are vendored as one ESM file (spec M04). Charts are M06 — this screen
// edits and can trigger a raw sim table, but draws nothing.
import {
  html, render, useState, useEffect,
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
const planWrite = (config, apply) => api('POST', '/api/write', { config, apply });

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

// ================= app =================
function App() {
  const [versions, setVersions] = useState([]);
  const [name, setName] = useState(null);
  const [config, setConfig] = useState(null);
  const [dirty, setDirty] = useState(false);
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

  return html`
    <header>
      <h1>VoidDay Balance</h1>
      <div class="toolbar">
        <label class="muted">version</label>
        <select value=${name} onChange=${(e) => load(e.target.value)}>
          ${versions.map((v) => html`<option value=${v}>${v}</option>`)}
        </select>
        ${dirty ? html`<span class="dirty">● unsaved</span>` : null}
      </div>
      <div class="grow"></div>
      <div class="toolbar">
        <button onClick=${doSave} disabled=${invalid || !dirty}>Save</button>
        <button onClick=${doSaveAs} disabled=${invalid}>Save as…</button>
        <button class="danger" onClick=${doDelete} disabled=${name === 'baseline'}>Delete</button>
        <button onClick=${doSim}>Run sim</button>
        <button class="primary" onClick=${doPush} disabled=${invalid}>Push to Unity…</button>
      </div>
    </header>
    <nav>${TABS.map((t) => html`<button class=${t === tab ? 'active' : ''} onClick=${() => setTab(t)}>${t}</button>`)}</nav>
    <main>
      ${invalid ? html`<div class="errs"><b>${errors.length} validation error(s)</b> — fix before saving or pushing.
        <ul>${errors.map((x) => html`<li>${x}</li>`)}</ul></div>` : null}
      <${View} c=${config} force=${force} />
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
