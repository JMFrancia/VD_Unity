using UnityEngine;
using UnityEngine.UI;
using VoidDay.Data;

namespace VoidDay.View
{
    /// One station-upgrade row in the panel's Upgrades tab — the `pattern.purchaseRow` for upgrades: a
    /// procedural effect description (§3.6), the current→next tier, a money cost, and a single Buy control.
    /// States (state colors from the theme SO): available (afford) → Buy enabled; can't-afford → disabled +
    /// warning-colored cost; maxed → "Maxed", no button. The look is authored in the prefab; StationPanel
    /// instantiates one per track and binds it.
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

        public void Bind(string description, int currentTier, int maxTier, int cost, bool affordable)
        {
            descriptionText.text = description;
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
