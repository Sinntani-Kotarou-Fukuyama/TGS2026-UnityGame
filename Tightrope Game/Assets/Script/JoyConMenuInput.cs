using UnityEngine;

/// <summary>Joy-Conを使用したメニュー操作の1フレーム分の入力です。</summary>
public readonly struct JoyConMenuInputFrame
{
    public static readonly JoyConMenuInputFrame None = new JoyConMenuInputFrame(0, 0, false);

    public int HorizontalStep { get; }
    public int VerticalStep { get; }
    public bool ConfirmPressed { get; }

    public JoyConMenuInputFrame(int horizontalStep, int verticalStep, bool confirmPressed)
    {
        HorizontalStep = horizontalStep;
        VerticalStep = verticalStep;
        ConfirmPressed = confirmPressed;
    }
}

/// <summary>
/// Joy-Con R横持ち用の軽量なメニュー入力Helperです。
/// 各UI Controllerが個別にインスタンスを保持することで、画面間でLock状態を共有しません。
/// </summary>
public sealed class JoyConMenuInput
{
    public const float DefaultInputThreshold = 0.5f;
    public const float DefaultNeutralThreshold = 0.25f;

    private readonly float inputThreshold;
    private readonly float neutralThreshold;

    private bool horizontalLocked;
    private bool verticalLocked;

    public JoyConMenuInput(
        float inputThreshold = DefaultInputThreshold,
        float neutralThreshold = DefaultNeutralThreshold)
    {
        this.inputThreshold = Mathf.Max(0f, inputThreshold);
        this.neutralThreshold = Mathf.Clamp(neutralThreshold, 0f, this.inputThreshold);
    }

    public JoyConMenuInputFrame Read()
    {
        Joycon joycon = GetCurrentJoycon();
        if (joycon == null)
        {
            return JoyConMenuInputFrame.None;
        }

        float[] stick = joycon.GetStick();
        if (stick == null || stick.Length < 2)
        {
            return JoyConMenuInputFrame.None;
        }

        float horizontal = stick[1];
        float vertical = -stick[0];
        int horizontalStep = GetStep(horizontal, ref horizontalLocked);
        int verticalStep = GetStep(vertical, ref verticalLocked);
        bool confirmPressed = joycon.GetButtonDown(Joycon.Button.DPAD_UP);

        return new JoyConMenuInputFrame(horizontalStep, verticalStep, confirmPressed);
    }

    public void Reset()
    {
        horizontalLocked = false;
        verticalLocked = false;
    }

    private int GetStep(float axis, ref bool isLocked)
    {
        float absoluteAxis = Mathf.Abs(axis);

        if (isLocked)
        {
            if (absoluteAxis <= neutralThreshold)
            {
                isLocked = false;
            }

            return 0;
        }

        if (absoluteAxis < inputThreshold)
        {
            return 0;
        }

        isLocked = true;
        return axis > 0f ? 1 : -1;
    }

    private static Joycon GetCurrentJoycon()
    {
        JoyconManager manager = JoyconManager.Instance;
        if (manager == null || manager.j == null || manager.j.Count == 0)
        {
            return null;
        }

        return manager.j[0];
    }
}
