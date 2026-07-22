using UnityEngine;
using UnityEngine.UI;

namespace VoidDay.View
{
    /// One transient corner notice (toast.generic, docs/UI-Mockups.md node 36:2). The prefab authors the look
    /// — rounded card, icon chip, one bold line; this only carries the message in and times itself out.
    ///
    /// It holds no opinion about *when* a toast is warranted. ToastController decides that by listening to
    /// domain events (UI-Inventory: "toasts listen to domain events and decide for themselves to appear;
    /// nothing tells them to"), instantiates one of these, and forgets about it — the toast destroys itself.
    public sealed class Toast : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image icon;
        [SerializeField] Text label;
        [SerializeField] Button dismissButton;

        float _fadeStartsAt;
        float _fadeSeconds;

        public void Show(string message, Sprite iconSprite, float lifetime, float fadeSeconds)
        {
            label.text = message;
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null; // a toast without an icon is a legitimate authoring choice
            group.alpha = 1f;
            _fadeStartsAt = Time.time + lifetime;
            _fadeSeconds = fadeSeconds;
            dismissButton.onClick.AddListener(Dismiss); // tap-to-dismiss, per the mockup
        }

        /// Skip the wait and fade out now. Idempotent — a second tap during the fade does nothing new.
        public void Dismiss()
        {
            if (_fadeStartsAt > Time.time) _fadeStartsAt = Time.time;
        }

        void Update()
        {
            if (Time.time < _fadeStartsAt) return;
            float faded = (Time.time - _fadeStartsAt) / _fadeSeconds;
            group.alpha = Mathf.Clamp01(1f - faded);
            if (faded >= 1f) Destroy(gameObject);
        }
    }
}
