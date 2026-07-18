using UnityEngine;
using UnityEngine.UI;

namespace VoidDay.View
{
    /// One recipe tile in the station popup's tile row (ALT 42:2): chip, name, timer, selection outline.
    /// The look is authored in the prefab; StationPanel instantiates one per recipe and binds it.
    public sealed class RecipeTile : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] GameObject selectionOutline;
        [SerializeField] Text nameText;
        [SerializeField] Text timerText;

        public Button Button => button;

        public void Bind(string label, string timer, bool selected)
        {
            nameText.text = label;
            timerText.text = timer;
            selectionOutline.SetActive(selected);
        }
    }
}
