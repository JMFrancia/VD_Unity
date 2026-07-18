using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Rules;

namespace VoidDay.View
{
    /// Renders each station's job state above its body (§12.6): a progress bar while a job runs, a hopping
    /// ready icon while output waits to be collected. Pure view-sync — it polls Core state every frame and
    /// holds no rule (CLAUDE.md). Placeholder visuals (tinted primitives); real VFX swaps in later.
    public sealed class WorldState : MonoBehaviour
    {
        const float BarWidth = 0.9f;
        const float BarHeight = 0.14f;
        const float AnchorY = 1.15f;   // above the station body
        const float HopAmplitude = 0.12f;
        const float HopSpeed = 6f;

        sealed class Visual
        {
            public string StationId;
            public GameObject BarRoot;
            public Transform Fill;
            public GameObject ReadyRoot;
            public float ReadyBaseY;
        }

        JobSystem _jobs;
        Camera _camera;
        readonly List<Visual> _visuals = new();

        public void Init(JobSystem jobs, IReadOnlyDictionary<string, Transform> stationRoots, Camera camera)
        {
            _jobs = jobs;
            _camera = camera;
            foreach (var kv in stationRoots)
                _visuals.Add(BuildVisual(kv.Key, kv.Value));
        }

        Visual BuildVisual(string stationId, Transform stationRoot)
        {
            var anchor = new GameObject($"WorldState_{stationId}");
            anchor.transform.SetParent(stationRoot, false);
            anchor.transform.localPosition = new Vector3(0f, AnchorY, 0f);
            anchor.AddComponent<Billboard>(); // face the ¾ camera (§12.6); uses Camera.main

            // Progress bar: a dark track with a green fill that scales from the left edge.
            var barRoot = new GameObject("Bar");
            barRoot.transform.SetParent(anchor.transform, false);
            var track = SolidCube(barRoot.transform, new Color(0.10f, 0.09f, 0.14f), new Vector3(BarWidth, BarHeight, 0.05f));
            track.transform.localPosition = Vector3.zero;
            var fill = SolidCube(barRoot.transform, new Color(0.36f, 0.85f, 0.45f), new Vector3(BarWidth, BarHeight, 0.06f));

            // Ready icon: a bright accent cube that hops.
            var readyRoot = new GameObject("Ready");
            readyRoot.transform.SetParent(anchor.transform, false);
            var icon = SolidCube(readyRoot.transform, new Color(0.98f, 0.82f, 0.25f), new Vector3(0.28f, 0.28f, 0.28f));
            icon.transform.localPosition = Vector3.zero;

            barRoot.SetActive(false);
            readyRoot.SetActive(false);

            return new Visual
            {
                StationId = stationId,
                BarRoot = barRoot,
                Fill = fill.transform,
                ReadyRoot = readyRoot,
                ReadyBaseY = 0.1f
            };
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
                    // Scale the fill down from the full width, keeping its left edge pinned.
                    v.Fill.localScale = new Vector3(BarWidth * fraction, BarHeight, 0.06f);
                    v.Fill.localPosition = new Vector3(-BarWidth * 0.5f * (1f - fraction), 0f, 0f);
                }
                else if (ready)
                {
                    float hop = Mathf.Abs(Mathf.Sin(Time.time * HopSpeed)) * HopAmplitude;
                    v.ReadyRoot.transform.localPosition = new Vector3(0f, v.ReadyBaseY + hop, 0f);
                }
            }
        }

        static GameObject SolidCube(Transform parent, Color color, Vector3 scale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cube.GetComponent<Collider>()); // world UI must not intercept station raycasts
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = scale;
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", color);
            cube.GetComponent<Renderer>().material = m;
            return cube;
        }
    }
}
