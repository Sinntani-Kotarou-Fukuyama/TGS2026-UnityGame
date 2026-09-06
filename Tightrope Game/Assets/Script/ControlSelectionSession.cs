using UnityEngine;

public enum GameplayControlType
{
    Keyboard,
    JoyCon
}

/// <summary>TitleSceneで選んだ操作方法を、同じゲーム起動中だけ保持します。</summary>
public static class ControlSelectionSession
{
    public static bool HasSelection { get; private set; }
    public static GameplayControlType SelectedControlType { get; private set; } = GameplayControlType.Keyboard;

    public static void SetSelection(GameplayControlType controlType)
    {
        SelectedControlType = controlType;
        HasSelection = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSelection()
    {
        HasSelection = false;
        SelectedControlType = GameplayControlType.Keyboard;
    }
}
