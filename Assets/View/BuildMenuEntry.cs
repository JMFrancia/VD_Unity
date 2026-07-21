using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VoidDay.Data;

namespace VoidDay.View
{
    /// One station entry in the build tray (menu.build, mockup 20:2). Shows the station's thumbnail, name, and a
    /// state marker: money cost (ink) when available, cost in warning color when unaffordable, an owned/cap
    /// badge when capped, or a grayscale + lock + "Lv N" when level-locked. Only an AVAILABLE entry is
    /// draggable — dragging it off the tray begins a placement (§12.2). Look is authored in the prefab;
    /// BuildMenu instantiates one per station type and drives its state.
    public sealed class BuildMenuEntry : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public enum State { Available, Locked, CapReached, CantAfford }

        [SerializeField] Image thumbnail;
        [SerializeField] Text nameText;
        [SerializeField] GameObject costRow;      // coin + cost
        [SerializeField] Text costText;
        [SerializeField] GameObject capBadge;
        [SerializeField] Text capBadgeText;
        [SerializeField] GameObject lockOverlay;  // grayscale veil + padlock + level
        [SerializeField] Text lockLevelText;

        PlacementController _placement;
        string _stationType;
        bool _draggable;

        public void Bind(PlacementController placement, string stationType, string displayName, Sprite thumb)
        {
            _placement = placement;
            _stationType = stationType;
            nameText.text = displayName;
            thumbnail.sprite = thumb;
        }

        public void Apply(State state, int cost, int count, int cap, int unlockLevel, UiThemeSO theme)
        {
            _draggable = state == State.Available;

            bool locked = state == State.Locked;
            bool capped = state == State.CapReached;
            bool showCost = state == State.Available || state == State.CantAfford;

            // Locked types dim under the disabled gray; every other state shows the thumbnail untinted.
            thumbnail.color = locked ? theme.lockedText : Color.white;

            costRow.SetActive(showCost);
            if (showCost)
            {
                costText.text = cost.ToString();
                costText.color = state == State.CantAfford ? theme.warning : theme.ink;
            }

            capBadge.SetActive(capped);
            if (capped) capBadgeText.text = $"{count}/{cap}";

            lockOverlay.SetActive(locked);
            if (locked) lockLevelText.text = $"Lv {unlockLevel}";
        }

        public void OnBeginDrag(PointerEventData _)
        {
            if (_draggable) _placement.BeginPlacement(_stationType);
        }

        // The placement ghost follows the pointer itself (it polls the device); these exist only so Unity
        // routes the drag to this entry rather than treating the gesture as a click.
        public void OnDrag(PointerEventData _) { }
        public void OnEndDrag(PointerEventData _) { }
    }
}
