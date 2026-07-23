using UnityEngine;
using UnityEngine.UI;
using VoidDay.Data;

namespace VoidDay.View
{
    /// One recipe tile in the station popup's tile row (ALT 42:2): chip, name, timer, selection outline.
    /// The look is authored in the prefab; StationPanel instantiates one per recipe and binds it.
    ///
    /// A level-locked recipe (RecipeSO.unlockLevel) is shown, not hidden — a greyed-but-identifiable teaser so
    /// the gate is something to play toward (mockup 42:2 / feature frame). It stays selectable so tapping it
    /// previews what it makes; the panel's Queue action is what actually refuses it.
    public sealed class RecipeTile : MonoBehaviour
    {
        [SerializeField] UiThemeSO theme;
        [SerializeField] Button button;
        [SerializeField] Image background;   // the tile card; tinted to the locked bg when level-gated
        [SerializeField] GameObject selectionOutline;
        [SerializeField] Image icon;
        [SerializeField] Text nameText;
        [SerializeField] Text timerText;

        [Tooltip("Timer-line copy while the recipe is level-gated. {0} is the level that opens it.")]
        [SerializeField] string lockedTimerFormat = "Lv {0}";

        public Button Button => button;

        // A fresh tile is instantiated per rebuild, so the unlocked bind keeps the prefab's authored colours
        // (touching none) and only BindLocked overrides them to the muted lock treatment.
        public void Bind(string label, Sprite iconSprite, string timer, bool selected)
        {
            icon.sprite = iconSprite;
            nameText.text = label;
            timerText.text = timer;
            selectionOutline.SetActive(selected);
        }

        /// Greyed teaser: name still shown so the player knows what unlocks, icon + text muted, timer replaced
        /// by the level it opens at. Fresh instance per rebuild, so overriding the authored colours is enough.
        public void BindLocked(string label, Sprite iconSprite, int unlockLevel, bool selected)
        {
            background.color = theme.lockedBg;  // grey the whole card, not just its contents
            icon.sprite = iconSprite;
            icon.color = theme.lockedText;
            nameText.text = label;
            nameText.color = theme.lockedText;
            timerText.text = string.Format(lockedTimerFormat, unlockLevel);
            timerText.color = theme.lockedText;
            selectionOutline.SetActive(selected);
        }
    }
}
