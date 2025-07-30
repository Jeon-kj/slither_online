using UnityEngine;

public class IdHolder : MonoBehaviour
{
    private int id = -1;

    public void SetId(int id) {  this.id = id; }

    public int GetId() { return id; }
}
