using UnityEngine.InputSystem;

namespace RobotSavaslari.Robots
{
    public static class KeyboardDriveAxes
    {
        // Each direction counts once, even when both of its bindings are held.
        public static float ReadAxis(Keyboard keyboard, Key positiveKey, Key negativeKey,
            Key positiveAlias, Key negativeAlias)
        {
            bool positive = IsHeld(keyboard, positiveKey) || IsHeld(keyboard, positiveAlias);
            bool negative = IsHeld(keyboard, negativeKey) || IsHeld(keyboard, negativeAlias);
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private static bool IsHeld(Keyboard keyboard, Key key) =>
            key != Key.None && keyboard[key].isPressed;
    }
}
