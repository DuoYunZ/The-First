using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 大招管理器 - 管理能量状态、释放大招与连携技
/// </summary>
public class UltimateManager : MonoBehaviour
{
    public static UltimateManager Instance { get; private set; }

    [Header("连携技配置")]
    [Tooltip("所有连携技配置列表")]
    public List<SO_ComboUltimate> comboUltimates = new List<SO_ComboUltimate>();

    [Header("释放设置")]
    [Tooltip("大招释放的伤害层")]
    public LayerMask damageableLayers;

    // 当前滚轮选中的主武器索引
    private int selectedWeaponIndex = 0;
    // 缓存能量满的武器列表
    private List<WeaponPart> fullyChargedWeapons = new List<WeaponPart>();

    // 事件：大招释放时通知UI
    public System.Action<string> OnUltimateReleased;
    // 事件：能量满武器列表变化时
    public System.Action<List<WeaponPart>> OnChargedWeaponsChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// 尝试释放大招（由输入系统调用，Space键）
    /// </summary>
    public void TryReleaseUltimate()
    {
        if (UltimateHUD.Instance == null) return;
        List<WeaponPart> queue = UltimateHUD.Instance.GetQueue();

        if (queue.Count == 0)
        {
            Debug.Log("[大招] 队列为空，无法释放");
            return;
        }

        // 检查队列前两个是否能组成连携
        if (queue.Count >= 2)
        {
            SO_ComboUltimate combo = FindComboForPair(queue[0], queue[1]);
            if (combo != null)
            {
                ReleaseComboUltimate(combo, queue[0], queue[1]);
                return;
            }
        }

        // 没有连携，释放队列第一个
        ReleaseSingleUltimate(queue[0]);
    }

    /// <summary>
    /// 滚轮切换主武器
    /// </summary>
    public void ScrollSelectWeapon(float scrollDelta)
    {
        RefreshChargedWeapons();
        if (fullyChargedWeapons.Count <= 1) return;

        if (scrollDelta > 0)
            selectedWeaponIndex++;
        else if (scrollDelta < 0)
            selectedWeaponIndex--;

        // 循环
        selectedWeaponIndex = ((selectedWeaponIndex % fullyChargedWeapons.Count) + fullyChargedWeapons.Count) % fullyChargedWeapons.Count;

        Debug.Log($"[大招] 切换主武器: {fullyChargedWeapons[selectedWeaponIndex].StatBlock.weaponName}");
    }

    // --- 内部方法 ---

    private void RefreshChargedWeapons()
    {
        fullyChargedWeapons.Clear();
        var controller = WeaponController.Instance;
        if (controller == null) return;

        // 检查内置武器
        if (controller.builtInBladeWeapon != null && controller.builtInBladeWeapon.IsEnergyFull)
        {
            fullyChargedWeapons.Add(controller.builtInBladeWeapon);
        }

        // 检查所有拥有的武器
        foreach (var owned in controller.ownedWeapons)
        {
            if (owned.weaponPartInstance != null && owned.weaponPartInstance.IsEnergyFull)
            {
                fullyChargedWeapons.Add(owned.weaponPartInstance);
            }
        }

        // 安全边界
        if (selectedWeaponIndex >= fullyChargedWeapons.Count)
            selectedWeaponIndex = 0;

        OnChargedWeaponsChanged?.Invoke(fullyChargedWeapons);
    }

    /// <summary>
    /// 检查两个武器是否能组成连携
    /// </summary>
    private SO_ComboUltimate FindComboForPair(WeaponPart a, WeaponPart b)
    {
        if (a == null || b == null) return null;
        foreach (var combo in comboUltimates)
        {
            if (combo != null && combo.MatchesWeapons(a.StatBlock, b.StatBlock))
                return combo;
        }
        return null;
    }

