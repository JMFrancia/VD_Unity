using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// The in-world state of each station (§12.6, sheet 34:2). Above the body: a progress bar while a job runs
    /// and a hopping void-accent ready icon while output waits. In front of the body (panel.station ALT 42:2):
    /// the **job queue as slots** — filled / running / empty — the queue having moved out of the panel into
    /// the world (see docs/UI-Inventory.md world.queueSlots). The slot row shows only while the queue is
    /// non-empty, so idle stations stay clean. Pure view-sync: polls Core state every frame, holds no rule
    /// (CLAUDE.md). Tapping a filled slot is a cancel — routed by InputRouter off QueueSlotTag.
    public sealed class WorldState : MonoBehaviour
    {
        const float BarWidth = 0.9f;
        const float BarHeight = 0.14f;
        const float BarAnchorY = 1.15f;   // above the station body
        const float HopAmplitude = 0.12f;
        const float HopSpeed = 6f;

        const float SlotSize = 0.42f;
        const float SlotGap = 0.10f;
        const float SlotLift = 0.30f;     // above the ground so the ground plane never occludes the slots
        const float SlotForward = 1.35f;  // toward the camera (−Z) → reads as "below the building" on screen

        sealed class Slot
        {
            public GameObject Root;
            public Renderer Bg;
            public GameObject Chip;
            public Collider Collider;
            public Transform Fill;   // running-head mini progress (slot 0 only)
        }

        sealed class Visual
        {
            public string StationId;
            public GameObject BarRoot;
            public Transform Fill;
            public GameObject ReadyRoot;
            public float ReadyBaseY;
            public GameObject QueueRow;
            public List<Slot> Slots = new();
        }

        JobSystem _jobs;
        RecipeCatalog _catalog;
        UiThemeSO _t;
        readonly List<Visual> _visuals = new();

        public void Init(JobSystem jobs, IReadOnlyDictionary<string, Transform> stationRoots, Camera camera,
            UiThemeSO theme, RecipeCatalog catalog)
        {
            _jobs = jobs;
            _catalog = catalog;
            _t = theme;
            foreach (var kv in stationRoots)
                _visuals.Add(BuildVisual(kv.Key, kv.Value));
        }

        Visual BuildVisual(string stationId, Transform stationRoot)
        {
            // Above-body indicators ride a billboarded anchor (progress bar + ready icon).
            var above = new GameObject($"WorldState_{stationId}");
            above.transform.SetParent(stationRoot, false);
            above.transform.localPosition = new Vector3(0f, BarAnchorY, 0f);
            above.AddComponent<Billboard>(); // face the ¾ camera (§12.6)

            var barRoot = new GameObject("Bar");
            barRoot.transform.SetParent(above.transform, false);
            SolidCube(barRoot.transform, _t.progressTrack, new Vector3(BarWidth, BarHeight, 0.05f), collides: false)
                .transform.localPosition = Vector3.zero;
            var fill = SolidCube(barRoot.transform, _t.progressFill, new Vector3(BarWidth, BarHeight, 0.06f), collides: false);

            var readyRoot = new GameObject("Ready");
            readyRoot.transform.SetParent(above.transform, false);
            SolidCube(readyRoot.transform, _t.readyAccent, new Vector3(0.28f, 0.28f, 0.28f), collides: false)
                .transform.localPosition = Vector3.zero;

            var visual = new Visual
            {
                StationId = stationId,
                BarRoot = barRoot,
                Fill = fill.transform,
                ReadyRoot = readyRoot,
                ReadyBaseY = 0.1f
            };
            barRoot.SetActive(false);
            readyRoot.SetActive(false);

            // Queue slots in front of the body — only producers have a queue.
            string type = _jobs.StationTypeOf(stationId);
            if (_catalog.ForStationType(type).Count > 0)
                BuildSlots(visual, stationRoot, stationId);

            return visual;
        }

        void BuildSlots(Visual visual, Transform stationRoot, string stationId)
        {
            // Non-billboarded row anchored in world space in front of the building (on the ground).
            var row = new GameObject("QueueRow");
            row.transform.SetParent(stationRoot, false);
            row.transform.localPosition = new Vector3(0f, SlotLift, -SlotForward);
            visual.QueueRow = row;

            int depth = _jobs.QueueDepth(stationId);
            float span = depth * SlotSize + (depth - 1) * SlotGap;
            float startX = -span * 0.5f + SlotSize * 0.5f;

            for (int i = 0; i < depth; i++)
            {
                var slotRoot = new GameObject($"Slot{i}");
                slotRoot.transform.SetParent(row.transform, false);
                slotRoot.transform.localPosition = new Vector3(startX + i * (SlotSize + SlotGap), 0f, 0f);
                slotRoot.AddComponent<Billboard>(); // each slot faces the camera

                var bg = SolidCube(slotRoot.transform, _t.progressTrack, new Vector3(SlotSize, SlotSize, 0.05f), collides: true);
                bg.transform.localPosition = Vector3.zero;
                var tag = bg.AddComponent<QueueSlotTag>();
                tag.StationId = stationId;
                tag.SlotIndex = i;

                var chip = SolidCube(slotRoot.transform, _t.resourceChip, new Vector3(SlotSize * 0.5f, SlotSize * 0.5f, 0.07f), collides: false);
                chip.transform.localPosition = new Vector3(0f, 0.03f, -0.02f);

                Transform miniFill = null;
                if (i == 0)
                {
                    var track = SolidCube(slotRoot.transform, _t.progressTrack, new Vector3(SlotSize * 0.78f, 0.06f, 0.07f), collides: false);
                    track.transform.localPosition = new Vector3(0f, -SlotSize * 0.33f, -0.02f);
                    var f = SolidCube(slotRoot.transform, _t.progressFill, new Vector3(SlotSize * 0.78f, 0.06f, 0.08f), collides: false);
                    f.transform.localPosition = track.transform.localPosition;
                    miniFill = f.transform;
                }

                visual.Slots.Add(new Slot
                {
                    Root = slotRoot,
                    Bg = bg.GetComponent<Renderer>(),
                    Chip = chip,
                    Collider = bg.GetComponent<Collider>(),
                    Fill = miniFill
                });
            }
            row.SetActive(false);
        }

        void Update()
        {
            if (_jobs == null) return;
            double now = Time.timeAsDouble;
            foreach (var v in _visuals)
            {
                bool has = _jobs.TryGetHeadProgress(v.StationId, now, out float fraction, out bool complete);
                bool running = has && !complete;
                bool ready = has && complete;

                if (v.BarRoot.activeSelf != running) v.BarRoot.SetActive(running);
                if (v.ReadyRoot.activeSelf != ready) v.ReadyRoot.SetActive(ready);

                if (running)
                {
                    v.Fill.localScale = new Vector3(BarWidth * fraction, BarHeight, 0.06f);
                    v.Fill.localPosition = new Vector3(-BarWidth * 0.5f * (1f - fraction), 0f, 0f);
                }
                else if (ready)
                {
                    float hop = Mathf.Abs(Mathf.Sin(Time.time * HopSpeed)) * HopAmplitude;
                    v.ReadyRoot.transform.localPosition = new Vector3(0f, v.ReadyBaseY + hop, 0f);
                }

                UpdateSlots(v, now);
            }
        }

        void UpdateSlots(Visual v, double now)
        {
            if (v.QueueRow == null) return;
            var queue = _jobs.GetQueue(v.StationId);
            bool anyJobs = queue.Count > 0;
            if (v.QueueRow.activeSelf != anyJobs) v.QueueRow.SetActive(anyJobs);
            if (!anyJobs) return;

            for (int i = 0; i < v.Slots.Count; i++)
            {
                var slot = v.Slots[i];
                bool filled = i < queue.Count;

                slot.Bg.material.SetColor("_BaseColor", filled ? _t.slotFill : _t.progressTrack);
                if (slot.Chip.activeSelf != filled) slot.Chip.SetActive(filled);
                if (slot.Collider.enabled != filled) slot.Collider.enabled = filled;

                if (slot.Fill != null) // slot 0 mini progress for the running head
                {
                    bool showFill = filled && queue[i].State == JobState.Running;
                    if (slot.Fill.gameObject.activeSelf != showFill) slot.Fill.gameObject.SetActive(showFill);
                    if (showFill && _jobs.TryGetHeadProgress(v.StationId, now, out float f, out bool complete))
                    {
                        float frac = complete ? 1f : f;
                        float w = SlotSize * 0.78f;
                        slot.Fill.localScale = new Vector3(w * frac, 0.06f, 0.08f);
                        slot.Fill.localPosition = new Vector3(-w * 0.5f * (1f - frac), -SlotSize * 0.33f, -0.02f);
                    }
                }
            }
        }

        static GameObject SolidCube(Transform parent, Color color, Vector3 scale, bool collides)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!collides) Destroy(cube.GetComponent<Collider>()); // world UI must not intercept station raycasts
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = scale;
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", color);
            cube.GetComponent<Renderer>().material = m;
            return cube;
        }
    }
}
