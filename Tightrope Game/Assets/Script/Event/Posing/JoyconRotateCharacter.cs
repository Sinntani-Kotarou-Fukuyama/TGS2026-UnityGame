using UnityEngine;

public class JoyconRotateCharacter : MonoBehaviour
{
    Joycon jc;
    float rotationY = 0f; // キャラの現在の回転角

    void Start()
    {
        var joycons = JoyconManager.Instance.j;
        if (joycons.Count > 0)
        {
            jc = joycons[0]; // 0番目のJoy-Conを使う
        }
    }

    void Update()
    {
        if (jc == null) return;

        // ジャイロ取得
        Vector3 gyro = jc.GetGyro();

        // Joy-Con のヨー回転（Z軸）をキャラのY回転に変換
        float yaw = gyro.z * Time.deltaTime * 5f;
        // ↑ 5f は感度。好みで調整

        rotationY += yaw;

        // キャラを回転
        transform.rotation = Quaternion.Euler(0, rotationY, 0);
    }
}