    private void ReleaseSingleUltimate(WeaponPart weapon)
    {
        Debug.Log($"<color=orange>[大招] 释放 {weapon.StatBlock.weaponName} 的大招！</color>");

        // 生成大招效果
        if (weapon.StatBlock.ultimateEffectPrefab != null)
        {
            Vector3 spawnPos = weapon.transform.position;
            // 尝试在玩家位置生成
            Transform playerT = GameManager.Instance?.playerTransform;
            if (playerT != null)
            {
                spawnPos = playerT.position + Vector3.up * 1f; // 稍微抬高，防止卡地
            }

            // 获取方向：玩家旋转在 Visuals 子物体上，不在根物体
            Transform visualsT = playerT?.Find("Visuals");
            Vector3 fireDir = (visualsT != null) ? visualsT.forward : (playerT != null ? playerT.forward : transform.forward);
            fireDir.y = 0f;
            if (fireDir.sqrMagnitude < 0.01f) fireDir = Vector3.forward;
            fireDir.Normalize();

            GameObject ultimateGo = Instantiate(weapon.StatBlock.ultimateEffectPrefab, spawnPos, Quaternion.LookRotation(fireDir));

            Projectile proj = ultimateGo.GetComponent<Projectile>();
            // 榴弹/闪电链大招走自定义路径，不走直线发射
            bool isGrenadeUlt = weapon.StatBlock.weaponID != null && weapon.StatBlock.weaponID.Contains("Grenade");
            bool isChainUlt = weapon.StatBlock.weaponID != null && weapon.StatBlock.weaponID.Contains("ChainLightning");
            if (proj != null && !isGrenadeUlt && !isChainUlt)
            {
                proj.isUltimate = true; // 标记这是大招子弹

                // 速度读取预制体自带的 speed，如果没配则默认 20f
                float projSpeed = proj.speed > 0.1f ? proj.speed : 20f;
                // 生命读取预制体自带的 lifetime，如果没配则默认 5f
                float projLife = proj.lifetime > 0.1f ? proj.lifetime : 5f;

                proj.InitializeAsStraight(
                    dir: fireDir,
                    spd: projSpeed,
                    directDmg: weapon.StatBlock.ultimateDamage, // 使用大招伤害
                    isEnemyBullet: false,
                    pierce: 999, // 极高穿透
                    life: projLife,
                    shieldVfx: null,
                    defaultVfx: weapon.StatBlock.hitEffectPrefab, // 若没有指定，可缺省
                    dotDmg: 0,
                    dotDur: 0f,
                    dotTick: 0f,
                    slowPct: 0f,
                    slowDur: 0f,
                    type: AttackType.Standard,
                    launcher: weapon,
                    aoeDmg: 0, // 【修复无穿透】置0，避免碰到第一只怪就触发Explode导致自身销毁！
                    aoeRad: 0f, // 【修复无穿透】置0，使得只走纯碰撞无限穿透逻辑
                    explodeVfx: null,
                    freezeChance: 0f
                );
                
                // 放大大招的基础模型（如果是火球，大招火球会大很多）
                // 暂时不硬编码缩放，交给美术在预制体里做大一点，或者在这里根据需求缩放
                // ultimateGo.transform.localScale *= 3f;
            }
            else
            {
                // 没有 Projectile 组件 → 判断大招类型
                var aura = FindObjectOfType<MagneticStormAura>();
                bool isLightning = aura != null;

                if (isLightning)
                {
                    // === 闪电大招：BUFF 型 - 雷霆之力 ===
                    float buffDuration = 10f;
                    float buffCritBonus = 0.5f;

                    // 播放BUFF特效（挂到玩家身上跟随移动，复用外层 playerT）
                    if (playerT != null && ultimateGo != null)
                    {
                        ultimateGo.transform.SetParent(playerT, false);
                        ultimateGo.transform.localPosition = Vector3.up * 1f;
                        ultimateGo.transform.localRotation = Quaternion.identity;

                        // 强制把所有粒子系统改为 Local 空间，确保跟随移动
                        foreach (var ps in ultimateGo.GetComponentsInChildren<ParticleSystem>())
                        {
                            var main = ps.main;
                            main.simulationSpace = ParticleSystemSimulationSpace.Local;
                        }

                        Destroy(ultimateGo, buffDuration);
                    }

                    // 给 MagneticStormAura 临时加暴击率
                    aura.ApplyThunderBuff(buffCritBonus, buffDuration);

                    Debug.Log($"<color=yellow>[大招] 雷霆之力！暴击率 +{buffCritBonus:P0}，持续 {buffDuration} 秒</color>");
                }
                else if (weapon.StatBlock.weaponID != null && weapon.StatBlock.weaponID.Contains("Hurricane"))
                {
                    // === 飓风大招：风暴之怒 - 推开敌人 + 移速BUFF ===
                    float hurricaneBuffDuration = 15f;
                    float moveSpeedBonus = 0.5f; // +50% 移速
                    float pushRadius = weapon.StatBlock.ultimateRadius > 0 ? weapon.StatBlock.ultimateRadius : 10f;
                    float pushForce = 10f;

                    // 播放特效（挂到玩家身上）
                    if (playerT != null && ultimateGo != null)
                    {
                        ultimateGo.transform.SetParent(playerT, false);
                        ultimateGo.transform.localPosition = Vector3.up * 0.5f;
                        ultimateGo.transform.localRotation = Quaternion.identity;

                        foreach (var ps in ultimateGo.GetComponentsInChildren<ParticleSystem>())
                        {
                            var main = ps.main;
                            main.simulationSpace = ParticleSystemSimulationSpace.Local;
                        }

                        Destroy(ultimateGo, hurricaneBuffDuration);
                    }

                    // 推开范围内所有敌人
                    Vector3 center = playerT != null ? playerT.position : weapon.transform.position;
                    Collider[] enemies = Physics.OverlapSphere(center, pushRadius, LayerMask.GetMask("Enemies"));
                    foreach (var col in enemies)
                    {
                        Health h = col.GetComponentInParent<Health>();
                        if (h == null || h.IsDead) continue;

                        Vector3 pushDir = (h.transform.position - center);
                        pushDir.y = 0f;
                        if (pushDir.sqrMagnitude < 0.01f) pushDir = Random.insideUnitSphere;
                        pushDir.Normalize();

                        // 直接位移推开 → 改用协程平滑推开
                        StartCoroutine(SmoothPush(h.transform, pushDir, pushForce));

                        // 造成大招伤害
                        h.TakeDamage(weapon.StatBlock.ultimateDamage, h.transform.position, 
                            playerT != null ? playerT.gameObject : weapon.gameObject, 
                            AttackType.Standard, null, null, weapon.StatBlock.weaponName);
                    }

                    // 临时增加移速
                    if (PlayerStats.Instance != null)
                    {
                        StartCoroutine(HurricaneSpeedBuff(moveSpeedBonus, hurricaneBuffDuration));
                    }

                    Debug.Log($"<color=green>[大招] 风暴之怒！推开 {enemies.Length} 个敌人，移速 +{moveSpeedBonus:P0}，持续 {hurricaneBuffDuration} 秒</color>");
                }
                else if (weapon.StatBlock.weaponID != null && weapon.StatBlock.weaponID.Contains("Grenade"))
                {
                    // === 榴弹大招：毁灭轰炸 - 投掷巨大榴弹造成高额伤害 ===
                    float ultRadius = weapon.StatBlock.ultimateRadius > 0 ? weapon.StatBlock.ultimateRadius : 8f;
                    int ultDamage = weapon.StatBlock.ultimateDamage;

                    // 找到最近敌人作为目标
                    Vector3 playerPos = playerT != null ? playerT.position : weapon.transform.position;
                    Transform targetEnemy = null;
                    float closestDist = float.MaxValue;
                    Collider[] candidates = Physics.OverlapSphere(playerPos, 30f, LayerMask.GetMask("Enemies"));
                    foreach (var col in candidates)
                    {
                        Health h = col.GetComponentInParent<Health>();
                        if (h == null || h.IsDead) continue;
                        float d = Vector3.Distance(playerPos, h.transform.position);
                        if (d < closestDist) { closestDist = d; targetEnemy = h.transform; }
                    }

                    // 目标落点
                    Vector3 targetPos = targetEnemy != null ? targetEnemy.position : playerPos + (playerT != null ? playerT.forward * 5f : Vector3.forward * 5f);

                    // 抛物线投掷特效（巨大榴弹）
                    if (ultimateGo != null)
                    {
                        // 放大特效表现巨型榴弹
                        ultimateGo.transform.localScale *= 3f;
                        StartCoroutine(GrenadeUltimateArc(ultimateGo, playerPos + Vector3.up * 1.5f, targetPos, ultRadius, ultDamage, weapon, playerT));
                    }
                    else
                    {
                        // 没有特效预制件，直接在目标位置造成伤害
                        DealAreaDamage(targetPos, ultRadius, ultDamage, 0f);
                    }

                    Debug.Log($"<color=orange>[大招] 毁灭轰炸！目标距离: {closestDist:F1}，伤害: {ultDamage}，半径: {ultRadius}</color>");
                }
                else if (weapon.StatBlock.weaponID != null && weapon.StatBlock.weaponID.Contains("ChainLightning"))
                {
                    // === 闪电链大招：雷神之怒 - 20次弹射的超级闪电链 ===
                    int ultChainDamage = weapon.StatBlock.ultimateDamage;
                    float ultChainRange = weapon.StatBlock.chainRange > 0 ? weapon.StatBlock.chainRange * 1.5f : 15f;
                    int ultChainCount = 20;

                    // 找最近敌人
                    Vector3 playerPos = playerT != null ? playerT.position : weapon.transform.position;
                    Transform chainTarget = null;
                    float closestDist = float.MaxValue;
                    Collider[] candidates = Physics.OverlapSphere(playerPos, ultChainRange, LayerMask.GetMask("Enemies"));
                    foreach (var col in candidates)
                    {
                        Health h = col.GetComponentInParent<Health>();
                        if (h == null || h.IsDead) continue;
                        float d = Vector3.Distance(playerPos, h.transform.position);
                        if (d < closestDist) { closestDist = d; chainTarget = h.transform; }
                    }

                    if (chainTarget != null)
                    {
                        // 通过 WeaponPart 发射超级闪电链
                        weapon.StartCoroutine(weapon.UltimateChainLightning(chainTarget, ultChainCount, ultChainDamage, ultChainRange));
                    }

                    // 销毁大招特效（如果有）
                    if (ultimateGo != null) Destroy(ultimateGo, 3f);

                    Debug.Log($"<color=cyan>[大招] 雷神之怒！弹射{ultChainCount}次，伤害: {ultChainDamage}</color>");
                }
                else
                {
                    // 其他武器：范围伤害型
                    float freezeTime = (weapon.StatBlock.baseFreezeChance > 0f) ? 3f : 0f;
                    DealAreaDamage(
                        GameManager.Instance?.playerTransform?.position ?? weapon.transform.position,
                        weapon.StatBlock.ultimateRadius,
                        weapon.StatBlock.ultimateDamage,
                        freezeTime
                    );
                }
            }
        }
        else
        {
            // 没有特效预制件的情况
            if (weapon.StatBlock.weaponID != null && weapon.StatBlock.weaponID.Contains("ChainLightning"))
            {
                // 闪电链大招不需要预制件
                int ultChainDamage = weapon.StatBlock.ultimateDamage;
                float ultChainRange = weapon.StatBlock.chainRange > 0 ? weapon.StatBlock.chainRange * 1.5f : 15f;
                int ultChainCount = 20;
                Transform playerT = GameManager.Instance?.playerTransform;
                Vector3 playerPos = playerT != null ? playerT.position : weapon.transform.position;

                Transform chainTarget = null;
                float closestDist = float.MaxValue;
                Collider[] candidates = Physics.OverlapSphere(playerPos, ultChainRange, LayerMask.GetMask("Enemies"));
                foreach (var col in candidates)
                {
                    Health h = col.GetComponentInParent<Health>();
                    if (h == null || h.IsDead) continue;
                    float d = Vector3.Distance(playerPos, h.transform.position);
                    if (d < closestDist) { closestDist = d; chainTarget = h.transform; }
                }

                if (chainTarget != null)
                {
                    weapon.StartCoroutine(weapon.UltimateChainLightning(chainTarget, ultChainCount, ultChainDamage, ultChainRange));
                }
                Debug.Log($"<color=cyan>[大招] 雷神之怒！弹射{ultChainCount}次，伤害: {ultChainDamage}</color>");
            }
            else
            {
                // 通用：范围伤害
                float freezeTime = (weapon.StatBlock.baseFreezeChance > 0f) ? 3f : 0f;
                DealAreaDamage(
                    GameManager.Instance?.playerTransform?.position ?? weapon.transform.position,
                    weapon.StatBlock.ultimateRadius,
                    weapon.StatBlock.ultimateDamage,
                    freezeTime
                );
            }
        }

        // 清空能量
        weapon.ConsumeAllEnergy();

        // 从HUD队列移除图标
        if (UltimateHUD.Instance != null)
        {
            UltimateHUD.Instance.RemoveFromQueue(weapon);
        }

        OnUltimateReleased?.Invoke(weapon.StatBlock.weaponName);
    }

