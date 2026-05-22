using UnityEngine;
using Unity.Cinemachine;


public class CameraSwhich : MonoBehaviour
{
    //このスクリプトはCameraManagerにアタッチしてある
    //Camera1にManCamera,Camera2にDinoCameraを入れる
    [SerializeField] private CinemachineCamera camera1;
    [SerializeField] private CinemachineCamera camera2;
  
    void Start()
    {
        Debug.Log("怪獣注目のやつEscでスキップできるよ");
        //カメラの描画優先度を変える
        camera1.Priority.Value = 5;
        camera2.Priority.Value = 15;
        
        Invoke("CameraSet", 5f);//5秒でカメラを切り替える
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            var brain = Camera.main.GetComponent<CinemachineBrain>();

            //DefaltBlendをcut(カメラを瞬時切り替え)に変更
            var blend = brain.DefaultBlend;
            blend.Style = CinemachineBlendDefinition.Styles.Cut;
            blend.Time = 0f;
            brain.DefaultBlend = blend;
            //カメラの描画優先度を変える
            camera1.Priority.Value = 15;
            camera2.Priority.Value = 5;
            
        
        }
    }
    private void CameraSet()
    {
        //カメラの描画優先度を変える
        camera1.Priority.Value = 15;
        camera2.Priority.Value = 5;
    }
   
}
