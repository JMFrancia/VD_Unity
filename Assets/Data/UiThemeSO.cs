using UnityEngine;

namespace VoidDay.Data
{
    /// Runtime UI *state* colors — the colors code switches between as Core state changes (afford / can't
    /// afford, enabled / locked). Static chrome (panel colors, sizes, radii) is authored directly in the UI
    /// prefabs (CLAUDE.md rule 1/4); this SO holds only what code must apply at runtime.
    [CreateAssetMenu(menuName = "VoidDay/UI Theme", fileName = "UiTheme")]
    public sealed class UiThemeSO : ScriptableObject
    {
        [Header("Text")]
        public Color ink = C(0x5A, 0x46, 0x32);        // default dark-brown text
        public Color warning = C(0xD9, 0x53, 0x4F);    // can't-afford / at-cap

        [Header("Action states")]
        public Color accent = C(0x5F, 0xA8, 0x3C);     // green: Queue available, affordability check
        public Color accentText = C(0xFF, 0xFF, 0xFF);
        public Color lockedBg = C(0xE7, 0xE0, 0xD1);   // disabled button bg
        public Color lockedText = C(0xB4, 0xAE, 0x9F); // disabled button text

        static Color C(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);
    }
}
