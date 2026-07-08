using System.Linq;
using UnityEngine;
using static Unity.Cinemachine.IInputAxisOwner.AxisDescriptor;

public class CameraControlCollider_Kaiju : MonoBehaviour
{
    [SerializeField] private CameraSwhich camera;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("koko");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("naa");
        if (other.CompareTag("Kaiju"))
        {
            Debug.Log("aai");

            camera.DinoUnderRopeCameraSet();
        }
    }
    
}
