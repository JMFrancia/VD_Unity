using UnityEngine;
using UnityEngine.UI;
using VoidDay.Data;

namespace VoidDay.View
{
    /// One have/need row in the station popup's detail card: chip, resource name, held/needed, ✓/! badge.
    /// State colors come from the theme SO (runtime-switched); everything else is authored in the prefab.
    public sealed class IngredientRow : MonoBehaviour
    {
        [SerializeField] UiThemeSO theme;
        [SerializeField] Image icon;
        [SerializeField] Text nameText;
        [SerializeField] Text haveNeedText;
        [SerializeField] Image badge;
        [SerializeField] Text badgeText;

        public void Bind(string displayName, Sprite iconSprite, int held, int needed)
        {
            bool ok = held >= needed;
            icon.sprite = iconSprite;
            nameText.text = displayName;
            haveNeedText.text = $"{held} / {needed}";
            haveNeedText.color = ok ? theme.ink : theme.warning;
            badge.color = ok ? theme.accent : theme.warning;
            badgeText.text = ok ? "✓" : "!";
        }
    }
}
