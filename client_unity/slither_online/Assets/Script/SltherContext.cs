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

        bodySegments.Clear();
        path.Clear();
        cursors.Clear();

        var node = path.AddLast((spawnPos, 0f));

        for (int i = 0; i < bodyLen; i++)
        {
            cursors.Add(node);
        }
    }

    public void SetHeadPos(Vector2 pos)
    {
        rootObject.transform.position = pos;
        headObject.transform.position = pos;
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
