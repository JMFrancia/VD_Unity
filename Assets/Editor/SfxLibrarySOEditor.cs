using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using VoidDay.Data;

namespace VoidDay.EditorTools
{
    /// Draws the SFX library like the default inspector, but under any cue whose clip is ALSO assigned to
    /// another cue, appends a note listing the other cues. Double-assignment can be intentional, so this is
    /// a plain note, not a warning — and it lives only here, in the inspector; the runtime never sees it.
    [CustomEditor(typeof(SfxLibrarySO))]
    public sealed class SfxLibrarySOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // First pass: map each assigned clip to every cue field that uses it, so a shared clip can name
            // its peers. Only the top-level Entry fields are Generic; m_Script and anything else is skipped.
            var cuesByClip = new Dictionary<Object, List<string>>();
            var scan = serializedObject.GetIterator();
            for (bool enter = true; scan.NextVisible(enter); enter = false)
            {
                if (scan.propertyType != SerializedPropertyType.Generic) continue;
                var clip = scan.FindPropertyRelative("clip")?.objectReferenceValue;
                if (clip == null) continue;
                if (!cuesByClip.TryGetValue(clip, out var users))
                    cuesByClip[clip] = users = new List<string>();
                users.Add(ObjectNames.NicifyVariableName(scan.name));
            }

            // Second pass: draw each field, dropping the note in right below any cue that shares its clip.
            var draw = serializedObject.GetIterator();
            for (bool enter = true; draw.NextVisible(enter); enter = false)
            {
                if (draw.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(draw);
                    continue;
                }

                EditorGUILayout.PropertyField(draw, true);

                if (draw.propertyType != SerializedPropertyType.Generic) continue;
                var clip = draw.FindPropertyRelative("clip")?.objectReferenceValue;
                if (clip == null || cuesByClip[clip].Count < 2) continue;

                string self = ObjectNames.NicifyVariableName(draw.name);
                var note = new StringBuilder("Same clip used by:");
                foreach (var name in cuesByClip[clip])
                    if (name != self) note.Append("\n• ").Append(name);
                EditorGUILayout.HelpBox(note.ToString(), MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
