using UnityEngine;

/// <summary>
/// 方糖（2D）：玩家靠近后长按 E 吸收，头顶进度条走满即吸收成功。
/// 进度会保留：松手/离开都不回退（decaySpeed 默认 0），下次回来接着吸。
/// </summary>
public class SugarCube : MonoBehaviour
{
    [Header("吸收")]
    [Tooltip("长按 E 吸收需要的总秒数")]
    public float absorbTime = 2f;

    [Tooltip("松手/离开后进度回退速度倍率。0 = 不回退（进度永久保留），>0 才会衰减")]
    public float decaySpeed = 0f;

    private float progress;
    private bool playerInRange;
    private bool absorbed;
    private Transform fillT;
    private float barWidth = 1.2f;
    private float barHeight = 0.18f;

    void Start()
    {
        GameManager.Instance.RegisterSugar(); // 统计本关方糖总数
        BuildProgressBar();
    }

    void Update()
    {
        if (absorbed) return;

        if (playerInRange && Input.GetKey(KeyCode.E))
            progress += Time.deltaTime;
        else if (decaySpeed > 0f)
            progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * decaySpeed * absorbTime);

        SetFill(progress / absorbTime);

        if (progress >= absorbTime)
            Absorb();
    }

    void Absorb()
    {
        if (absorbed) return;
        absorbed = true;
        GameManager.Instance.OnSugarAbsorbed(transform.position);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayAbsorb();
        Destroy(gameObject);
    }

    void BuildProgressBar()
    {
        // 背景条（头顶）
        ColorBlockFactory.CreateBlock("BarBG", new Vector2(0, 0.9f), new Vector2(barWidth, barHeight),
            new Color(0, 0, 0, 0.6f), transform, false, 5);

        // 填充条（左锚点，向右增长）
        var fill = ColorBlockFactory.CreateBlock("BarFill", new Vector2(-barWidth * 0.5f, 0.9f),
            new Vector2(barWidth, barHeight), Color.green, transform, false, 6, new Vector2(0f, 0.5f));
        fillT = fill.transform;
        SetFill(0f);
    }

    void SetFill(float f)
    {
        if (fillT != null)
            fillT.localScale = new Vector3(barWidth * Mathf.Clamp01(f), barHeight, 1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }
}
