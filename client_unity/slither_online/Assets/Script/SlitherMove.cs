using UnityEngine;

public class SlitherMove : MonoBehaviour
{
    Transform slither;
    public NetworkManager networkManager;
    private Camera mainCam;

    [Header("공통")]
    public float sendInterval = 0.05f; // 50ms(20fps)
    float timer;

    [Header("봇 모드")]
    public bool botMode = false;
    public float botTurnDegPerSec = 90f;   // 초당 최대 회전각(도)
    public float botDecisionInterval = 1.2f; // 방향 재결정 주기
    float botDecisionTimer;
    float botTargetHeadingRad; // 봇이 가고 싶은 목표 각(라디안)
    float botCurrHeadingRad;   // 현재 봇 각(라디안)

    void Start()
    {
        mainCam = Camera.main;
        botTargetHeadingRad = Random.value * Mathf.PI * 2f;
        botCurrHeadingRad = botTargetHeadingRad;
    }

    void Update()
    {
        if (slither == null) return;

        timer += Time.deltaTime;
        if (timer < sendInterval) return;
        timer = 0f;

        float angleToSend;

        if (!botMode)
        {
            // 사람 조작: 마우스 기준 각도
            Vector3 mouse = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mouse.z = 0f;
            Vector3 head = slither.position;
            angleToSend = Mathf.Atan2(mouse.y - head.y, mouse.x - head.x);
        }
        else
        {
            // 봇 조작: 랜덤 워크(부드럽게 회전)
            botDecisionTimer += sendInterval;
            if (botDecisionTimer >= botDecisionInterval)
            {
                botDecisionTimer = 0f;
                botDecisionInterval = Random.Range(0.8f, 1.6f);
                // 새로운 목표 각도(라디안)
                botTargetHeadingRad = Random.value * Mathf.PI * 2f;
            }

            // 현재 각도를 목표 각도 쪽으로 제한 회전
            float maxStepRad = botTurnDegPerSec * Mathf.Deg2Rad * sendInterval;
            botCurrHeadingRad = MoveAngleTowards(botCurrHeadingRad, botTargetHeadingRad, maxStepRad);

            angleToSend = botCurrHeadingRad;
        }

        networkManager.SendAngle(angleToSend);
    }

    // 라디안 각도를 부드럽게 목표로 이동(±π 래핑 처리)
    static float MoveAngleTowards(float current, float target, float maxStep)
    {
        float delta = Mathf.DeltaAngle(current * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        float step = Mathf.Clamp(delta, -maxStep, maxStep);
        return Mathf.Repeat(current + step + Mathf.PI * 2f, Mathf.PI * 2f);
    }

    public void SetSlither(Transform slitherTransform) { slither = slitherTransform; }
}
