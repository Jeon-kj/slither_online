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

    Dictionary<int, SlitherContext> slithers = new Dictionary<int, SlitherContext>();
    public GameObject slitherPrefab; // 유니티에서 프리팹으로 연결
    public GameObject bodyPrefab;

    public float space = 0.5f;

    public void DisposeSlither()
    {
        slithers.Clear();
    }

    public void AddSlither(int id, SlitherContext context) { slithers[id] = context; }
    public void RemoveSlither(int id) { slithers.Remove(id); }
    public SlitherContext GetSlither(int id) { return slithers[id]; }

    public bool ContainsId(int id) { return slithers.ContainsKey(id);}

    public bool TryGetSlither(int id, out SlitherContext slither) { return slithers.TryGetValue(id, out slither); }

    // BodySegment를 length에 맞게 생성 또는 삭제
    void BuildBodySegments(SlitherContext slither, int length)
    {
        var bodySegments = slither.GetBodySegments();
        var bodyGroup = slither.GetBodyGroup();
        var path = slither.GetPath();

        while (bodySegments.Count < length)
        {
            var tail = path.First;
            Vector2 startPos = tail != null ? tail.Value.pos : slither.GetHeadObject().transform.position;

            var segment = Instantiate(bodyPrefab, startPos, Quaternion.identity);
            segment.transform.SetParent(bodyGroup, true);
            bodySegments.Add(segment);
        }

        while (bodySegments.Count > length)
        {
            var last = bodySegments[^1];    // ^1 : 뒤에서 첫번째 원소!
            Destroy(last);
            bodySegments.RemoveAt(bodySegments.Count - 1);
        }
    }

    // 커서 수 == 세그먼트 수 유지
    void SyncCursorsWithBodySegments(SlitherContext slither)
    {
        var cursors = slither.GetCursors();
        var bodySegments = slither.GetBodySegments();
        var tail = slither.GetPath().First;  // SlitherContext의 Init에서 최초 Add, 즉 존재를 전제로 함

        int need = bodySegments.Count;
        int have = cursors.Count;
        if (have < need)
        {
            for (int i = have; i < need; i++) cursors.Add(tail);
        }
        else if (have > need)
        {
            cursors.RemoveRange(need, have - need);
        }
    }

    public void InitSlither(bool isLocalPlayer, PlayerJoinPayload payload)
    {
        DebugUtil.DbgTag("SEQUENCE", "[InitSlither] isLocalPlayer : ", isLocalPlayer);
        Vector2 spawnPos = new Vector2(payload.x, payload.y);
        GameObject slitherObj = Instantiate(slitherPrefab, spawnPos, Quaternion.identity);

        Transform head = slitherObj.transform.Find("Head");
        Transform bodyGroup = slitherObj.transform.Find("BodyGroup");

        if (head == null || bodyGroup == null)
        {
            Debug.LogError("Slither prefab 구조가 잘못되었습니다. Head 또는 BodyGroup이 없습니다.");
            return;
        }

        SlitherContext context = new SlitherContext();
        
        context.Init(slitherObj, head.gameObject, bodyGroup, payload.length, spawnPos);
        BuildBodySegments(context, payload.length);
        SyncCursorsWithBodySegments(context);

        if (isLocalPlayer)
        {
            gameManager.InitPlayer(slitherObj);
            var reporter = context.GetHeadObject().AddComponent<CollisionReporter>();
            reporter.slitherManager = this;
        }
        slitherObj.name = $"Player_{payload.id}";
        slitherObj.GetComponent<IdHolder>().SetId(payload.id);

        AddSlither(payload.id, context);
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
        if (!ContainsId(payload.id)) return;

        Vector2 newPos = new Vector2(payload.x, payload.y);
        SlitherContext slither = GetSlither(payload.id);

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

    public void DelSCtx(int id, SlitherContext slither)
    {
        GameObject rootObject = slither.GetRootObject();
        GameObject headObject = slither.GetHeadObject();
        List<GameObject> bodySegments = slither.GetBodySegments();

        Destroy(headObject);
        foreach (var segment in bodySegments)
        {
            Destroy(segment);
        }
        Destroy(rootObject);
        RemoveSlither(id);
    }

    public void UpdateSlitherLength(int id, int length)
    {
        DebugUtil.DbgTag("SEQUENCE", "[UpdateSlitherLength]");
        if (!TryGetSlither(id, out var slither)) return;

        slither.SetBodyLen(length);
        BuildBodySegments(slither, length);
        SyncCursorsWithBodySegments(slither);
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
            Vector2 hitPos = other.ClosestPoint(transform.position);

            if (myId == targetId)
                return; // 내 body와 충돌 → 무시

            networkManager.SendHit(targetId, hitPos);
        }
    }
}
