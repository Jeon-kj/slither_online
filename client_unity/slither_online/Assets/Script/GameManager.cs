using UnityEngine;

public class GameManager : MonoBehaviour
{
    public SlitherMove slitherMove;
    public CameraFollowTarget cameraFollow;

    public void InitPlayer(GameObject slither)
    {
        slitherMove.slither = slither.transform;
        cameraFollow.target = slither.transform;
    }
}
