using UnityEngine;

public class CameraFollowTarget : MonoBehaviour
{
    public Transform target;
    public float followSpeed = 5f;

    void LateUpdate()
    {
        if (target == null)
            return;

        DebugUtil.Dbg("[CameraFollowTarget]");
        Vector3 targetPosition = target.position;
        DebugUtil.Dbg("[CameraFollowTarget] targetPosition : ", targetPosition);
        targetPosition.z = transform.position.z; // 카메라 z 고정

        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        DebugUtil.Dbg("[CameraFollowTarget] transform.position : ", transform.position);
    }
}
