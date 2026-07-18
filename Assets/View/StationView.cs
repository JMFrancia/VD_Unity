using UnityEngine;
using VoidDay.Data;

namespace VoidDay.View
{
    /// Renders a station's static body (§12.6). Uses the real prefab when the SO has one; otherwise a
    /// tinted primitive placeholder. State visuals (progress bar, ready hop, storage-full) are NOT built
    /// here — each rides with the milestone that introduces the state (M2/M7).
    public sealed class StationView : MonoBehaviour
    {
        public void Build(StationSO so)
        {
            GameObject body;
            if (so.prefab != null)
            {
                body = Instantiate(so.prefab, transform);
            }
            else
            {
                body = GameObject.CreatePrimitive(so.placeholderPrimitive);
                body.transform.SetParent(transform, false);
                body.transform.localScale = so.placeholderScale;
                body.transform.localEulerAngles = so.placeholderEuler;
                body.transform.localPosition = new Vector3(0f, so.placeholderYOffset, 0f);
                body.GetComponent<Renderer>().material = LitMaterial(so.placeholderColor);
            }
            body.name = $"{so.stationType}_body";
        }

        static Material LitMaterial(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            return m;
        }
    }
}
