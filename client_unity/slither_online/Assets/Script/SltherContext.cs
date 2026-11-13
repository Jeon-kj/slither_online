using System;
using System.Collections.Generic;
using UnityEngine;

public class SlitherContext
{
    private GameObject rootObject;            // Slither 전체 오브젝트 (EmptyObject)
    private GameObject headObject;            // Head 오브젝트 (rootObject의 자식)
    private Transform bodyGroup;              // BodyGroup 오브젝트 (rootObject의 자식)
    private List<GameObject> bodySegments;
    private int bodyLen;
    private Vector2 targetHeadPos;
    private float targetHeadAngle;
    private readonly List<Vector2> targetBodyPositions = new List<Vector2>();
    private const float PositionSmoothing = 20f;

    // pos : 수신받은 head의 좌표를 저장(head의 이동경로)
    // cumLen(cumulativeSum) : cumPath[i] = headPath[0]->headPath[i]까지의 누적길이
    // 꼬리->머리
    private LinkedList<(Vector2 pos, float cumLen)> path;
    // i번째 Segment가 가리키는 headPath의 index를 저장(머리 미포함)
    // 머리->꼬리
    List<LinkedListNode<(Vector2 pos, float cumLen)>> cursors;

    // 생성자
    public SlitherContext()
    {
        bodySegments = new List<GameObject>();
        path = new LinkedList<(Vector2 pos, float cumLen)>();
        cursors = new List<LinkedListNode<(Vector2 pos, float cumLen)>>();
        bodyLen = 0;
    }

    public void Init(GameObject root, GameObject head, Transform bodyGroup, int bodyLen, Vector2 spawnPos)
    {
        this.rootObject = root;
        this.headObject = head;
        this.bodyGroup = bodyGroup;
        SetBodyLen(bodyLen);
        targetHeadPos = spawnPos;
        targetHeadAngle = head.transform.eulerAngles.z;
        targetBodyPositions.Clear();

        bodySegments.Clear();
        path.Clear();
        cursors.Clear();

        var node = path.AddLast((spawnPos, 0f));

        for (int i = 0; i < bodyLen; i++)
        {
            cursors.Add(node);
        }
    }

    public void SetHeadTarget(Vector2 pos) { targetHeadPos = pos; }

    public void SetHeadAngleTarget(float angleDegrees) { targetHeadAngle = angleDegrees; }

    public void SyncBodyTargets()
    {
        while (targetBodyPositions.Count < bodySegments.Count)
            targetBodyPositions.Add(bodySegments[targetBodyPositions.Count].transform.position);
        if (targetBodyPositions.Count > bodySegments.Count)
            targetBodyPositions.RemoveRange(bodySegments.Count, targetBodyPositions.Count - bodySegments.Count);
    }

    public void SetBodyTarget(int index, Vector2 pos) { targetBodyPositions[index] = pos; }

    public void Render(float deltaTime)
    {
        float blend = 1f - Mathf.Exp(-PositionSmoothing * deltaTime);
        Vector3 previousRootPos = rootObject.transform.position;
        Vector2 headPos = Vector2.Lerp(headObject.transform.position, targetHeadPos, blend);
        rootObject.transform.position = headPos;
        headObject.transform.position = headPos;
        headObject.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.LerpAngle(headObject.transform.eulerAngles.z, targetHeadAngle, blend));

        // Moving the parent also moves its children. Remove that displacement
        // before interpolating each body's previous world position.
        Vector3 rootDelta = rootObject.transform.position - previousRootPos;
        for (int i = 0; i < bodySegments.Count; i++)
        {
            Transform segment = bodySegments[i].transform;
            Vector2 previousPos = segment.position - rootDelta;
            segment.position = Vector2.Lerp(previousPos, targetBodyPositions[i], blend);
        }
    }

    public GameObject GetRootObject() { return rootObject; }
    public GameObject GetHeadObject() { return headObject; }
    public Transform GetBodyGroup() { return bodyGroup; }
    public List<GameObject> GetBodySegments() { return bodySegments; }
    public int GetTotalLen() { return bodyLen + 1; }
    public int GetBodyLen() { return bodyLen; }
    public void SetBodyLen(int bodyLen) { this.bodyLen = bodyLen; }

    public LinkedList<(Vector2 pos, float cumLen)> GetPath() { return path; }

    public List<LinkedListNode<(Vector2 pos, float cumLen)>> GetCursors() { return cursors; }
}
