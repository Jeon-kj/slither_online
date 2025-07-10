using UnityEngine;

public class SlitherMove : MonoBehaviour
{
    public float moveSpeed = 5f;

    void Update()
    {
        // 마우스 월드 좌표 계산
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPosition.z = 0f;

        // 현재 위치
        Vector3 currentPosition = transform.position;

        // 방향 계산
        Vector3 direction = (mouseWorldPosition - currentPosition).normalized;

        // 이동
        transform.position += direction * moveSpeed * Time.deltaTime;

        // (선택) 방향으로 회전
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}
