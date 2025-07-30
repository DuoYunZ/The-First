// StaticPopup.cs
using UnityEngine;
using TMPro;

public class StaticPopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private Color textColor;

    [Header("动画参数")]
    public float moveSpeed = 2f;
    public float fadeOutSpeed = 3f;
    public float lifetime = 1.0f;

    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    // 新的Setup方法，可以接收任意文本和颜色
    public void Setup(string text, Color color)
    {
        if (textMesh != null)
        {
            textMesh.text = text;
            textColor = color;
            textMesh.color = textColor;
        }
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        textColor.a -= fadeOutSpeed * Time.deltaTime;
        if (textMesh != null)
        {
            textMesh.color = textColor;
        }
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}