    private void ReleaseComboUltimate(SO_ComboUltimate combo, WeaponPart weaponA, WeaponPart weaponB)
    {
        Debug.Log($"<color=red>[连携技] 释放 {combo.comboName}！</color>");

        // 生成连携技效果
        Vector3 spawnPos = GameManager.Instance?.playerTransform?.position ?? transform.position;
        if (combo.comboEffectPrefab != null)
        {
            GameObject effectGO = Instantiate(combo.comboEffectPrefab, spawnPos, Quaternion.identity);

            // 【修复】初始化龙卷风/持续AOE的伤害参数
            TornadoController tornado = effectGO.GetComponent<TornadoController>();
            if (tornado == null) tornado = effectGO.GetComponentInChildren<TornadoController>();
            if (tornado != null)
            {
                tornado.Setup(combo.comboDamage, weaponA);
                Debug.Log($"<color=green>[连携技] 初始化龙卷风: 伤害={combo.comboDamage}, 来源={weaponA.StatBlock.weaponName}</color>");
            }
        }

        // 一次性范围伤害（初始爆发）
        if (combo.comboRadius > 0 && combo.comboDamage > 0)
        {
            DealAreaDamage(spawnPos, combo.comboRadius, combo.comboDamage);
        }

        // 消耗两个武器的能量
        weaponA.ConsumeAllEnergy();
        weaponB.ConsumeAllEnergy();

        // 从HUD队列移除两个图标
        if (UltimateHUD.Instance != null)
        {
            UltimateHUD.Instance.RemoveFromQueue(weaponA);
            UltimateHUD.Instance.RemoveFromQueue(weaponB);
        }

        OnUltimateReleased?.Invoke(combo.comboName);
    }

