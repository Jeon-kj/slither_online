using UnityEngine;
using UnityEngine.InputSystem;

public class SlitherMove : MonoBehaviour
{
    Transform slither;
    public NetworkManager networkManager;
    private Camera mainCam;

    public float sendInterval = 0.05f; // 50ms 20fps
    private float timer = 0f;

    void Start()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if(slither != null)
        {
            timer += Time.deltaTime;
            if (timer >= sendInterval)
            {
                timer = 0f;

                Vector3 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mouse.z = 0f;

                Vector3 head = slither.position;

                float angle = Mathf.Atan2(mouse.y - head.y, mouse.x - head.x);

                networkManager.SendAngle(angle);
            }
        }        
    }   

    public void SetSlither(Transform slitherTransform) { slither = slitherTransform; }
}
