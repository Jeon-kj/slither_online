using System.Collections.Generic;
using UnityEngine;
using static NetworkManager;
using static UnityEditor.Experimental.GraphView.GraphView;
using Slither.Protocol;

public class SlitherManager : MonoBehaviour
{
    public GameManager gameManager;
    public NetworkManager networkManager;

    Dictionary<int, SlitherContext> players = new Dictionary<int, SlitherContext>();
    public GameObject slitherPrefab; // 유니티에서 프리팹으로 연결
    public GameObject bodyPrefab;

    public void AddPlayer(int id, SlitherContext context) { players[id] = context; }
    public void RemovePlayer(int id) { players.Remove(id); }
    public SlitherContext GetPlayer(int id) { return players[id]; }

    public bool ContainsId(int id) { return players.ContainsKey(id);}

    public bool TryGetPlayer(int id, out SlitherContext context) { return players.TryGetValue(id, out context); }

    public void InitSlither(bool isLocalPlayer, MovePayload payload)
    {
        DebugUtil.Dbg("[InitSlither] isLocalPlayer : ", isLocalPlayer);
        Vector2 spawnPos = new Vector2(payload.x, payload.y);
        GameObject slither = Instantiate(slitherPrefab, spawnPos, Quaternion.identity);

        Transform head = slither.transform.Find("Head");
        Transform bodyGroup = slither.transform.Find("BodyGroup");
        DebugUtil.Dbg("[InitSlither] 오브젝트 생성  완료");
        if (head == null || bodyGroup == null)
        {
            Debug.LogError("Slither prefab 구조가 잘못되었습니다. Head 또는 BodyGroup이 없습니다.");
            return;
        }
        DebugUtil.Dbg("[InitSlither] SlitherContext 생성 직전");
        SlitherContext context = new SlitherContext
        {
            rootObject = slither,
            headObject = head.gameObject,
            bodyGroup = bodyGroup,
            bodyLen = payload.length
        };
        DebugUtil.Dbg("[InitSlither] SlitherContext 생성 완료");
        if (isLocalPlayer)
        {
            gameManager.InitPlayer(slither);
        }
        slither.name = $"Player_{payload.id}";
        slither.GetComponent<IdHolder>().SetId(payload.id);

        players[payload.id] = context;
        Debug.Log($"[Type : MSG_PLAYER_JOIN] payload.id : {payload.id}, payload.x : {payload.x}, payload.y : {payload.y}, slither : {slither}");
    }

    public void UpdateSlitherPos(MovePayload payload)
    {
        if (!ContainsId(payload.id))
        {
            return;
        }

        SlitherContext slither = players[payload.id];

        // 꼬리(body) 위치 갱신
        for (int i = slither.bodySegments.Count - 1; i > 0; i--)
        {
            slither.bodySegments[i].transform.position = slither.bodySegments[i - 1].transform.position;
        }
        if (slither.bodySegments.Count > 0)
        {
            slither.bodySegments[0].transform.position = slither.headObject.transform.position;
        }
        // head 위치 갱신
        slither.headObject.transform.position = new Vector2(payload.x, payload.y);
        slither.rootObject.transform.position = new Vector2(payload.x, payload.y);
    }

    public void DelSCtx(int id, SlitherContext sCtx)
    {
        Destroy(sCtx.headObject);
        foreach (var segment in sCtx.bodySegments)
        {
            Destroy(segment);
        }
        players.Remove(id);
    }

    public void UpdateSlitherLength(int id, int length)
    {
        if (!ContainsId(id))
        {
            return; 
        }
        
        players[id].bodyLen = length;

        SlitherContext slither = players[id];

        while (slither.bodySegments.Count < slither.bodyLen)
        {
            var segment = Instantiate(bodyPrefab);
            Vector2 pos;
            if (slither.bodySegments.Count > 0)
                pos = slither.bodySegments[slither.bodySegments.Count - 1].transform.position;
            else pos = slither.headObject.transform.position;

            segment.transform.position = pos;
            segment.transform.SetParent(slither.bodyGroup);
            slither.bodySegments.Add(segment);
        }
        // 길이가 줄었을 경우
        while (slither.bodySegments.Count > slither.bodyLen)
        {
            var lastSegment = slither.bodySegments[slither.bodySegments.Count - 1];
            GameObject.Destroy(lastSegment);
            slither.bodySegments.RemoveAt(slither.bodySegments.Count - 1);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Food"))
        {
            int foodId = other.GetComponent<IdHolder>().GetId();
            Vector2 hitPos = other.ClosestPoint(transform.position);

            networkManager.SendEat(foodId, hitPos);
        }
        else if (other.CompareTag("SlitherBody"))
        {
            int myId = gameManager.GetLocalPlayerId();
            int targetId = other.transform.parent.parent.GetComponent<IdHolder>().GetId();

            if (myId == targetId)
                return; // 내 body와 충돌 → 무시

            networkManager.SendHit(targetId, transform.position);
        }
    }
}
