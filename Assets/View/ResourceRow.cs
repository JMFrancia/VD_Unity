using UnityEngine;
using UnityEngine.UI;

namespace VoidDay.View
{
    /// One row of the total-resources popup (27:2): chip, name, count. The look is authored in the prefab;
    /// Hud instantiates one per resource and binds it.
    public sealed class ResourceRow : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] Text nameText;
        [SerializeField] Text countText;

        public void Bind(string displayName, Sprite iconSprite, int count)
        {
            icon.sprite = iconSprite;
            nameText.text = displayName;
            countText.text = count.ToString();
        }
    }
}
