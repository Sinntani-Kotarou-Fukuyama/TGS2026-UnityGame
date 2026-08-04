using UnityEngine;
using UnityEngine.UI;

public class BlanceBar_Vignette : MonoBehaviour
{
    [Header("親オブジェクト（傾きを検知するもの）")]
    [SerializeField] private GameObject Parent;

    [Header("左右の端のUI（Imageオブジェクト）")]
    [SerializeField] private Image leftEndImage;
    [SerializeField] private Image rightEndImage;

    [Header("角度の設定")]
    [SerializeField] private float maxWarningAngle = 30f; // 完全に真っ黒になる限界の角度

    void Update()
    {
        if (Parent == null) return;

        // Z軸の回転を取得し、-180 〜 180 の範囲に変換
        float currentAngle = Parent.transform.localEulerAngles.z;
        if (currentAngle > 180f) currentAngle -= 360f;

        // 傾きの割合（0.0 〜 1.0）を計算
        float dangerRatio = Mathf.Clamp01(Mathf.Abs(currentAngle) / maxWarningAngle);

        // 基本の色は「黒」。傾きに応じてアルファ値（透明度）を 0.0(透明) から 1.0(真っ黒) に変化させる
        Color targetColor = new Color(0f, 0f, 0f, dangerRatio);
        Color transparentColor = new Color(0f, 0f, 0f, 0f); // 完全に透明な黒

        // 左右どちらに傾いているかで処理を分岐
        // ※「右に傾いた時に右端を暗くする」仕様
        if (currentAngle < 0)
        {
            // 右に傾いている場合：右端を徐々に暗く、左端は完全に透明
            SetImageAlpha(rightEndImage, targetColor);
            SetImageAlpha(leftEndImage, transparentColor);
        }
        else if (currentAngle > 0)
        {
            // 左に傾いている場合：左端を徐々に暗く、右端は完全に透明
            SetImageAlpha(leftEndImage, targetColor);
            SetImageAlpha(rightEndImage, transparentColor);
        }
        else
        {
            // 完全に水平な場合：両方透明
            SetImageAlpha(leftEndImage, transparentColor);
            SetImageAlpha(rightEndImage, transparentColor);
        }
    }

    private void SetImageAlpha(Image img, Color color)
    {
        if (img != null)
        {
            img.color = color;
        }
    }



    //-------------------------------------------------------
    //3Dオブジェクトを発行させる場合
    //-------------------------------------------------------
    /*
    [Header("親オブジェクト")]
    [SerializeField] private GameObject Parent;
    [Header("左右の端のオブジェクト（Rendererを持つもの）")]
    [SerializeField] private Renderer leftEndRenderer;
    [SerializeField] private Renderer rightEndRenderer;

    [Header("角度の設定")]
    [SerializeField] private float maxWarningAngle = 30f; // 完全に真っ赤に光る限界の角度

    [Header("色の設定")]
    [ColorUsage(true, true)]
    [SerializeField] private Color normalColor = Color.black; // 通常時（発光なし）
    [ColorUsage(true, true)]
    [SerializeField] private Color maxDangerColor = Color.red; // 最大傾き時の発光色

    private Material leftMaterial;
    private Material rightMaterial;
    private int emissionColorID;

    void Start()
    {
        // 割り当てられたオブジェクトからマテリアルを取得し、Emissionを有効化
        if (leftEndRenderer != null)
        {
            leftMaterial = leftEndRenderer.material;
            leftMaterial.EnableKeyword("_EMISSION");
        }
        if (rightEndRenderer != null)
        {
            rightMaterial = rightEndRenderer.material;
            rightMaterial.EnableKeyword("_EMISSION");
        }

        emissionColorID = Shader.PropertyToID("_EmissionColor");
    }

    void Update()
    {
        // Z軸の回転を取得し、-180 〜 180 の範囲に変換
        float currentAngle = Parent.transform.localEulerAngles.z;
        if (currentAngle > 180f) currentAngle -= 360f;

        // 傾きの割合（0.0 〜 1.0）を計算
        float dangerRatio = Mathf.Clamp01(Mathf.Abs(currentAngle) / maxWarningAngle);
        Color targetGlowColor = Color.Lerp(normalColor, maxDangerColor, dangerRatio);

        // 左右どちらに傾いているかで処理を分岐
        // ※Unityの標準的な配置では、Z軸がプラスで左傾き、マイナスで右傾きになります
        if (currentAngle > 0)
        {
            // 左に傾いている場合：左を光らせ、右は消灯
            SetEmissionColor(leftMaterial, targetGlowColor);
            SetEmissionColor(rightMaterial, normalColor);
        }
        else if (currentAngle < 0)
        {
            // 右に傾いている場合：右を光らせ、左は消灯
            SetEmissionColor(leftMaterial, normalColor);
            SetEmissionColor(rightMaterial, targetGlowColor);
        }
        else
        {
            // 完全に水平な場合：両方消灯
            SetEmissionColor(leftMaterial, normalColor);
            SetEmissionColor(rightMaterial, normalColor);
        }
    }

    private void SetEmissionColor(Material mat, Color color)
    {
        if (mat != null)
        {
            mat.SetColor(emissionColorID, color);
        }
    }
    */
}
