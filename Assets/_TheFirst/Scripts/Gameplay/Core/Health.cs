using UnityEngine;
using UnityEngine.Events; // ���������¼�

public class Health : MonoBehaviour
{
    [Header("����ֵ����")]
    [SerializeField] private int maxHealth = 100; // �������ֵ (������ Inspector ���޸�)
    private int currentHealth; // ��ǰ����ֵ

    [Header("�����¼�")]
    [Tooltip("������ֵ����ʱ�������¼� (������ Inspector �й�����Ч���ɡ��������š��������ӵ�)")]
    public UnityEvent OnDeath; // ����ʱ�������¼�

    public bool IsDead => currentHealth <= 0; // �ж��Ƿ�������

    void Awake() // ʹ�� Awake ȷ���� Start ǰ��ʼ��
    {
        currentHealth = maxHealth; // ��Ϸ��ʼʱ����Ϊ��Ѫ
    }

    /// <summary>
    /// �Դ���������˺���
    /// </summary>
    /// <param name="damageAmount">��ɵ��˺�ֵ</param>
    public void TakeDamage(int damageAmount)
    {
        if (IsDead) return; // ����Ѿ����������ٽ����˺�

        currentHealth -= damageAmount;
        Debug.Log($"{gameObject.name} �ܵ� {damageAmount} ���˺�, ʣ������: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            currentHealth = 0; // ��ֹ����
            Die(); // ִ�������߼�
        }
        // (��ѡ) ��������Դ��������¼�������������Ч����ʾ�˺����ֵ�
        // OnTakeDamage?.Invoke(damageAmount);
    }

    /// <summary>
    /// �����߼���
    /// </summary>
    private void Die()
    {
        Debug.Log($"{gameObject.name} �ѱ��ݻ�!");
        OnDeath?.Invoke();

        // --- ��Ӽ򵥵��������� ---
        // ����: ʵ����һ����ը��ЧԤ��
        // public GameObject deathEffectPrefab; // ��Ҫ�� Health �ű��������������� Inspector ָ��
        // if (deathEffectPrefab != null) Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        // ����: ����������Ч
        // public AudioClip deathSound; // ��Ҫ��� AudioSource ������������
        // AudioSource audioSource = GetComponent<AudioSource>();
        // if (audioSource != null && deathSound != null) audioSource.PlayOneShot(deathSound);
        // -----------------------

        Destroy(gameObject);
    }

    // (��ѡ) �ṩ��ȡ��ǰ/�������ֵ�ķ���
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
}