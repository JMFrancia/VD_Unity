using UnityEngine;
using UnityEngine.UI;
using VoidDay.Data;

namespace VoidDay.View
{
    /// One station-upgrade row in the panel's Upgrades tab — the `pattern.purchaseRow` for upgrades: a
    /// procedural effect description (§3.6), the current→next tier, a money cost, and a single Buy control.
    /// States (state colors from the theme SO): available (afford) → Buy enabled; can't-afford → disabled +
    /// warning-colored cost; level-locked → the level it wants, no button; maxed → "Maxed", no button. The
    /// look is authored in the prefab; StationPanel instantiates one per track and binds it.
    public sealed class UpgradeRow : MonoBehaviour
    {
        [SerializeField] UiThemeSO theme;
        [SerializeField] Text descriptionText;
        [SerializeField] Text tierText;
        [SerializeField] Text costText;
        [SerializeField] Button buyButton;
        [SerializeField] Image buyButtonImage;
        [SerializeField] Text buyLabel;

        public Button Button => buyButton;

        [Tooltip("Tier line while the track is still level-gated. {0} is the level that opens it.")]
        [SerializeField] string lockedTierFormat = "Unlocks at level {0}";

        /// A locked track is shown, not hidden — the gate is something to play toward. Lock beats maxed and
        /// affordability: a track you cannot reach yet has nothing to say about price.
        public void BindLocked(string description, int unlockLevel)
        {
            descriptionText.text = description;
            descriptionText.color = theme.lockedText;
            tierText.text = string.Format(lockedTierFormat, unlockLevel);
            tierText.color = theme.lockedText;
            costText.text = "";
            buyButton.gameObject.SetActive(false);
        }

        public void Bind(string description, int currentTier, int maxTier, int cost, bool affordable)
        {
            descriptionText.text = description;
            descriptionText.color = theme.ink;
            bool maxed = currentTier >= maxTier;

            if (maxed)
            {
                tierText.text = $"Lv {currentTier} · Maxed";
                tierText.color = theme.lockedText;
                costText.text = "";
                buyButton.gameObject.SetActive(false);
                return;
            }

            tierText.text = $"Lv {currentTier} → {currentTier + 1}";
            tierText.color = theme.ink;
            costText.text = $"${cost}";
            costText.color = affordable ? theme.ink : theme.warning;

            buyButton.gameObject.SetActive(true);
            buyButton.interactable = affordable;
            buyButtonImage.color = affordable ? theme.accent : theme.lockedBg;
            buyLabel.color = affordable ? theme.accentText : theme.lockedText;
            buyLabel.text = "Buy";
        }
    }
}
