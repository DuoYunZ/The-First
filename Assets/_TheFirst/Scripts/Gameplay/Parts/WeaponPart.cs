using UnityEngine;

public class WeaponPart : MonoBehaviour
{
    [Header("��������")]
    [Tooltip("�ӵ������ͳ�ʼ����")]
    public Transform firePoint; // �� Inspector �н����洴���� FirePoint ��ק������
    [Tooltip("Ҫ������ӵ�Ԥ��")]
    public GameObject projectilePrefab; // �� Inspector �н� Bullet Prefab ��ק������

    [Tooltip("���� (ÿ�뷢�����)")]
    public float fireRate = 2f;
    [Tooltip("�ӵ��ٶ�")]
    public float projectileSpeed = 20f;
    [Tooltip("�ӵ����ʱ��")]
    public float projectileLifetime = 3f;

    // (��������˺�����Ч����������)
    // public int damage = 10;
    // public GameObject muzzleFlashEffect;
    // public AudioClip fireSound;

    // --- �ڲ���ʱ�� ---
    private float fireCooldown = 0f;
    public bool IsReadyToFire => fireCooldown <= 0f; // ����һ�������ж��Ƿ���ȴ���

    void Update()
    {
        // ÿ֡������ȴʱ��
        if (fireCooldown > 0f)
        {
            fireCooldown -= Time.deltaTime;
        }
    }

    // ���ⲿ WeaponController ����
    public void Fire(Vector3 targetDirection) // ����������ռ���������
    {
        if (!IsReadyToFire || firePoint == null || projectilePrefab == null)
        {
            return; // ��ȴ�л�ȱ�ٱ�Ҫ����򲻷���
        }

        Debug.Log($"���� {gameObject.name} ����!");

        // --- ������ת��λ�� ---
        // ���ӵ�����Ŀ�귽�� (���� firePoint ����� Z �ᳯ��ֱ����Ŀ�귽��)
        Quaternion projectileRotation = Quaternion.LookRotation(targetDirection);
        // �� firePoint ��λ�������ӵ�
        Vector3 spawnPosition = firePoint.position;

        // --- ʵ�����ӵ� ---
        GameObject bullet = Instantiate(projectilePrefab, spawnPosition, projectileRotation);



        // --- �����ӵ����� ---
        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            projectileScript.speed = this.projectileSpeed;
            projectileScript.lifetime = this.projectileLifetime;
            // �ӵ��ű�����ʹ������� forward ������У������Ѿ����ú�����ת
            projectileScript.direction = bullet.transform.forward; // ����ֱ���� targetDirection Ҳ����
        }
        else
        {
            Debug.LogError("�ӵ�Ԥ����û���ҵ� Projectile �ű�!", bullet);
        }

        // --- ������ȴ ---
        fireCooldown = 1f / fireRate;

        // --- (��ѡ) ������Ч������ ---
        // if (muzzleFlashEffect != null) Instantiate(muzzleFlashEffect, firePoint.position, firePoint.rotation);
        // if (fireSound != null) AudioSource.PlayClipAtPoint(fireSound, firePoint.position);
    }

    // ���������ʱ���ã���ȴ�� WeaponController ���������
    // public void RequestFire(Vector3 targetDirection) {
    //      if (IsReadyToFire) {
    //           Fire(targetDirection);
    //      }
    // }
}