using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private Color textColor;

    public float moveSpeed = 2f;    // 向上漂浮的速度
    public float fadeOutSpeed = 3f; // 淡出速度
    public float lifetime = 0.7f;   // 存活时间

    [Header("颜色设置")]
    public Color healthDamageColor = Color.white; // 默认生命伤害颜色
    public Color shieldDamageColor = new Color(0.5f, 0.8f, 1f); // 默认护盾伤害颜色 (淡蓝色)

    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    /// <summary>
    /// 设置跳字显示的伤害数值
    /// </summary>
    public void Setup(int damageAmount, bool isShieldDamage)
    {
        if (textMesh != null)
        {
            textMesh.text = damageAmount.ToString();
            // 根据 isShieldDamage 参数选择颜色
            textColor = isShieldDamage ? shieldDamageColor : healthDamageColor;
            textMesh.color = textColor;
        }
        Destroy(gameObject, lifetime);
    }

    public void Setup(int damageAmount)
    {
        Setup(damageAmount, false); // 默认调用新方法，并标记为非护盾伤害
    }

    void Update()
    {
        // 向上移动
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // 淡出效果
        textColor.a -= fadeOutSpeed * Time.deltaTime;
        if (textMesh != null)
        {
            textMesh.color = textColor;
        }

        // 让文本始终朝向主摄像机
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}