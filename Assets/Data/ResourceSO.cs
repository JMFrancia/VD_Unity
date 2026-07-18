using UnityEngine;

namespace VoidDay.Data
{
    /// One asset per resource (§14). M1 reads id + displayName; baseValue/sellable are authored now
    /// and read by order pricing/generation from M3. Icon/mesh ref lands when a milestone renders resources.
    [CreateAssetMenu(menuName = "VoidDay/Resource", fileName = "Resource")]
    public sealed class ResourceSO : ScriptableObject
    {
        public string id;
        public string displayName;

        [Tooltip("Base cash/XP value. Read by order pricing (M3+).")]
        public int baseValue = 1;

        [Tooltip("False = never sold as-is (wheat). Read by order generation (M3+, §6.1).")]
        public bool sellable = true;
    }
}
