using UnityEngine;

// 스프링(감쇠) 계산 도우미
// 값이 목표를 향해 "출렁이지 않고 부드럽게" 따라가게 함 (임계 감쇠 스프링)
// 정확한 공식(지수 함수)을 써서 30fps든 144fps든 똑같이 움직임
//   speed : 클수록 빨리 따라감
//   SpeedFromTime(0.2f) : "0.2초 안에 거의(95%) 도착" 하는 speed
public static class FeelSpring
{
    // 시간(초) → speed. (임계 감쇠 스프링은 speed × 시간 ≈ 4.74 일 때 95% 도착)
    public static float SpeedFromTime(float seconds) => 4.74f / Mathf.Max(seconds, 0.001f);

    // value를 goal 쪽으로 dt초만큼 움직임. velocity는 따로 들고 있다가 매번 넘겨줘야 함
    public static float Step(float value, ref float velocity, float goal, float speed, float dt)
    {
        if (dt <= 0f) return value;
        float j0 = value - goal;                 // 목표까지 남은 거리
        float j1 = velocity + j0 * speed;
        float e = Mathf.Exp(-speed * dt);
        velocity = e * (velocity - j1 * speed * dt);
        return e * (j0 + j1 * dt) + goal;
    }

    // 통통 튀는 스프링: 목표를 살짝 넘쳤다가 돌아옴
    //   frequency : 1초에 몇 번 출렁이는지 (클수록 빠릿빠릿)
    //   damping   : 0 ~ 1. 작을수록 더 많이 출렁, 1이면 안 넘침
    // 한 프레임을 1/600초 조각으로 잘라서 계산 → 프레임레이트가 달라도 거의 같은 움직임
    public static Vector3 Bouncy(Vector3 value, ref Vector3 velocity, Vector3 goal, float frequency, float damping, float dt)
    {
        if (dt <= 0f) return value;
        dt = Mathf.Min(dt, 0.1f);   // 렉이 크게 걸려도 튀지 않게
        float w = 2f * Mathf.PI * Mathf.Max(frequency, 0.01f);
        int n = Mathf.CeilToInt(dt * 600f);
        float h = dt / n;
        for (int i = 0; i < n; i++)
        {
            Vector3 accel = w * w * (goal - value) - 2f * damping * w * velocity;
            velocity += accel * h;
            value += velocity * h;
        }
        return value;
    }

    // Vector3 버전 (x, y, z 각각 따로 계산)
    public static Vector3 Step(Vector3 value, ref Vector3 velocity, Vector3 goal, float speed, float dt)
    {
        float vx = velocity.x, vy = velocity.y, vz = velocity.z;
        value.x = Step(value.x, ref vx, goal.x, speed, dt);
        value.y = Step(value.y, ref vy, goal.y, speed, dt);
        value.z = Step(value.z, ref vz, goal.z, speed, dt);
        velocity = new Vector3(vx, vy, vz);
        return value;
    }
}

// 충격 스프링: 맞는 순간 정한 크기(peak)만큼 튀었다가, 살짝 출렁이며 0으로 돌아옴
//   Hit(위치 peak, 회전 peak) → 처음 튀는 최대값이 딱 peak가 되도록 속도를 계산해서 넣음
//   이동 중에는 안 쓰고 "충격 순간"에만 씀
public struct FeelImpact
{
    private Vector3 pos, posVel, rot, rotVel;

    public Vector3 Position => pos;
    public Vector3 Rotation => rot;
    public bool IsZero => pos == Vector3.zero && rot == Vector3.zero && posVel == Vector3.zero && rotVel == Vector3.zero;

    public void Hit(Vector3 peakPos, Vector3 peakRot, float frequency, float damping)
    {
        // 감쇠 진동의 첫 최대값 = 처음속도 / ω × e^(-ζωt). 거꾸로 풀어서 처음속도를 구함
        float w = 2f * Mathf.PI * Mathf.Max(frequency, 0.01f);
        float z = Mathf.Clamp(damping, 0.05f, 0.95f);
        float s = Mathf.Sqrt(1f - z * z);
        float k = w * Mathf.Exp(z / s * Mathf.Atan2(s, z));
        posVel += peakPos * k;
        rotVel += peakRot * k;
    }

    public void Step(float frequency, float damping, float dt)
    {
        if (dt <= 0f || IsZero) return;
        pos = FeelSpring.Bouncy(pos, ref posVel, Vector3.zero, frequency, damping, dt);
        rot = FeelSpring.Bouncy(rot, ref rotVel, Vector3.zero, frequency, damping, dt);
        if (pos.sqrMagnitude < 1e-10f && posVel.sqrMagnitude < 1e-8f && rot.sqrMagnitude < 1e-6f && rotVel.sqrMagnitude < 1e-4f)
            Reset();   // 눈에 안 보일 만큼 작아지면 정확히 0
    }

    public void Reset()
    {
        pos = posVel = rot = rotVel = Vector3.zero;
    }
}

// 반동처럼 "확 튀었다가 천천히 돌아오는" 값
//   Add(튀는 양)  → riseTime 동안 빠르게 튀고 → returnTime 동안 0으로 돌아옴
//   거의 0이 되면 정확히 0으로 맞춰 줌 → 반동이 끝나면 원래 자리로 딱 돌아옴
public struct FeelKick
{
    private Vector3 target, targetVel;   // 튀어야 할 자리 (천천히 0으로 돌아감)
    private Vector3 value, valueVel;     // 실제 값 (target을 빠르게 따라감)
    private float sinceKick;             // 마지막으로 튄 뒤 지난 시간 (튀는 동안은 복귀를 안 함 → 정한 만큼 다 튐)

    public Vector3 Value => value;
    public bool IsZero => value == Vector3.zero && target == Vector3.zero;

    // limit: 여러 번 연달아 맞아도 이 이상은 안 튐 (각 칸의 절댓값)
    public void Add(Vector3 kick, Vector3 limit)
    {
        target += kick;
        target.x = Mathf.Clamp(target.x, -Mathf.Abs(limit.x), Mathf.Abs(limit.x));
        target.y = Mathf.Clamp(target.y, -Mathf.Abs(limit.y), Mathf.Abs(limit.y));
        target.z = Mathf.Clamp(target.z, -Mathf.Abs(limit.z), Mathf.Abs(limit.z));
        sinceKick = 0f;
    }

    public void Step(float riseTime, float returnTime, float dt)
    {
        if (dt <= 0f || IsZero) return;
        if (sinceKick < riseTime) sinceKick += dt;   // 튀어 오르는 중: 목표는 그대로
        else target = FeelSpring.Step(target, ref targetVel, Vector3.zero, FeelSpring.SpeedFromTime(returnTime), dt);
        value = FeelSpring.Step(value, ref valueVel, target, FeelSpring.SpeedFromTime(riseTime), dt);

        // 눈에 안 보일 만큼(0.0001) 작아지면 정확히 0으로
        const float tiny = 0.0001f;
        if (value.sqrMagnitude < tiny * tiny && target.sqrMagnitude < tiny * tiny &&
            valueVel.sqrMagnitude < 0.0001f && targetVel.sqrMagnitude < 0.0001f)
            Reset();
    }

    public void Reset()
    {
        target = targetVel = value = valueVel = Vector3.zero;
        sinceKick = 0f;
    }
}
