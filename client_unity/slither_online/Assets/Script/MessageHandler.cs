using UnityEngine;
using static NetworkManager;
using static UnityEditor.Experimental.GraphView.GraphView;
using Slither.Protocol;

public class MessageHandler : MonoBehaviour
{
    public SlitherManager slitherManager;
    public FoodManager foodManager;
    public GameManager gameManager;

    public bool ProcessMessage(int type, string body)
    {
        DebugUtil.Dbg("[ProcessMessage] type : ", type, ", body : ", body);
        // 메시지 처리 ...
        switch ((MessageType)type)
        {
            case MessageType.MSG_PLAYER_JOIN:
                HandlePlayerJoin(body);
                break;
            case MessageType.MSG_PLAYER_LEFT:
                HandlePlayerLeft(body);
                break;
            case MessageType.MSG_POS:
                HandlePos(body);
                break;
            case MessageType.MSG_ANGLE:
                break;
            case MessageType.MSG_CHAT:
                HandleChat(body);
                break;
            case MessageType.MSG_FOOD_SPAWN:
                HandleFoodSpawn(body); 
                break;
            case MessageType.MSG_FOOD_REMOVE:
                HandleFoodRemove(body);
                break;
            case MessageType.MSG_SLITHER_LENGTH_UPDATE:
                HandleSlitherLengthUpdate(body);
                break;
        }
        return true;
    }

    public void HandlePos(string body)
    {
        var payload = JsonUtility.FromJson<MovePayload>(body);
        //Debug.Log($"[Type : MSG_POS] id:{payload.id}, x:{payload.x}, y:{payload.y}, length:{payload.length}");

        MainThreadDispatcher.Enqueue(() => {
            slitherManager.UpdateSlitherPos(payload);
        });
    }

    public void HandleChat(string body)    // [수정 필요] 채팅창 구현
    {
        //{"id":id, "msg":msg}
        var payload = JsonUtility.FromJson<ChatPayload>(body);
        Debug.Log($"[Player_{payload.id}] : {payload.msg}");
    }

    public void HandlePlayerJoin(string body)
    {
        DebugUtil.Dbg("[HandlePlayerJoin] body : ", body);
        // 모든 플레이어들의 데이터를 전달받음.
        // id,x,y 값이 든 데이터를 받아 오브젝트를 생성하고 현재 players에 정보 저장.
        // 처음 받는다면, 모든 플레이어들 생성.

        // 새로 입장하여 모든 플레이어에 대한 정보를 받는 경우
        // 이 경우는 slither의 body segments는 어떻게 업데이트 하지?
        // 왜냐하면, 서버에서는 head에 대한 정보밖에 주지 않으니까.
        // 초기에는 그냥 머리에 대한 정보만 줘서 시간이 지날수록 업데이트 되도록 함
        if (body.TrimStart().StartsWith("{\"id\""))
        {
            DebugUtil.Dbg("[HandlePlayerJoin] first if");
            var payloadList = JsonUtility.FromJson<MovePayloadList>(body);
            gameManager.SetLocalPlayerId(payloadList.id);

            foreach (var payload in payloadList.payloads)
            {
                DebugUtil.Dbg("[HandlePlayerJoin] payload : ", payload);
                MainThreadDispatcher.Enqueue(() => {
                    Debug.Log($"[MainThreadDispatcher] MainThreadDispatcher");
                    bool flag = payload.id == gameManager.GetLocalPlayerId();
                    slitherManager.InitSlither(flag, payload);
                });
            }
        }
        // 새로운 플레이어에 대한 정보를 받는 경우
        else
        {
            DebugUtil.Dbg("[HandlePlayerJoin] else");
            var payload = JsonUtility.FromJson<MovePayload>(body);
            Debug.Log($"[Type : MSG_HELL] id:{payload.id}, x:{payload.x}, y:{payload.y}");

            MainThreadDispatcher.Enqueue(() => {
                slitherManager.InitSlither(false, payload);
            });
        }
    }

    public void HandlePlayerLeft(string body)
    {
        var payload = JsonUtility.FromJson<PlayerLeftPayload>(body);
        if (slitherManager.TryGetPlayer(payload.id, out SlitherContext sCtx))
        {
            MainThreadDispatcher.Enqueue(() => {
                slitherManager.DelSCtx(payload.id, sCtx);
            });
        }
    }

    public void HandleFoodSpawn(string body)
    {
        DebugUtil.Dbg("[HandleFoodSpawn]");
        var payloadList = JsonUtility.FromJson<FoodSpawnPayloadList>(body);

        foreach (var payload in payloadList.payloads)
        {
            DebugUtil.Dbg("[HandleFoodSpawn] payload : ", payload);
            MainThreadDispatcher.Enqueue(() => {
                Debug.Log($"[HandleFoodSpawn] MainThreadDispatcher");
                foodManager.ApplyFoodSpawn(payload);
            });
        }
    }

    public void HandleFoodRemove(string body)
    {
        DebugUtil.Dbg("[HandleFoodRemove]");
        var payload = JsonUtility.FromJson<FoodRemovePayload>(body);

        DebugUtil.Dbg("[HandleFoodRemove] payload : ", payload);
        MainThreadDispatcher.Enqueue(() => {
            Debug.Log($"[HandleFoodRemove] MainThreadDispatcher");
            foodManager.ApplyFoodRemove(payload);
        });
    }

    public void HandleSlitherLengthUpdate(string body)
    {
        DebugUtil.Dbg("[HandleSlitherLengthUpdate]");
        var payload = JsonUtility.FromJson<SlitherLengthUpdatePayload>(body);

        MainThreadDispatcher.Enqueue(() => {
            Debug.Log($"[HandleSlitherLengthUpdate] MainThreadDispatcher");
            slitherManager.UpdateSlitherLength(payload.id, payload.length);
        });
    }
}
