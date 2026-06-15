using UnityEngine;
using Unity.Cinemachine;


public class CameraSwhich : MonoBehaviour
{
    //CameraManagerにアタッチしてある
    //Camera1にManCamera,Camera2にDinoCameraを入れる
    [SerializeField] private CinemachineCamera mancamera;
    [SerializeField] private CinemachineCamera dinocamera;
    [SerializeField] private CinemachineCamera eventdinocamera;

    
    void Start()
    {
        Debug.Log("怪獣注目のやつEscでスキップできるよ");
        //カメラの描画優先度を変える
        mancamera.Priority.Value = 15;
        dinocamera.Priority.Value = 5;
        Invoke("GameStartCamera",0.7f);
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
            mancamera.Priority.Value = 15;
            dinocamera.Priority.Value = 5;
            
        
        }
    }
    void GameStartCamera()
    {
        //カメラの描画優先度を変える
        mancamera.Priority.Value = 5;
        dinocamera.Priority.Value = 15;
    }
    private void CameraSet()
    {
        //カメラの描画優先度を変える
        mancamera.Priority.Value = 15;
        dinocamera.Priority.Value = 5;
    }
    public void EventCameraSet()
    {
        var brain = Camera.main.GetComponent<CinemachineBrain>();

        //DefaltBlendをEaseInOut(カメラをゆっくり切り替え)に変更
        var blend = brain.DefaultBlend;
        blend.Style = CinemachineBlendDefinition.Styles.EaseInOut;
        blend.Time = 2f;
        brain.DefaultBlend = blend;

        mancamera.Priority.Value = 5;
        eventdinocamera.Priority.Value = 15;
        Invoke("EventFinish", 8.0f);
    }
    void EventFinish()
    {
        mancamera.Priority.Value = 15;
        eventdinocamera.Priority.Value = 0;
    }
}
