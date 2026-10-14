using UnityEngine;
using UnityEngine.InputSystem;

// One shared move action (WASD, arrow keys, gamepad stick) for the maze and the fight.
public static class GameInput
{
    static InputAction move;

    // Test hooks (Autopilot): replace player input without a keyboard.
    public static Vector2? MoveOverride;
    public static bool InteractHeldOverride;
    public static int PendingPresses;

    public static Vector2 MoveValue => MoveOverride ?? Move.ReadValue<Vector2>();

    // Domain reload is off when entering Play mode; rebuild the action for each session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        move?.Dispose();
        move = null;
        MoveOverride = null;
        InteractHeldOverride = false;
        PendingPresses = 0;
    }

    static bool Key(Key k, bool held)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;
        return held ? kb[k].isPressed : kb[k].wasPressedThisFrame;
    }

    // E: pull boxes (hold) and flip levers (press); gamepad: west button.
    public static bool InteractHeld => InteractHeldOverride || Key(UnityEngine.InputSystem.Key.E, true) ||
                                       (Gamepad.current?.buttonWest.isPressed ?? false);
    public static bool InteractPressed => (PendingPresses > 0 && PendingPresses-- > 0) ||
                                          Key(UnityEngine.InputSystem.Key.E, false) ||
                                          (Gamepad.current?.buttonWest.wasPressedThisFrame ?? false);
    // Space: shield in fights; gamepad: south button.
    public static bool ShieldPressed => Key(UnityEngine.InputSystem.Key.Space, false) ||
                                        (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false);
    public static bool RestartPressed => Key(UnityEngine.InputSystem.Key.R, false);

    public static InputAction Move
    {
        get
        {
            if (move != null) return move;
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            move.Enable();
            return move;
        }
    }
}
