using UnityEditor;
using UnityEngine.InputSystem;

namespace VoidDay.EditorTools
{
    /// Editor-only safety net — lives in an Editor/ folder, so it is NOT compiled into the WebGL/device
    /// build and never touches real-device input.
    ///
    /// The problem it fixes: Unity's **Device Simulator** window emulates a phone by DISABLING the Mouse and
    /// Pen devices. With the mouse disabled, Pointer.current falls to a synthetic Touchscreen that nothing
    /// feeds, so every input path that reads Pointer.current (camera pan, world taps) and the
    /// InputSystemUIInputModule (UI clicks) go dead — the whole editor becomes untappable. Since this
    /// project's entire verification gate is "press Play," a silently-disabled mouse blocks all iteration.
    ///
    /// This keeps Mouse/Pen enabled whenever the editor is running: on load, on entering Play mode, and
    /// reactively the instant anything disables them. Re-enabling holds (the Simulator disables once per
    /// activation, it does not fight back frame-to-frame), so this is a guard, not a busy-loop.
    [InitializeOnLoad]
    static class EditorMouseGuard
    {
        static EditorMouseGuard()
        {
            EnablePointers();
            EditorApplication.playModeStateChanged += _ => EnablePointers();
            InputSystem.onDeviceChange += (device, change) =>
            {
                // Only react to a disable, and only re-enable — EnableDevice raises an Enabled change, not a
                // Disabled one, so there is no re-entrant loop here.
                if (change == InputDeviceChange.Disabled && (device is Mouse || device is Pen))
                    InputSystem.EnableDevice(device);
            };
        }

        static void EnablePointers()
        {
            if (Mouse.current != null && !Mouse.current.enabled) InputSystem.EnableDevice(Mouse.current);
            if (Pen.current != null && !Pen.current.enabled) InputSystem.EnableDevice(Pen.current);
        }
    }
}
