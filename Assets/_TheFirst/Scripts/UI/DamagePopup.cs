using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float disappearTimer;
    private Color textColor;
    private Vector3 moveVector;
    private bool isCritical;
    private float initialScale;

    [Header("基础设置")]
    public float lifetime = 1f;

    [Header("颜色与大小配置")]
    public Color normalColor = Color.white;
    public float normalFontSize = 6f;
    [Space(5)]
    public Color critColor = new Color(1f, 0.6f, 0f); // 金色
    public float critFontSize = 10f;

    [Header("动画配置")]
    public float normalUpSpeed = 1f;
    public float critUpThrust = 5f;
    public float critSideSpread = 1f;
    public float drag = 1.5f;

    [Header("暴击缩放特效")]
    public float scalePunchAmount = 1.3f;
    public float scalePunchDuration = 0.2f;
    private float scaleTimer;

    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        initialScale = transform.localScale.x;
    }

    // 🔥【关键修改】改名为 InitPopup，避免与旧代码混淆
    public void InitPopup(int damageAmount, bool isCrit)
    {
        this.isCritical = isCrit;

        // 确保 TextMesh 组件存在
        if (textMesh == null) textMesh = GetComponent<TextMeshPro>();

        textMesh.text = damageAmount.ToString();
        disappearTimer = lifetime;
        
        if (isCrit)
        {
            textMesh.fontSize = critFontSize;
            textColor = critColor;
            textMesh.color = textColor;

            // 暴击运动：向上冲 + 随机左右
            moveVector = new Vector3(Random.Range(-critSideSpread, critSideSpread), critUpThrust, 0f);
            scaleTimer = scalePunchDuration;
        }
        else
        {
            textMesh.fontSize = normalFontSize;
            textColor = normalColor;
            textMesh.color = textColor;

            // 普通运动：慢速向上
            moveVector = new Vector3(0, normalUpSpeed, 0f);
            scaleTimer = 0f;
        }

        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }

    void Update()
    {
        transform.position += moveVector * Time.deltaTime;
        moveVector -= moveVector * drag * Time.deltaTime;

        if (isCritical && scaleTimer > 0)
        {
            scaleTimer -= Time.deltaTime;
            float scaleLerp = scaleTimer / scalePunchDuration;
            float currentScale = Mathf.Lerp(initialScale, initialScale * scalePunchAmount, scaleLerp);
            transform.localScale = Vector3.one * currentScale;
        }
        else
        {
            transform.localScale = Vector3.one * initialScale;
        }

        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            float fadeSpeed = 3f;
            textColor.a -= fadeSpeed * Time.deltaTime;
            textMesh.color = textColor;
            if (textColor.a < 0) Destroy(gameObject);
        }
    }
}