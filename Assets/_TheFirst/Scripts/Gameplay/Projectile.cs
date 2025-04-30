using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 20f;     // �ӵ������ٶ�
    public float lifetime = 3f;     // �ӵ����ʱ��
    public Vector3 direction = Vector3.forward; // �ӵ����з��� (����ռ�)
    public int damage = 10; // *** �������ӵ��˺�ֵ ***

    void Start()
    {
        // ��ָ��ʱ����Զ������ӵ� GameObject
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // ÿ֡����ָ�������ƶ�
        // ʹ�� Space.World ȷ��������ռ䷽���ƶ�
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    // --- ��ײ/�������� (δ�����) ---
    // ��� Collider �� Is Trigger ��ѡ��:
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Projectile OnTriggerEnter with: {other.name} (Tag: {other.tag})"); // 1. ��¼��ײ������Ϣ

        if (other.CompareTag("Enemy")) // 2. ��� Tag �Ƿ�ƥ��
        {
            Debug.Log($"Tag 'Enemy' MATCHED on {other.name}!"); // 3. ȷ�� Tag ƥ��ɹ�

            // --- �޸���������� GetComponentInParent ---
            // Health enemyHealth = other.GetComponent<Health>(); // ԭ���Ĵ���
            Health enemyHealth = other.GetComponentInParent<Health>(); // �������ϲ��� Health
                                                                       // --------------------------------------

            if (enemyHealth != null) // 4. ����Ƿ��ҵ��� Health ���
            {
                Debug.Log($"Health component FOUND on {other.name} or its parent. Attempting TakeDamage({damage})."); // 5. ȷ���ҵ� Health
                enemyHealth.TakeDamage(damage); // 6. ���� TakeDamage
                Debug.Log($"Called TakeDamage on {other.name}."); // 7. ȷ�ϵ������
            }
            else
            {
                // 8. ���û�ҵ� Health �������ӡ��ȷ����
                Debug.LogError($"Health component NOT FOUND on {other.name} or its parents!", other.gameObject);
            }

            // �����ӵ�
            Debug.Log($"Destroying projectile {this.gameObject.name}"); // 9. ȷ�������ӵ�
            Destroy(gameObject);
        }
        else if (other.CompareTag("Wall"))
        {
            Debug.Log($"Projectile hit Wall: {other.name}. Destroying projectile.");
            Destroy(gameObject);
        }
        // ������� else ��֧���鿴ײ��������ʲô����
        // else { Debug.Log($"Projectile hit something else: {other.name} (Tag: {other.tag})"); }
    }


    // ��������ײ�岻�� Trigger�����޸� OnCollisionEnter
    /*
    void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("Enemy")) {
            Health enemyHealth = collision.gameObject.GetComponent<Health>();
            if (enemyHealth != null) {
                enemyHealth.TakeDamage(damage);
            }
            Destroy(gameObject);
        } else if (collision.gameObject.CompareTag("Wall")) {
             Destroy(gameObject);
        }
    }
    */
}
