using System.Collections.Generic;
using UnityEngine;

public class SlitherContext
{
    public GameObject rootObject;            // Slither 전체 오브젝트 (EmptyObject)
    public GameObject headObject;            // Head 오브젝트 (rootObject의 자식)
    public Transform bodyGroup;              // BodyGroup 오브젝트 (rootObject의 자식)
    public List<GameObject> bodySegments = new List<GameObject>();
    public int bodyLen = 0;
}
