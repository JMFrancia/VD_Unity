using UnityEngine;
using UnityEngine.UI;
using VoidDay.Data;

namespace VoidDay.View
{
    /// One row of the quest menu (§ quest system, Figma frame 01): the description on top, then a filled
    /// progress bar with the % on the right. The look is authored in the QuestRow prefab; QuestMenuPanel
    /// instantiates one per quest and binds it. A ready-to-collect row is highlighted and its whole surface
    /// is the collect button (interactable only when ready).
    public sealed class QuestRow : MonoBehaviour
    {
        [SerializeField] UiThemeSO theme;
        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] Text descriptionText;
        [SerializeField] Image progressFill;
        [SerializeField] Text percentText;

        [Tooltip("Row background while the quest is still in progress.")]
        [SerializeField] Color activeBackground = new(1f, 1f, 1f, 1f);
        [Tooltip("Row background once the quest is ready to collect — the highlight that pulls the eye.")]
        [SerializeField] Color readyBackground = new(0.85f, 0.94f, 0.76f, 1f);

        public Button Button => button;

        public void Bind(string description, float progress, bool ready)
        {
            descriptionText.text = description;
            descriptionText.color = theme.ink;
            progressFill.fillAmount = Mathf.Clamp01(progress);
            progressFill.color = theme.accent;
            percentText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
            percentText.color = theme.ink;
            background.color = ready ? readyBackground : activeBackground;
            button.interactable = ready;
        }
    }
}
