using Slither.Protocol;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static NetworkManager;
using static UnityEditor.Experimental.GraphView.GraphView;

public class SlitherManager : MonoBehaviour
{
    public GameManager gameManager;
    public NetworkManager networkManager;

    Dictionary<int, SlitherContext> players = new Dictionary<int, SlitherContext>();
    public GameObject slitherPrefab; // 유니티에서 프리팹으로 연결
    public GameObject bodyPrefab;

    public float space = 0.5f;

    public void AddPlayer(int id, SlitherContext context) { players[id] = context; }
    public void RemovePlayer(int id) { players.Remove(id); }
    public SlitherContext GetPlayer(int id) { return players[id]; }

    public bool ContainsId(int id) { return players.ContainsKey(id);}

    public bool TryGetPlayer(int id, out SlitherContext context) { return players.TryGetValue(id, out context); }

    public void InitSlither(bool isLocalPlayer, PlayerJoinPayload payload)
    {
        DebugUtil.DbgTag("SEQUENCE", "[InitSlither] isLocalPlayer : ", isLocalPlayer);
        Vector2 spawnPos = new Vector2(payload.x, payload.y);
        GameObject slither = Instantiate(slitherPrefab, spawnPos, Quaternion.identity);

        Transform head = slither.transform.Find("Head");
        Transform bodyGroup = slither.transform.Find("BodyGroup");

        if (head == null || bodyGroup == null)
        {
            Debug.LogError("Slither prefab 구조가 잘못되었습니다. Head 또는 BodyGroup이 없습니다.");
            return;
        }

        SlitherContext context = new SlitherContext();
        context.Init(slither, head.gameObject, bodyGroup, payload.length, spawnPos);

        if (isLocalPlayer)
        {
            gameManager.InitPlayer(slither);
            var reporter = context.GetHeadObject().AddComponent<CollisionReporter>();
            reporter.slitherManager = this;
        }
        slither.name = $"Player_{payload.id}";
        slither.GetComponent<IdHolder>().SetId(payload.id);

        players[payload.id] = context;
    }

    void AppendSample(SlitherContext slither, Vector2 newPos)
    {
        LinkedList<(Vector2 pos, float cumLen)> path = slither.GetPath();

        if (path.Count == 0)
        {
            path.AddLast((newPos, 0f));
            slither.SetHeadPos(newPos);
            return;
        }
        var last = path.Last!.Value;
        float d = (newPos - last.pos).magnitude;
        if (d <= 0.0) return;          // 정지/노이즈면 스킵 가능

        path.AddLast((newPos, last.cumLen + d));   // 누적 길이 추가
        slither.SetHeadPos(newPos);
    }

    int TrimToPhysicalLen(SlitherContext slither, float physicalLen)
    {
        LinkedList<(Vector2 pos, float cumLen)> path = slither.GetPath();
        List<LinkedListNode<(Vector2 pos, float cumLen)>> cursors = slither.GetCursors();

        if (path.Count < 2) return 0;
        float headCumLen = path.Last!.Value.cumLen;
        float keepFrom = headCumLen - physicalLen;
        int trimmed = 0;

        // 왜 First가 아닌 First.Next와 비교를 할까?
        // -> First는 이론상 Slither의 꼬리. 꼬리를 다음으로 넘겨도 되겠다 싶을 때, 먼저 넘기고 지우기 위함.
        while (path.Count >= 2 && path.First!.Next!.Value.cumLen < keepFrom)
        {
            // 커서가 제거될 노드를 가리키면 먼저 다음으로 올려 붙임
            for (int i = 0; i < cursors.Count; i++)
                if (cursors[i] == path.First) cursors[i] = path.First.Next!;

            path.RemoveFirst();
            trimmed++;
        }
        return trimmed;

    }

    // 간격 맞추기 (i번째 세그 목표 = 헤드에서 (i+1)*spacing 뒤)
    void AdvanceCursorToTarget(SlitherContext slither, int i)
    {
        LinkedList<(Vector2 pos, float cumLen)> path = slither.GetPath();
        List<LinkedListNode<(Vector2 pos, float cumLen)>> cursors = slither.GetCursors();
        List<GameObject> bodySegments = slither.GetBodySegments();

        var lastNode = path.Last!;
        float headCumLen = lastNode.Value.cumLen;

        float targetCumLen = headCumLen - (i + 1) * space;
        if (path.First != null)
            targetCumLen = Mathf.Max(targetCumLen, path.First.Value.cumLen); // 경계 클램프

        var node = cursors[i];
        while (node != lastNode && node.Value.cumLen < targetCumLen)
            node = node.Next!;
        cursors[i] = node;

        bodySegments[i].transform.position = node.Value.pos;
    }

    public void UpdateSlitherPos(MovePayload payload)
    {
        if (!ContainsId(payload.id))
        {
            return;
        }
        Vector2 newPos = new Vector2(payload.x, payload.y);

        SlitherContext slither = players[payload.id];

        List<LinkedListNode<(Vector2 pos, float cumLen)>> cursors = slither.GetCursors();

        // 수신된 좌표 path에 저장
        AppendSample(slither, newPos);

        int bodyLen = slither.GetBodyLen(); // 몸길이
        float physicalLen = space * bodyLen;   // 간격을 포함한 slither의 물리적 총 길이

        TrimToPhysicalLen(slither, physicalLen);

        // bodySegment들의 간격을 맞춰 위치 갱신
        // 그런데 마지막 요소만 거리계산해도 되지 않을까? (개선사항)
        int cursorIndex = 0;
        while (cursorIndex < cursors.Count)
        {
            AdvanceCursorToTarget(slither, cursorIndex);

            cursorIndex++;
        }
    }

    public void DelSCtx(int id, SlitherContext sCtx)
    {
        GameObject rootObject = sCtx.GetRootObject();
        GameObject headObject = sCtx.GetHeadObject();
        List<GameObject> bodySegments = sCtx.GetBodySegments();

        Destroy(headObject);
        foreach (var segment in bodySegments)
        {
            Destroy(segment);
        }
        Destroy(rootObject);
        players.Remove(id);
    }

    public void UpdateSlitherLength(int id, int length)
    {
        DebugUtil.DbgTag("SEQUENCE", "[UpdateSlitherLength]");
        if (!ContainsId(id))
        {
            return; 
        }

        SlitherContext slither = players[id];

        slither.SetBodyLen(length);
        
        List<GameObject> bodySegments = slither.GetBodySegments();
        LinkedList<(Vector2 pos, float cumLen)> path = slither.GetPath();
        List<LinkedListNode<(Vector2 pos, float cumLen)>> cursors = slither.GetCursors();
        Transform bodyGroup = slither.GetBodyGroup();

        // 길이가 늘었을 경우
        while (bodySegments.Count < length)
        {
            var segment = Instantiate(bodyPrefab);

            var node = path.First;

            cursors.Add(node);
            Vector2 pos = node.Value.pos;

            segment.transform.position = pos;
            segment.transform.SetParent(bodyGroup);
            bodySegments.Add(segment);
        }

        // 길이가 줄었을 경우
        while (bodySegments.Count > length)
        {
            var lastSegment = bodySegments[bodySegments.Count - 1];
            GameObject.Destroy(lastSegment);
            bodySegments.RemoveAt(bodySegments.Count - 1);

            cursors.RemoveAt(cursors.Count-1);
        }
    }

    public void OnHeadTriggerEnter(Collider2D other)
    {
        DebugUtil.DbgTag("HIT", $"OnTriggerEnter2D : {other}");
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
