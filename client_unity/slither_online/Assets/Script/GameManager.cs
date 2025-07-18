using UnityEngine;

public class GameManager : MonoBehaviour
{
    int localPlayerId = -1;

    public SlitherMove slitherMove;
    public CameraFollowTarget cameraFollow;

    public void SetLocalPlayerId(int id) {  localPlayerId = id; }

    public int GetLocalPlayerId() { return localPlayerId; }

    public void InitPlayer(GameObject slither)
    {
        slitherMove.SetSlither(slither.transform);
        cameraFollow.SetTarget(slither.transform);
    }
}
