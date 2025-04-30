using UnityEngine;
using UnityEngine.UI; // ��Ҫ���� UI �����ռ�

[RequireComponent(typeof(Slider))] // ȷ���ű����صĶ����� Slider ���
public class PlayerHealthUI : MonoBehaviour
{
    private Slider healthSlider;
    private Health playerHealth; // ��� Health ���������

    void Awake()
    {
        healthSlider = GetComponent<Slider>(); // ��ȡͬһ�������ϵ� Slider ���
    }

    void Update()
    {
        // --- ����������� Health ��� (ֱ���ҵ�) ---
        // ��Ϊ��� MechRoot ��������Ϸ��ʼ��ż���
        if (playerHealth == null)
        {
            // ����ͨ�� GameManager ��ȡ (��� GameManager ������������Ҵ���)
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                playerHealth = GameManager.Instance.playerTransform.GetComponent<Health>();
                if (playerHealth != null)
                {
                    InitializeSlider(); // �ҵ����ʼ�� Slider
                }
            }
            // ���û�ҵ�����һ֡������...
            if (playerHealth == null) return; // ��֡������ UI
        }
        // ------------------------------------------

        // --- ����ҵ������ Health������� Slider ֵ ---
        if (healthSlider != null) // ȷ�� Slider ����
        {
            // ƽ�����»�ֱ�Ӹ���
            healthSlider.value = playerHealth.GetCurrentHealth();
            // ���ߴ���ƽ��Ч��:
            // healthSlider.value = Mathf.Lerp(healthSlider.value, playerHealth.GetCurrentHealth(), Time.deltaTime * 10f);
        }
    }

    // ��ʼ�� Slider ���ֵ������
    void InitializeSlider()
    {
        if (healthSlider != null && playerHealth != null)
        {
            healthSlider.maxValue = playerHealth.GetMaxHealth();
            healthSlider.value = playerHealth.GetCurrentHealth(); // ���ó�ʼѪ��
            Debug.Log("PlayerHealthUI Initialized. MaxHealth: " + healthSlider.maxValue);
        }
    }

    // (��ѡ) ����Ҷ�������ʱ��������Ҫ���ػ��� Slider
    // ����ͨ�� Health �� OnDeath �¼�������
}