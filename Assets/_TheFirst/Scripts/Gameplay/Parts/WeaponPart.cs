using UnityEngine;

public class WeaponPart : MonoBehaviour
{
    [Header("武器属性")]
    [Tooltip("子弹发射点和初始方向")]
    public Transform firePoint; // 在 Inspector 中将上面创建的 FirePoint 拖拽到这里
    [Tooltip("要发射的子弹预设")]
    public GameObject projectilePrefab; // 在 Inspector 中将 Bullet Prefab 拖拽到这里

    [Tooltip("射速 (每秒发射次数)")]
    public float fireRate = 2f;
    [Tooltip("子弹速度")]
    public float projectileSpeed = 20f;
    [Tooltip("子弹存活时间")]
    public float projectileLifetime = 3f;

    // (可以添加伤害、特效等其他属性)
    // public int damage = 10;
    // public GameObject muzzleFlashEffect;
    // public AudioClip fireSound;

    // --- 内部计时器 ---
    private float fireCooldown = 0f;
    public bool IsReadyToFire => fireCooldown <= 0f; // 公开一个属性判断是否冷却完毕

    void Update()
    {
        // 每帧减少冷却时间
        if (fireCooldown > 0f)
        {
            fireCooldown -= Time.deltaTime;
        }
    }

    // 由外部 WeaponController 调用
    public void Fire(Vector3 targetDirection) // 参数是世界空间的射击方向
    {
        if (!IsReadyToFire || firePoint == null || projectilePrefab == null)
        {
            return; // 冷却中或缺少必要组件则不发射
        }

        Debug.Log($"武器 {gameObject.name} 开火!");

        // --- 计算旋转和位置 ---
        // 让子弹朝向目标方向 (忽略 firePoint 自身的 Z 轴朝向，直接用目标方向)
        Quaternion projectileRotation = Quaternion.LookRotation(targetDirection);
        // 在 firePoint 的位置生成子弹
        Vector3 spawnPosition = firePoint.position;

        // --- 实例化子弹 ---
        GameObject bullet = Instantiate(projectilePrefab, spawnPosition, projectileRotation);



        // --- 设置子弹属性 ---
        Projectile projectileScript = bullet.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            projectileScript.speed = this.projectileSpeed;
            projectileScript.lifetime = this.projectileLifetime;
            // 子弹脚本现在使用自身的 forward 方向飞行，我们已经设置好了旋转
            projectileScript.direction = bullet.transform.forward; // 或者直接用 targetDirection 也可以
        }
        else
        {
            Debug.LogError("子弹预设上没有找到 Projectile 脚本!", bullet);
        }

        // --- 重置冷却 ---
        fireCooldown = 1f / fireRate;

        // --- (可选) 播放特效和声音 ---
        // if (muzzleFlashEffect != null) Instantiate(muzzleFlashEffect, firePoint.position, firePoint.rotation);
        // if (fireSound != null) AudioSource.PlayClipAtPoint(fireSound, firePoint.position);
    }

    // 这个方法暂时不用，冷却由 WeaponController 管理更方便
    // public void RequestFire(Vector3 targetDirection) {
    //      if (IsReadyToFire) {
    //           Fire(targetDirection);
    //      }
    // }
}