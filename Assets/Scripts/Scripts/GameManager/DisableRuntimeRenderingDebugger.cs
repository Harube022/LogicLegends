using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Prevents Rendering Debugger gestures from opening UI during gameplay.</summary>
public static class DisableRuntimeRenderingDebugger
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void DisableRuntimeUI()
    {
        // Disables the Debug Updater as well as its keyboard/gamepad/touch toggle
        // polling. Runs in the Editor and all player builds, before scene startup.
        DebugManager.instance.enableRuntimeUI = false;
        DebugManager.instance.displayRuntimeUI = false;
        DebugManager.instance.displayPersistentRuntimeUI = false;
    }
}
