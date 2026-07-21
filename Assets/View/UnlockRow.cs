using UnityEngine;
using UnityEngine.UI;

namespace VoidDay.View
{
    /// One line of popup.levelUp's UNLOCKED list (24:2): an icon chip and a sentence. The look is authored in
    /// the prefab; LevelUpPopup instantiates one per unlock and binds it.
    public sealed class UnlockRow : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] Text label;

        public void Bind(Sprite iconSprite, string text)
        {
            icon.sprite = iconSprite;
            label.text = text;
        }
    }
}
