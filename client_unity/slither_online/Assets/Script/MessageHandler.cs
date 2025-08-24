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
        DebugUtil.DbgTag("MESSAGE", "[ProcessMessage] type : ", type, ", body : ", body);
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
        if (!gameManager.GetJoinStatus()) return;
        var payloads = JsonUtility.FromJson<MovePayloadList>(body);

        foreach (var payload in payloads.posPayloads)
        {
            MainThreadDispatcher.Enqueue(() => {
                slitherManager.UpdateSlitherPos(payload);
            });
        }        
    }

    public void HandleChat(string body)    // [수정 필요] 채팅창 구현
    {
        //{"id":id, "msg":msg}
        var payload = JsonUtility.FromJson<ChatPayload>(body);
        Debug.Log($"[Player_{payload.id}] : {payload.msg}");
    }

    public void HandlePlayerJoin(string body)
    {
        DebugUtil.DbgTag("JOIN", "[HandlePlayerJoin] body : ", body);
        // 모든 플레이어들의 데이터를 전달받음.
        // id,x,y 값이 든 데이터를 받아 오브젝트를 생성하고 현재 players에 정보 저장.
        // 처음 받는다면, 모든 플레이어들 생성.

        // 새로 입장하여 모든 플레이어에 대한 정보를 받는 경우
        // 이 경우는 slither의 body segments는 어떻게 업데이트 하지?
        // 왜냐하면, 서버에서는 head에 대한 정보밖에 주지 않으니까.
        // 초기에는 그냥 머리에 대한 정보만 줘서 시간이 지날수록 업데이트 되도록 함
        if (body.Contains("slitherPayloads"))
        {
            var payloadList = JsonUtility.FromJson<PlayerJoinPayloadList>(body);
            DebugUtil.DbgTag("ERRORCHECK", "payloadList.id: ", payloadList.id);
            gameManager.SetLocalPlayerId(payloadList.id);

            foreach (var payload in payloadList.slitherPayloads)
            {
                DebugUtil.DbgTag("JOIN", "[HandlePlayerJoin] payload : ", payload);
                MainThreadDispatcher.Enqueue(() => {
                    bool flag = payload.id == gameManager.GetLocalPlayerId();
                    DebugUtil.DbgTag("ERRORCHECK", "payloadList.id: ", payloadList.id);
                    DebugUtil.DbgTag("ERRORCHECK", "gameManager.GetLocalPlayerId(): ", gameManager.GetLocalPlayerId());
                    DebugUtil.DbgTag("ERRORCHECK", "payload.id: ", payload.id);
                    DebugUtil.DbgTag("ERRORCHECK", "payload.length: ", payload.length);
                    slitherManager.InitSlither(flag, payload);
                });
            }
            foreach (var payload in payloadList.foodPayloads)
            {
                DebugUtil.DbgTag("JOIN", "[HandlePlayerJoin] payload : ", payload);
                MainThreadDispatcher.Enqueue(() => {
                    foodManager.ApplyFoodSpawn(payload);
                });
            }
        }
        // 새로운 플레이어에 대한 정보를 받는 경우
        else
        {
            var payload = JsonUtility.FromJson<PlayerJoinPayload>(body);
            DebugUtil.DbgTag("NEW_PLAYER", $"[Type : MSG_HELL] id:{payload.id}, x:{payload.x}, y:{payload.y}");

            MainThreadDispatcher.Enqueue(() => {
                slitherManager.InitSlither(false, payload);
            });
        }
    }

    public void HandlePlayerLeft(string body)
    {
        var payload = JsonUtility.FromJson<PlayerLeftPayload>(body);
        if (slitherManager.TryGetSlither(payload.id, out SlitherContext sCtx))
        {
            MainThreadDispatcher.Enqueue(() => {
                slitherManager.DelSCtx(payload.id, sCtx);
            });
        }
    }

    public void HandleFoodSpawn(string body)
    {
        DebugUtil.DbgTag("SEQUENCE", "[HandleFoodSpawn] body: ", body);
        var payloadList = JsonUtility.FromJson<FoodSpawnPayloadList>(body);

        foreach (var payload in payloadList.payloads)
        {
            DebugUtil.DbgTag("FOOD", "[HandleFoodSpawn] payload : ", payload);
            MainThreadDispatcher.Enqueue(() => {
                foodManager.ApplyFoodSpawn(payload);
            });
        }
    }

    public void HandleFoodRemove(string body)
    {
        DebugUtil.DbgTag("SEQUENCE", "[HandleFoodRemove]");
        var payload = JsonUtility.FromJson<FoodRemovePayload>(body);

        DebugUtil.DbgTag("FOOD", "[HandleFoodRemove] payload : ", payload);
        MainThreadDispatcher.Enqueue(() => {
            foodManager.ApplyFoodRemove(payload);
        });
    }

    public void HandleSlitherLengthUpdate(string body)
    {
        DebugUtil.DbgTag("SEQUENCE", "[HandleSlitherLengthUpdate]");
        var payload = JsonUtility.FromJson<SlitherLengthUpdatePayload>(body);

        MainThreadDispatcher.Enqueue(() => {
            slitherManager.UpdateSlitherLength(payload.id, payload.length);
        });
    }
}