    private void DealAreaDamage(Vector3 center, float radius, int damage, float freezeDuration = 0f)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, damageableLayers);
        foreach (var hit in hits)
        {
            Health health = hit.GetComponentInParent<Health>();
            if (health != null && !health.IsDead)
            {
                health.TakeDamage(damage, hit.transform.position, gameObject, AttackType.Standard);

                // 如果有冰冻持续时间，对敌人施加冰冻效果
                if (freezeDuration > 0f)
                {
                    StatusEffectReceiver receiver = health.GetComponent<StatusEffectReceiver>();
                    if (receiver != null)
                    {
                        receiver.ApplyFreeze(freezeDuration);
                    }
                }
            }
        }
        Debug.Log($"[大招伤害] 范围{radius} 伤害{damage} 冰冻{freezeDuration}s 命中{hits.Length}个目标");
    }

    /// <summary>
    /// 飓风大招移速BUFF协程
    /// </summary>
    private System.Collections.IEnumerator HurricaneSpeedBuff(float bonus, float duration)
    {
        PlayerStats.Instance.moveSpeedMultiplier += bonus;
        Debug.Log($"<color=green>[风暴之怒] 移速 +{bonus:P0}，当前倍率: {PlayerStats.Instance.moveSpeedMultiplier}</color>");

        yield return new WaitForSeconds(duration);

        PlayerStats.Instance.moveSpeedMultiplier -= bonus;
        Debug.Log($"<color=green>[风暴之怒] BUFF 结束，移速恢复为: {PlayerStats.Instance.moveSpeedMultiplier}</color>");
    }

    /// <summary>
    /// 平滑推开敌人（在指定时间内将敌人推出指定距离）
    /// </summary>
    private System.Collections.IEnumerator SmoothPush(Transform target, Vector3 direction, float totalDistance)
    {
        float pushDuration = 0.4f; // 推开动画持续 0.4 秒
        float elapsed = 0f;
        float speed = totalDistance / pushDuration;

        while (elapsed < pushDuration && target != null)
        {
            float step = speed * Time.deltaTime;
            target.position += direction * step;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 榴弹大招：巨型榴弹抛物线飞行，落地后爆炸造成范围伤害
    /// </summary>
    private System.Collections.IEnumerator GrenadeUltimateArc(GameObject grenadeFx, Vector3 startPos, Vector3 targetPos,
        float radius, int damage, WeaponPart weapon, Transform playerT)
    {
        float flightTime = 0.8f; // 飞行时间
        float arcHeight = 6f;    // 弧线最高点
        float elapsed = 0f;

        grenadeFx.transform.position = startPos;

        while (elapsed < flightTime)
        {
            float t = elapsed / flightTime;
            // 水平插值
            Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
            // 抛物线高度 (y = -4h*t^2 + 4h*t)
            float heightOffset = (-4f * arcHeight * t * t) + (4f * arcHeight * t);
            pos.y += heightOffset;

            if (grenadeFx != null) grenadeFx.transform.position = pos;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 落地爆炸
        Vector3 explosionPos = targetPos;

        // 播放爆炸特效（如果有配置）
        if (weapon.StatBlock.explosionEffectPrefab != null)
        {
            GameObject explosionVfx = Instantiate(weapon.StatBlock.explosionEffectPrefab, explosionPos, Quaternion.identity);
            explosionVfx.transform.localScale *= 3f; // 巨大爆炸
            Destroy(explosionVfx, 5f);
        }

        // 销毁飞行中的榴弹模型
        if (grenadeFx != null) Destroy(grenadeFx);

        // 造成范围伤害
        Collider[] hits = Physics.OverlapSphere(explosionPos, radius, LayerMask.GetMask("Enemies"));
        foreach (var col in hits)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;

            h.TakeDamage(damage, explosionPos,
                playerT != null ? playerT.gameObject : weapon.gameObject,
                AttackType.Standard, null, null, weapon.StatBlock.weaponName);
        }

        Debug.Log($"<color=orange>[毁灭轰炸] 爆炸！命中 {hits.Length} 个敌人，伤害 {damage}</color>");
    }
}
