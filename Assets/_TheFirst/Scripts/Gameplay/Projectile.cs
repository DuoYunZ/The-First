using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 20f;     // �ӵ������ٶ�
    public float lifetime = 3f;     // �ӵ����ʱ��
    public Vector3 direction = Vector3.forward; // �ӵ����з��� (����ռ�)

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
        // ����Ƿ���ײ���˵��� (����ͨ�� Tag �� Layer)
        if (other.CompareTag("Enemy")) // ������� Tag Ϊ "Enemy"
        {
            Debug.Log($"�ӵ������˵���: {other.name}");
            // �����ﴦ���˺��߼�...
            // ���ٵ���? other.GetComponent<EnemyHealth>()?.TakeDamage(10);
            Destroy(gameObject); // ���к������ӵ�
        }
        // ������Ӷ�ǽ�ڻ������ϰ���Ĵ���
        else if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }

    // ��� Collider �� Is Trigger û�й�ѡ (������ײ):
    /*
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Debug.Log($"�ӵ������˵���: {collision.gameObject.name}");
            // �����˺�...
            Destroy(gameObject);
        }
        else if (collision.gameObject.CompareTag("Wall")) {
             Destroy(gameObject);
        }
    }
    */
}