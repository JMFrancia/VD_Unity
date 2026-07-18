using UnityEngine;

namespace VoidDay.Data
{
    /// The UI theme (StyleGuide "Type & UI" + Color). One ScriptableObject that owns every colour, type size,
    /// corner radius, and font the View chrome reads — so a designer retunes the look in the inspector and no
    /// mockup value is ever inlined in code (CLAUDE.md rule 1). Values start from docs/StyleGuide.md roles and
    /// the chosen Figma mockups (panel.station ALT 42:2, popup.totalResources 27:2, menu.debug 22:2, in-world
    /// 34:2). Farm surfaces are warm/neutral; only the ready icon and the debug button borrow the void accent.
    [CreateAssetMenu(menuName = "VoidDay/UI Theme", fileName = "UiTheme")]
    public sealed class UiThemeSO : ScriptableObject
    {
        [Header("Font (null → builtin LegacyRuntime fallback)")]
        public Font font;

        [Header("Type sizes (@1080 reference width)")]
        public int sizeTitle = 44;    // output name, panel title, Queue
        public int sizeHeading = 40;  // building name, money
        public int sizeBody = 30;     // resource name, have/need
        public int sizeLabel = 26;    // tile name, timer chip
        public int sizeSmall = 22;    // tile timer, subtext

        [Header("Corner radii (px @1080)")]
        public int radiusPanel = 34;
        public int radiusCard = 26;
        public int radiusTile = 22;
        public int radiusButton = 30;
        public int radiusPill = 24;
        public int radiusChip = 14;
        public int radiusBar = 8;

        [Header("Farm UI palette")]
        public Color panelBg = C(0xF7, 0xEE, 0xDD);   // cream panel
        public Color cardBg = C(0xFF, 0xF8, 0xEC);    // inner card
        public Color tileBg = C(0xFF, 0xFF, 0xFF);    // recipe tile / slot fill
        public Color ink = C(0x5A, 0x46, 0x32);       // dark brown text
        public Color inkMuted = C(0xA8, 0x8E, 0x63);  // secondary text / timers
        public Color accent = C(0x5F, 0xA8, 0x3C);    // green: Queue, available, selection, check
        public Color accentText = C(0xFF, 0xFF, 0xFF);
        public Color warning = C(0xD9, 0x53, 0x4F);   // can't-afford / at-cap
        public Color lockedBg = C(0xE7, 0xE0, 0xD1);  // disabled tile bg
        public Color lockedText = C(0xB4, 0xAE, 0x9F); // disabled tile text
        public Color resourceChip = C(0xE3, 0xB2, 0x3C); // placeholder resource-icon chip (real icons later)
        public Color chipBg = C(0xEF, 0xE6, 0xD3);    // timer chip bg

        [Header("Building label pill")]
        public Color namePillBg = C(0x2A, 0x24, 0x38);
        public Color namePillText = C(0xF3, 0xEE, 0xFF);

        [Header("In-world state (34:2)")]
        public Color progressTrack = C(0x2A, 0x24, 0x38);
        public Color progressFill = C(0x7D, 0xBE, 0x5A);
        public Color readyAccent = C(0x8B, 0x5C, 0xF6); // void accent — the one farm-loop element that glows
        public Color slotFill = C(0xFF, 0xFF, 0xFF, 0.92f);
        public Color slotEmpty = C(0xFF, 0xFF, 0xFF, 0.9f); // empty-slot outline

        [Header("HUD money pill")]
        public Color hudPillBg = C(0xFF, 0xF8, 0xEC);
        public Color hudPillText = C(0x5A, 0x46, 0x32);

        [Header("Debug menu (dark, utilitarian — 22:2)")]
        public Color debugPanelBg = C(0x2A, 0x24, 0x38);
        public Color debugButtonBg = C(0x3B, 0x35, 0x50);
        public Color debugText = C(0xF3, 0xEE, 0xFF);
        public Color debugSubtext = C(0x8B, 0x84, 0xA6);
        public Color debugAccent = C(0x8B, 0x5C, 0xF6);
        public Color debugResetBg = C(0x5A, 0x2E, 0x2E);
        public Color debugResetText = C(0xE8, 0xA0, 0xA0);

        static Color C(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);
    }
}
