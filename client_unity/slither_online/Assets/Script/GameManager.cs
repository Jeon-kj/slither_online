using UnityEngine;

public class GameManager : MonoBehaviour
{
    int localPlayerId = -1;
    bool disposed = false;

    public SlitherMove slitherMove;
    public CameraFollowTarget cameraFollow;
    public SlitherManager slitherManager;
    public NetworkManager networkManager;

    public void DisposeGame()
    {
        if (disposed) return;
        disposed = true;

        networkManager.DisposeClient();
        slitherManager.DisposeSlither();        
    }

    public void SetLocalPlayerId(int id) {  localPlayerId = id; }

    public int GetLocalPlayerId() { return localPlayerId; }

    public void InitPlayer(GameObject slither)
    {
        slitherMove.SetSlither(slither.transform);
        cameraFollow.SetTarget(slither.transform);
    }

    public bool GetJoinStatus() { return (localPlayerId != -1); }
}
