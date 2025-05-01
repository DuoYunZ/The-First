using UnityEngine;
using UnityEngine.UI; // ���� Slider
using TMPro; // ���� TextMeshPro

public class PlayerXPUI : MonoBehaviour // ���齫�˽ű������� CombatUIContainer ��һ��ר�ŵ� PlayerStatsUI ������
{
    [Header("UI ���� (�� Inspector ��ָ��)")]
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("�������Դ")]
    [Tooltip("����ѡ���ֶ�ָ����� Level Manager�����Ϊ�գ����Զ����ҡ�")]
    [SerializeField] private PlayerLevelManager levelManager;

    private bool isInitialized = false;

    void Update()
    {
        // ��� levelManager ��δ�ҵ����ʼ�������Բ���
        if (levelManager == null)
        {
            // ����ͨ�� GameManager ��ȡ (�Ƽ�)
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                levelManager = GameManager.Instance.playerTransform.GetComponent<PlayerLevelManager>();
                if (levelManager != null)
                {
                    Debug.Log("PlayerXPUI found PlayerLevelManager via GameManager.");
                }
            }
            // ����ȫ�ֲ��� (Ч�ʽϵ�)
            // levelManager = FindObjectOfType<PlayerLevelManager>();

            if (levelManager == null)
            {
                // Debug.LogWarning("PlayerXPUI waiting for PlayerLevelManager...");
                return; // �������û�ҵ����ȴ���һ֡
            }
        }

        // ����ҵ��� Level Manager������ UI
        if (xpSlider != null)
        {
            // ȷ�� maxValue ���� 0������������
            int xpToNext = levelManager.GetXPToNextLevel();
            xpSlider.maxValue = xpToNext > 0 ? xpToNext : 1; // ��ֹΪ 0
            xpSlider.value = levelManager.GetCurrentXP();
        }

        if (levelText != null)
        {
            levelText.text = "Level: " + levelManager.GetLevel();
        }
    }
}