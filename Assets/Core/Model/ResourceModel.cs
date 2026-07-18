namespace VoidDay.Core.Model
{
    /// Rule-relevant projection of a ResourceSO (§14). No Unity handles, no icon/mesh refs —
    /// those stay on the SO. M1 reads only id + display name; economic fields land when a rule needs them.
    public sealed class ResourceModel
    {
        public readonly string Id;
        public readonly string DisplayName;

        public ResourceModel(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }
    }
}
