using UnityEngine;

/// <summary>
/// 让正交相机平滑跟随目标（2D 俯视，相机在 Z 轴 -10 处）。
/// 支持轻微屏幕震动（攻击命中时调用 Shake）。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance { get; private set; }

    [Tooltip("跟随目标（把场景里的 Player 拖进来）")]
    public Transform target;

    [Tooltip("跟随平滑度（越大越跟得紧）")]
    public float smoothSpeed = 8f;

    [Tooltip("相机相对目标的偏移（Z 轴保持 -10 俯视）")]
    public Vector3 offset = new Vector3(0, 0, -10f);

    [Header("背景")]
    [Tooltip("地图外背景色（默认深红，运行时可在 Inspector 拖实时看效果）")]
    public Color backgroundColor = new Color(0.42f, 0.05f, 0.06f, 1f);

    private Camera cam;
    private float shakeTime;
    private float shakePower;
    private Coroutine hitStopCo;
    private bool hitStopping;
    private float prevTimeScale = 1f;

    void Awake() { Instance = this; cam = GetComponent<Camera>(); }

    void LateUpdate()
    {
        if (cam != null) cam.backgroundColor = backgroundColor;
        if (target == null) return;
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);

        // 屏幕震动（攻击命中时触发，随剩余时间衰减）
        if (shakeTime > 0f)
        {
            shakeTime -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(shakeTime / 0.25f);
            Vector2 r = Random.insideUnitCircle * (shakePower * t);
            transform.position += new Vector3(r.x, r.y, 0f);
        }
    }

    /// <summary>触发屏幕震动。amount=强度（世界单位），duration=持续时间（秒）。</summary>
    public void Shake(float amount, float duration)
    {
        shakePower = Mathf.Max(shakePower, amount);
        shakeTime = Mathf.Max(shakeTime, duration);
    }

    /// <summary>切换环境氛围（最终抉择 GROW/NO）：直接改背景色，LateUpdate 每帧自动应用。</summary>
    public void SetAmbience(Color c)
    {
        backgroundColor = c;
    }

    /// <summary>打击顿帧：把时间暂停一小段时间（命中瞬间的「停顿感」）。短时间多次调用只会延长、不会叠加。</summary>
    public void HitStop(float duration)
    {
        // 防御：如果当前本来就是 0（对话中 / 选关暂停 / 结算中），顿帧直接跳过——
        // 否则它会把 Time.timeScale 又抢一遍，覆盖对话系统保存的 prevTimeScale，导致对话结束后游戏被永久定格在 0。
        if (Time.timeScale <= 0f) return;
        if (!hitStopping)
        {
            hitStopping = true;
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        if (hitStopCo != null) StopCoroutine(hitStopCo);
        hitStopCo = StartCoroutine(HitStopRoutine(duration));
    }

    System.Collections.IEnumerator HitStopRoutine(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = prevTimeScale;
        hitStopping = false;
        hitStopCo = null;
    }
}
