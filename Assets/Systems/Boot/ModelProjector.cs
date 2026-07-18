using VoidDay.Core.Model;
using VoidDay.Data;

namespace VoidDay.Systems
{
    /// Projects Data-layer SOs into pure Core/Model objects at boot (§14). A Core rule reads a *Model,
    /// never a *SO. Asset refs (mesh/material) are dropped here — they stay on the SO for the View layer.
    public static class ModelProjector
    {
        public static GameConfigModel Project(GameConfigSO so) =>
            new GameConfigModel(so.gridCols, so.gridRows, so.cellSize);

        public static ResourceModel Project(ResourceSO so) =>
            new ResourceModel(so.id, so.displayName);

        public static StationModel Project(StationSO so, string instanceId) =>
            new StationModel(instanceId, so.stationType, so.displayName, so.width, so.height);
    }
}
