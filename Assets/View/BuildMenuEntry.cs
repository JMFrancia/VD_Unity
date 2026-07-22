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
    ///
    /// The entry sits inside the tray's horizontal ScrollRect and is the drag handler the event system finds
    /// first, so it owns the axis-lock: sideways scrolls the strip, away-from-the-tray starts a placement.
    /// Sideways gestures are forwarded to the ScrollRect by hand, since it never sees them otherwise.
    public sealed class BuildMenuEntry : MonoBehaviour,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public enum State { Available, Locked, CapReached, CantAfford }

        [SerializeField] Image thumbnail;
        [SerializeField] Text nameText;
        [SerializeField] GameObject costRow;      // coin + cost
        [SerializeField] Text costText;
        [Tooltip("Build-cost display format, {0} = the money amount. Matches LevelUpPopup's moneyFormat.")]
        [SerializeField] string costFormat = "${0}";
        [SerializeField] GameObject capBadge;
        [SerializeField] Text capBadgeText;
        [SerializeField] GameObject lockOverlay;  // grayscale veil + padlock + level
        [SerializeField] Text lockLevelText;

        PlacementController _placement;
        string _stationType;
        bool _draggable;
        ScrollRect _strip;    // the tray's horizontal strip — this entry's scrolling ancestor
        bool _scrolling;      // the gesture in progress is a strip scroll, not a placement

        public void Bind(PlacementController placement, string stationType, string displayName, Sprite thumb)
        {
            _placement = placement;
            _stationType = stationType;
            _strip = GetComponentInParent<ScrollRect>();
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
                costText.text = string.Format(costFormat, cost);
                costText.color = state == State.CantAfford ? theme.warning : theme.ink;
            }

            capBadge.SetActive(capped);
            if (capped) capBadgeText.text = $"{count}/{cap}";

            lockOverlay.SetActive(locked);
            if (locked) lockLevelText.text = $"Lv {unlockLevel}";
        }

        // Touching a coasting strip should stop it. The event system delivers this to whichever object it
        // resolved as the drag handler — this entry — so the ScrollRect only hears about it if we pass it on.
        public void OnInitializePotentialDrag(PointerEventData e) => _strip.OnInitializePotentialDrag(e);

        public void OnBeginDrag(PointerEventData e)
        {
            // Axis-lock. Begin-drag only fires once the pointer has cleared the drag threshold, so
            // press → current is a long enough vector to read a direction from.
            var gesture = e.position - e.pressPosition;
            _scrolling = Mathf.Abs(gesture.x) > Mathf.Abs(gesture.y);

            if (_scrolling) _strip.OnBeginDrag(e);
            else if (_draggable) _placement.BeginPlacement(_stationType);
        }

        // A placement ghost follows the pointer itself (it polls the device), so only the scroll case needs
        // the per-frame event — but both cases need Unity to route the gesture here rather than call it a click.
        public void OnDrag(PointerEventData e)
        {
            if (_scrolling) _strip.OnDrag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_scrolling) _strip.OnEndDrag(e);
            _scrolling = false;
        }
    }
}
