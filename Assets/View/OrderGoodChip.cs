using UnityEngine;
using UnityEngine.UI;
using VoidDay.Data;

namespace VoidDay.View
{
    /// One requested good on an order card (mockup 15:6–15:10): the goods tile, an "×N" quantity badge, and
    /// the have/need signal underneath — a green check when you hold enough, a "need N" pill in the warning
    /// color when you don't (the HayDay red count).
    public sealed class OrderGoodChip : MonoBehaviour
    {
        [SerializeField] UiThemeSO theme;
        [SerializeField] Image icon;
        [SerializeField] Text quantityText;
        [SerializeField] GameObject haveBadge;
        [SerializeField] GameObject needPill;
        [SerializeField] Image needPillImage;
        [SerializeField] Text needText;
        [SerializeField] Text goodNameText;

        public void Bind(string displayName, Sprite iconSprite, int requested, int held)
        {
            icon.sprite = iconSprite;
            goodNameText.text = displayName;
            quantityText.text = $"×{requested}";

            bool enough = held >= requested;
            haveBadge.SetActive(enough);
            needPill.SetActive(!enough);
            if (enough) return;

            needText.text = $"need {requested - held}";
            needText.color = theme.warning;
            needPillImage.color = new Color(theme.warning.r, theme.warning.g, theme.warning.b, 0.18f);
        }
    }
}
