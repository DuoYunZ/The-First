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

    [Header("剑士融合大招特效")]
    [Tooltip("火斑融合 - 释放特效（一次性）")]
    public GameObject bladeFire_ReleaseVFX;
    [Tooltip("火斑融合 - 持续BUFF特效（循环，挂载到玩家身上）")]
    public GameObject bladeFire_BuffVFX;
    [Tooltip("火斑融合 - 火海预制体（包含GroundHazard组件）")]
    public GameObject bladeFire_GroundHazardPrefab;
    [Tooltip("雷斑融合 - 释放特效（一次性）")]
    public GameObject bladeThunder_ReleaseVFX;
    [Tooltip("雷斑融合 - 持续BUFF特效（循环）")]
    public GameObject bladeThunder_BuffVFX;
    [Tooltip("风斑融合 - 释放特效（一次性）")]
    public GameObject bladeWind_ReleaseVFX;
    [Tooltip("风斑融合 - 持续BUFF特效（循环）")]
    public GameObject bladeWind_BuffVFX;

    [Header("剑士融合大招数值配置")]
    [Tooltip("火海伤害/跟")]
    public int fireHazardDamagePerTick = 15;
    [Tooltip("火海持续时间")]
    public float fireHazardDuration = 5f;
    [Tooltip("雷震麻痹半径")]
    public float thunderParalyzeRadius = 8f;
    [Tooltip("雷震麻痹时间")]
    public float thunderParalyzeDuration = 3f;
    [Tooltip("风暴吹飞半径")]
    public float windBlowRadius = 10f;
    [Tooltip("风暴吹飞力度")]
    public float windBlowForce = 5f;

    [Header("剑士融合大招音效")]
    [Tooltip("火斑融合释放音效")]
    public AudioClip bladeFire_ReleaseSFX;
    [Tooltip("雷斑融合释放音效")]
    public AudioClip bladeThunder_ReleaseSFX;
    [Tooltip("风斑融合释放音效")]
    public AudioClip bladeWind_ReleaseSFX;

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
                // 先检查 weaponID，再检查 behavior，确保每种武器进入正确分支

                if (weapon.StatBlock.weaponID != null && weapon.StatBlock.weaponID.Contains("Blade"))
                {
                    // === 斩击大招：嗜血斩 - 10秒内造成伤害回血 ===
                    float bladeBuffDuration = 10f;
                    float lifeStealBonus = 0.02f; // +2% 吸血

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

                        Destroy(ultimateGo, bladeBuffDuration);
                    }

                    // 启动吸血BUFF协程
                    if (PlayerStats.Instance != null)
                    {
                        StartCoroutine(BladeLifeStealBuff(lifeStealBonus, bladeBuffDuration));
                    }

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

                        StartCoroutine(SmoothPush(h.transform, pushDir, pushForce));

                        h.TakeDamage(weapon.StatBlock.ultimateDamage, h.transform.position, 
                            playerT != null ? playerT.gameObject : weapon.gameObject, 
                            AttackType.Standard, null, null, weapon.StatBlock.weaponName);
                    }

                    if (PlayerStats.Instance != null)
                    {
                        StartCoroutine(HurricaneSpeedBuff(moveSpeedBonus, hurricaneBuffDuration));
                    }

                }
                else if (weapon.StatBlock.weaponID != null && weapon.StatBlock.weaponID.Contains("Grenade"))
                {
                    // === 榴弹大招：毁灭轰炸 ===
                    float ultRadius = weapon.StatBlock.ultimateRadius > 0 ? weapon.StatBlock.ultimateRadius : 8f;
                    int ultDamage = weapon.StatBlock.ultimateDamage;

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

                    Vector3 targetPos = targetEnemy != null ? targetEnemy.position : playerPos + (playerT != null ? playerT.forward * 5f : Vector3.forward * 5f);

                    if (ultimateGo != null)
                    {
                        ultimateGo.transform.localScale *= 3f;
                        StartCoroutine(GrenadeUltimateArc(ultimateGo, playerPos + Vector3.up * 1.5f, targetPos, ultRadius, ultDamage, weapon, playerT));
                    }
                    else
                    {
                        DealAreaDamage(targetPos, ultRadius, ultDamage, 0f);
                    }

                }
                else if (weapon.StatBlock.weaponID != null && weapon.StatBlock.weaponID.Contains("ChainLightning"))
                {
                    // === 闪电链大招：雷神之怒 ===
                    int ultChainDamage = weapon.StatBlock.ultimateDamage;
                    float ultChainRange = weapon.StatBlock.chainRange > 0 ? weapon.StatBlock.chainRange * 1.5f : 15f;
                    int ultChainCount = 20;

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

                    if (ultimateGo != null) Destroy(ultimateGo, 3f);

                }
                else if (weapon.StatBlock.behavior == WeaponBehaviorType.Aura)
                {
                    // 【关键修复】通过 weapon.orbitalPivot 获取正确的光环实例，避免 FindObjectOfType 找到残留副本
                    SupportAura supportAura = weapon.GetAuraComponent<SupportAura>();
                    MagneticStormAura magneticAura = weapon.GetAuraComponent<MagneticStormAura>();

                    if (magneticAura != null)
                    {
                        // === 雷击光环大招：雷霆之力 - BUFF型加暴击率 ===
                        float buffDuration = 10f;
                        float buffCritBonus = 0.5f;

                        if (playerT != null && ultimateGo != null)
                        {
                            ultimateGo.transform.SetParent(playerT, false);
                            ultimateGo.transform.localPosition = Vector3.up * 1f;
                            ultimateGo.transform.localRotation = Quaternion.identity;

                            foreach (var ps in ultimateGo.GetComponentsInChildren<ParticleSystem>())
                            {
                                var main = ps.main;
                                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                            }

                            Destroy(ultimateGo, buffDuration);
                        }

                        magneticAura.ApplyThunderBuff(buffCritBonus, buffDuration);

                        weapon.isUltimateBuffActive = true;
                        StartCoroutine(ClearUltimateBuffFlag(weapon, buffDuration));

                    }
                    else if (supportAura != null)
                    {
                        // === 辅助光环大招：生命汲取 - 30秒范围x1.5 + 击杀恢复 ===
                        float lifeSiphonDuration = 30f;

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

                            Destroy(ultimateGo, lifeSiphonDuration);
                        }

                        supportAura.isLifeSiphonActive = true;
                        supportAura.isRadiusBoostActive = true;
                        StartCoroutine(LifeSiphonBuff(supportAura, lifeSiphonDuration));

                        weapon.isUltimateBuffActive = true;
                        StartCoroutine(ClearUltimateBuffFlag(weapon, lifeSiphonDuration));

                    }
                    else
                    {
                        Debug.LogWarning("[大招] Aura 武器但找不到 SupportAura 或 MagneticStormAura 实例！");
                        if (ultimateGo != null) Destroy(ultimateGo, 3f);
                    }
                }
                else if (weapon.StatBlock.behavior == WeaponBehaviorType.Orbital)
                {
                    // === 环绕武器大招：涡轮驱动 ===
                    float buffDuration = 10f;
                    float speedMultiplier = 2f;

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

                        Destroy(ultimateGo, buffDuration);
                    }

                    weapon.ForceSpawnOrbiters();

                    weapon.orbitalSpeedMultiplier *= speedMultiplier;
                    Orbiter[] activeOrbiters = FindObjectsOfType<Orbiter>();
                    foreach (var orb in activeOrbiters)
                    {
                        orb.selfRotationSpeed *= speedMultiplier;
                    }

                    StartCoroutine(OrbiterSpeedBuff(weapon, activeOrbiters, speedMultiplier, buffDuration));

                }
                else if (weapon.StatBlock.behavior == WeaponBehaviorType.FlyingDagger)
                {
                    // === 灵能飞刀大招：灵刃风暴 - 速度x3 + 伤害x2 + 体积x2 ===
                    float daggerBuffDuration = 8f;

                    // 找到所有活跃的飞刀，开启大招状态
                    FlameDaggerController[] daggers = FindObjectsOfType<FlameDaggerController>();
                    int activatedCount = 0;
                    foreach (var dagger in daggers)
                    {
                        if (dagger != null && dagger.sourceWeapon == weapon)
                        {
                            dagger.isUltimateActive = true;
                            dagger.transform.localScale *= 2f;

                            // 在每把飞刀上挂载大招特效
                            if (weapon.StatBlock.ultimateEffectPrefab != null)
                            {
                                dagger.AttachUltimateVfx(weapon.StatBlock.ultimateEffectPrefab);
                            }
                            else if (ultimateGo != null)
                            {
                                // 没有专用飞刀特效，用大招特效的子粒子复制到飞刀上
                                dagger.AttachUltimateVfx(ultimateGo);
                            }
                            activatedCount++;
                        }
                    }

                    // 如果大招特效对象还在（没被用作飞刀特效源），挂到玩家身上
                    if (playerT != null && ultimateGo != null && weapon.StatBlock.ultimateEffectPrefab != null)
                    {
                        ultimateGo.transform.SetParent(playerT, false);
                        ultimateGo.transform.localPosition = Vector3.up * 1f;
                        Destroy(ultimateGo, daggerBuffDuration);
                    }

                    StartCoroutine(DaggerUltimateBuff(weapon, daggers, daggerBuffDuration));

                    weapon.isUltimateBuffActive = true;
                    StartCoroutine(ClearUltimateBuffFlag(weapon, daggerBuffDuration));

                }
                else if (weapon.StatBlock.behavior == WeaponBehaviorType.FrostNova)
                {
                    // === 冰霜新星大招：冰爽之星 ===
                    // 在玩家前方生成冰晶体，冰冻范围敌人并阻碍移动
                    float spawnDistance = 5f; // 玩家前方距离
                    // 角色面朝方向在 Visuals 子物体上
                    Transform visualsForFrost = playerT?.Find("Visuals");
                    Vector3 spawnDir = (visualsForFrost != null) ? visualsForFrost.forward : (playerT != null ? playerT.forward : Vector3.forward);
                    spawnDir.y = 0f;
                    if (spawnDir.sqrMagnitude < 0.01f) spawnDir = Vector3.forward;
                    spawnDir.Normalize();
                    Vector3 frostPos = (playerT != null ? playerT.position : weapon.transform.position) + spawnDir * spawnDistance;
                    frostPos.y = playerT != null ? playerT.position.y : frostPos.y; // 保持地面高度

                    if (ultimateGo != null)
                    {
                        // 移动特效到前方
                        ultimateGo.transform.position = frostPos;

                        // 添加冰霜大招组件
                        FrostNovaUltimate frostUlt = ultimateGo.GetComponent<FrostNovaUltimate>();
                        if (frostUlt == null) frostUlt = ultimateGo.AddComponent<FrostNovaUltimate>();

                        // 配置参数
                        frostUlt.freezeRadius = weapon.StatBlock.ultimateRadius > 0 ? weapon.StatBlock.ultimateRadius : 8f;
                        frostUlt.damage = weapon.StatBlock.ultimateDamage > 0 ? weapon.StatBlock.ultimateDamage : 150;
                        frostUlt.freezeDuration = 10f;
                        frostUlt.lifetime = 15f;
                        frostUlt.blockEnemies = true;
                        // 碰撞体使用预制体自带的 Collider，不再代码中添加
                    }

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
            }
            else if (weapon.StatBlock.behavior == WeaponBehaviorType.Orbital)
            {
                // 环绕武器大招（无特效预制件情况）：直接加速
                float buffDuration = 10f;
                float speedMultiplier = 2f;

                // 强制重置冷却并立即召唤环绕武器
                weapon.ForceSpawnOrbiters();

                weapon.orbitalSpeedMultiplier *= speedMultiplier;
                Orbiter[] activeOrbiters = FindObjectsOfType<Orbiter>();
                foreach (var orb in activeOrbiters)
                {
                    orb.selfRotationSpeed *= speedMultiplier;
                }
                StartCoroutine(OrbiterSpeedBuff(weapon, activeOrbiters, speedMultiplier, buffDuration));
            }
            else if (weapon.StatBlock.behavior == WeaponBehaviorType.Landmine)
            {
                // === 地雷大招：矩阵雷区 - 在周围生成3颗地雷 ===
                int mineCount = 3;
                Transform playerT = GameManager.Instance?.playerTransform;
                Vector3 playerPos = playerT != null ? playerT.position : weapon.transform.position;

                for (int i = 0; i < mineCount; i++)
                {
                    Vector2 rnd = Random.insideUnitCircle * (weapon.StatBlock.spawnRadius * 1.5f);
                    Vector3 spawnPos = playerPos + new Vector3(rnd.x, 0, rnd.y);

                    // 尝试放在地面上
                    RaycastHit hit;
                    if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out hit, 10f))
                    { spawnPos = hit.point; }

                    if (weapon.StatBlock.minePrefab != null)
                    {
                        GameObject mineGO = Instantiate(weapon.StatBlock.minePrefab, spawnPos, Quaternion.identity);
                        Landmine mineScript = mineGO.GetComponent<Landmine>();
                        if (mineScript != null)
                        {
                            int finalDmg = Mathf.RoundToInt(weapon.StatBlock.baseAoeDamage * PlayerStats.Instance.aoeDamageMultiplier);
                            float finalRadius = weapon.StatBlock.baseAoeRadius * PlayerStats.Instance.aoeRadiusMultiplier * 1.5f;
                            mineScript.Initialize(
                                finalDmg, finalRadius, 0.1f, weapon.StatBlock.mineDuration,
                                GameManager.Instance?.playerTransform?.gameObject,
                                weapon.StatBlock.explosionEffectPrefab,
                                weapon.StatBlock.layersToDamageByAOE, weapon
                            );
                        }
                    }
                }
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
        // === 【剑士融合大招】检测是否有斩击武器参与 ===
        WeaponController controller = WeaponController.Instance;
        bool isBladeCombo = false;
        WeaponPart bladeWeapon = null;
        WeaponPart otherWeapon = null;

        if (controller != null && controller.builtInBladeWeapon != null)
        {
            if (weaponA == controller.builtInBladeWeapon)
            {
                isBladeCombo = true;
                bladeWeapon = weaponA;
                otherWeapon = weaponB;
            }
            else if (weaponB == controller.builtInBladeWeapon)
            {
                isBladeCombo = true;
                bladeWeapon = weaponB;
                otherWeapon = weaponA;
            }
        }

        if (isBladeCombo && bladeWeapon != null && otherWeapon != null)
        {
            // 找到玩家身上的 PlayerBladeAttack 组件
            PlayerBladeAttack bladeAttack = controller.builtInBladeWeapon.GetComponentInParent<PlayerBladeAttack>();
            if (bladeAttack == null && GameManager.Instance?.playerTransform != null)
                bladeAttack = GameManager.Instance.playerTransform.GetComponentInChildren<PlayerBladeAttack>();

            if (bladeAttack != null)
            {
                // 根据另一把武器的 weaponID 决定模式
                string otherID = otherWeapon.StatBlock?.weaponID ?? "";
                BladeMode targetMode = BladeMode.Normal;

                if (otherID.Contains("Hurricane"))
                    targetMode = BladeMode.WindBlade;
                else if (otherID.Contains("ChainLightning"))
                    targetMode = BladeMode.Thunder;
                else if (otherID.Contains("Fireball"))
                    targetMode = BladeMode.Fire;

                if (targetMode != BladeMode.Normal)
                {
                    bladeAttack.SetBladeMode(targetMode);
                    // === 根据模式附加10秒BUFF效果 ===
                    float buffDuration = 10f;
                    if (PlayerStats.Instance != null)
                    {
                        switch (targetMode)
                        {
                            case BladeMode.Fire:
                                // 火：攻击范围 +50% 持续10秒
                                StartCoroutine(BladeFusionBuff_AreaBoost(0.5f, buffDuration));
                                break;
                            case BladeMode.Thunder:
                                // 雷：暴击率 +50% 持续10秒
                                StartCoroutine(BladeFusionBuff_CritBoost(0.5f, buffDuration));
                                break;
                            case BladeMode.WindBlade:
                                // 风：移速 +30% + 攻速 +100% 持续10秒
                                StartCoroutine(BladeFusionBuff_SpeedBoost(0.3f, 1.0f, buffDuration));
                                break;
                        }
                    }

                    // 消耗两个武器的能量
                    weaponA.ConsumeAllEnergy();
                    weaponB.ConsumeAllEnergy();

                    // 从HUD队列移除
                    if (UltimateHUD.Instance != null)
                    {
                        UltimateHUD.Instance.RemoveFromQueue(weaponA);
                        UltimateHUD.Instance.RemoveFromQueue(weaponB);
                    }

                    // 通知UI
                    OnUltimateReleased?.Invoke(combo.comboName);
                    return; // 剑士融合不走通用特效生成逻辑
                }
            }
        }
        // === 【剑士融合大招】结束 ===

        // 生成连携技效果
        Vector3 spawnPos = GameManager.Instance?.playerTransform?.position ?? transform.position;
        Transform playerT = GameManager.Instance?.playerTransform;

        // 判断是否为 Orbital+ChainLightning 组合（由专属分支处理特效，跳过通用生成）
        bool isOrbitalLightningCombo = 
            ((weaponA.StatBlock.behavior == WeaponBehaviorType.Orbital || weaponB.StatBlock.behavior == WeaponBehaviorType.Orbital) &&
             ((weaponA.StatBlock.weaponID != null && weaponA.StatBlock.weaponID.Contains("ChainLightning")) ||
              (weaponB.StatBlock.weaponID != null && weaponB.StatBlock.weaponID.Contains("ChainLightning"))));

        if (combo.comboEffectPrefab != null && !isOrbitalLightningCombo)
        {
            // 检查预制件是否为喷火塔类型（地雷+火球连携）
            FlamethrowerTurret turretCheck = combo.comboEffectPrefab.GetComponent<FlamethrowerTurret>();
            if (turretCheck != null)
            {
                // === 烈焰炮台阵：在玩家周围生成3个喷火塔 ===
                int turretCount = 3;
                float spawnRadius = 4f;
                for (int i = 0; i < turretCount; i++)
                {
                    // 均匀分布在圆周上
                    float angle = (360f / turretCount) * i;
                    Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * spawnRadius;
                    Vector3 turretPos = spawnPos + offset;

                    // 尝试放在地面上
                    RaycastHit hit;
                    if (Physics.Raycast(turretPos + Vector3.up * 5f, Vector3.down, out hit, 10f))
                    { turretPos = hit.point; }

                    GameObject turretGO = Instantiate(combo.comboEffectPrefab, turretPos, Quaternion.identity);
                    FlamethrowerTurret turret = turretGO.GetComponent<FlamethrowerTurret>();
                    if (turret != null)
                    {
                        turret.Initialize(
                            combo.comboDamage,  // 每tick伤害
                            8f,                 // 持续8秒
                            GameManager.Instance?.playerTransform?.gameObject,
                            turret.flameRange,  // 使用预制件上配置的射程
                            0.2f,               // 伤害间隔
                            combo.comboName
                        );
                    }
                }
            }
            else
            {
                // 非喷火塔：使用原有的生成逻辑
                GameObject effectGO = Instantiate(combo.comboEffectPrefab, spawnPos, Quaternion.identity);

                // 初始化龙卷风/持续AOE的伤害参数
                TornadoController tornado = effectGO.GetComponent<TornadoController>();
                if (tornado == null) tornado = effectGO.GetComponentInChildren<TornadoController>();
                if (tornado != null)
                {
                    tornado.Setup(combo.comboDamage, weaponA);
                    tornado.isComboUltimate = true; // 标记为融合大招，阻止能量增加
                }
            }
        }

        // === 检查是否有 Aura 武器参与连携 → 启用推开效果 ===
        bool hasAura = (weaponA.StatBlock.behavior == WeaponBehaviorType.Aura ||
                        weaponB.StatBlock.behavior == WeaponBehaviorType.Aura);
        if (hasAura)
        {
            float comboDuration = 15f;
            // 【关键修复】通过 auraWeapon.orbitalPivot 获取正确的光环实例
            WeaponPart auraWeapon = (weaponA.StatBlock.behavior == WeaponBehaviorType.Aura) ? weaponA : weaponB;
            SupportAura supportAura = auraWeapon.GetAuraComponent<SupportAura>();
            if (supportAura != null)
            {
                supportAura.isPushActive = true;
                supportAura.isRadiusBoostActive = true;
                supportAura.isLifeSiphonActive = true;
                StartCoroutine(AuraPushBuff(supportAura, comboDuration));
            }

            // 阻止 Aura 武器能量积累
            auraWeapon.isUltimateBuffActive = true;
            StartCoroutine(ClearUltimateBuffFlag(auraWeapon, comboDuration));
        }

        // === 检查是否有飞刀+火球参与连携 → 炎刃流星 ===
        bool hasDagger = (weaponA.StatBlock.behavior == WeaponBehaviorType.FlyingDagger ||
                          weaponB.StatBlock.behavior == WeaponBehaviorType.FlyingDagger);
        bool hasFireball = (weaponA.StatBlock.weaponID != null && weaponA.StatBlock.weaponID.Contains("Fireball")) ||
                           (weaponB.StatBlock.weaponID != null && weaponB.StatBlock.weaponID.Contains("Fireball"));
        if (hasDagger && hasFireball)
        {
            float comboDuration = 15f;
            WeaponPart daggerWeapon = (weaponA.StatBlock.behavior == WeaponBehaviorType.FlyingDagger) ? weaponA : weaponB;

            FlameDaggerController[] daggers = FindObjectsOfType<FlameDaggerController>();
            foreach (var dagger in daggers)
            {
                if (dagger.sourceWeapon == daggerWeapon)
                {
                    dagger.isFlameUltimateActive = true;
                    dagger.isUltimateActive = true;
                    dagger.transform.localScale *= 2f;

                    // 在每把飞刀上挂载融合大招特效
                    if (combo.comboEffectPrefab != null)
                    {
                        dagger.AttachUltimateVfx(combo.comboEffectPrefab);
                    }
                    else if (daggerWeapon.StatBlock.ultimateEffectPrefab != null)
                    {
                        dagger.AttachUltimateVfx(daggerWeapon.StatBlock.ultimateEffectPrefab);
                    }
                }
            }

            StartCoroutine(DaggerFlameUltimateBuff(daggerWeapon, daggers, comboDuration));

            daggerWeapon.isUltimateBuffActive = true;
            StartCoroutine(ClearUltimateBuffFlag(daggerWeapon, comboDuration));

        }

        // === 检查是否有 Orbital+ChainLightning 参与连携 → 雷暴漩涡 ===
        if (isOrbitalLightningCombo)
        {
            float comboDuration = 12f;
            float speedMultiplier = 3f;
            WeaponPart orbitalWeapon = (weaponA.StatBlock.behavior == WeaponBehaviorType.Orbital) ? weaponA : weaponB;
            WeaponPart lightningWeapon = (weaponA.StatBlock.behavior == WeaponBehaviorType.Orbital) ? weaponB : weaponA;

            // 用 comboEffectPrefab 替换当前环绕武器（销毁旧盾，生成紫色 orbit）
            // 复用现有的环绕旋转系统，到期后进入冷却自动重生普通盾
            Transform comboPivot = null;
            if (combo.comboEffectPrefab != null)
            {
                comboPivot = orbitalWeapon.ForceSpawnOrbitersWithPrefab(combo.comboEffectPrefab, comboDuration);
            }
            else
            {
                // 没有专属预制件，直接用普通盾加速
                orbitalWeapon.ForceSpawnOrbiters();
            }

            // 加速环绕旋转
            orbitalWeapon.orbitalSpeedMultiplier *= speedMultiplier;
            Orbiter[] activeOrbiters = FindObjectsOfType<Orbiter>();
            foreach (var orb in activeOrbiters)
            {
                orb.selfRotationSpeed *= speedMultiplier;
            }
            StartCoroutine(OrbiterSpeedBuff(orbitalWeapon, activeOrbiters, speedMultiplier, comboDuration));

            // 把吸附+闪电链逻辑挂到枢轴上（和盾同生共死）
            if (comboPivot != null)
            {
                StormOrbiterUltimate stormUlt = comboPivot.gameObject.AddComponent<StormOrbiterUltimate>();
                stormUlt.Initialize(orbitalWeapon, lightningWeapon, combo.comboDamage, comboDuration);
            }

            orbitalWeapon.isUltimateBuffActive = true;
            lightningWeapon.isUltimateBuffActive = true;
            StartCoroutine(ClearUltimateBuffFlag(orbitalWeapon, comboDuration));
            StartCoroutine(ClearUltimateBuffFlag(lightningWeapon, comboDuration));

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
    }

    /// <summary>
    /// 飓风大招移速BUFF协程
    /// </summary>
    private System.Collections.IEnumerator HurricaneSpeedBuff(float bonus, float duration)
    {
        PlayerStats.Instance.moveSpeedMultiplier += bonus;
        yield return new WaitForSeconds(duration);

        PlayerStats.Instance.moveSpeedMultiplier -= bonus;
    }

    /// <summary>
    /// 斩击大招吸血BUFF协程
    /// </summary>
    private System.Collections.IEnumerator BladeLifeStealBuff(float bonus, float duration)
    {
        PlayerStats.Instance.lifeStealPercent += bonus;
        yield return new WaitForSeconds(duration);

        PlayerStats.Instance.lifeStealPercent -= bonus;
    }

    /// <summary>
    /// 剑士融合BUFF：烈焰 — 攻击范围增大 + 生成火海
    /// </summary>
    private System.Collections.IEnumerator BladeFusionBuff_AreaBoost(float bonus, float duration)
    {
        Transform player = GameManager.Instance?.playerTransform;
        if (player == null) yield break;

        // 释放特效
        if (bladeFire_ReleaseVFX != null)
            Instantiate(bladeFire_ReleaseVFX, player.position, Quaternion.identity);

        // 释放音效
        if (bladeFire_ReleaseSFX != null)
            AudioSource.PlayClipAtPoint(bladeFire_ReleaseSFX, player.position);

        // 生成火海
        if (bladeFire_GroundHazardPrefab != null)
        {
            GameObject hazardGO = Instantiate(bladeFire_GroundHazardPrefab, player.position, Quaternion.identity);
            GroundHazard hazard = hazardGO.GetComponent<GroundHazard>();
            if (hazard != null)
                hazard.Initialize(fireHazardDamagePerTick, fireHazardDuration, "斩击", player.gameObject);
        }

        // 挂载持续BUFF特效
        GameObject buffVFXInstance = null;
        if (bladeFire_BuffVFX != null)
        {
            buffVFXInstance = Instantiate(bladeFire_BuffVFX, player);
            buffVFXInstance.transform.localPosition = Vector3.zero;
        }

        // 应用BUFF
        PlayerStats.Instance.aoeRadiusMultiplier += bonus;

        yield return new WaitForSeconds(duration);

        PlayerStats.Instance.aoeRadiusMultiplier -= bonus;
        if (buffVFXInstance != null) Destroy(buffVFXInstance);
    }

    /// <summary>
    /// 剑士融合BUFF：雷震 — 暴击率提升 + 麻痹范围内敌人
    /// </summary>
    private System.Collections.IEnumerator BladeFusionBuff_CritBoost(float bonus, float duration)
    {
        Transform player = GameManager.Instance?.playerTransform;
        if (player == null) yield break;

        // 释放特效
        if (bladeThunder_ReleaseVFX != null)
            Instantiate(bladeThunder_ReleaseVFX, player.position, Quaternion.identity);

        // 释放音效
        if (bladeThunder_ReleaseSFX != null)
            AudioSource.PlayClipAtPoint(bladeThunder_ReleaseSFX, player.position);

        // 麻痹范围内敌人
        Collider[] enemies = Physics.OverlapSphere(player.position, thunderParalyzeRadius, damageableLayers);
        foreach (var col in enemies)
        {
            StatusEffectReceiver receiver = col.GetComponentInParent<StatusEffectReceiver>();
            if (receiver != null)
                receiver.ApplyParalyze(thunderParalyzeDuration);
        }

        // 挂载持续BUFF特效
        GameObject buffVFXInstance = null;
        if (bladeThunder_BuffVFX != null)
        {
            buffVFXInstance = Instantiate(bladeThunder_BuffVFX, player);
            buffVFXInstance.transform.localPosition = Vector3.zero;
        }

        // 应用BUFF
        PlayerStats.Instance.critRate += bonus;

        yield return new WaitForSeconds(duration);

        PlayerStats.Instance.critRate -= bonus;
        if (buffVFXInstance != null) Destroy(buffVFXInstance);
    }

    /// <summary>
    /// 剑士融合BUFF：风暴 — 移速+攻速提升 + 吹飞周围敌人
    /// </summary>
    private System.Collections.IEnumerator BladeFusionBuff_SpeedBoost(float moveBonus, float fireRateBonus, float duration)
    {
        Transform player = GameManager.Instance?.playerTransform;
        if (player == null) yield break;

        // 释放特效
        if (bladeWind_ReleaseVFX != null)
            Instantiate(bladeWind_ReleaseVFX, player.position, Quaternion.identity);

        // 释放音效
        if (bladeWind_ReleaseSFX != null)
            AudioSource.PlayClipAtPoint(bladeWind_ReleaseSFX, player.position);

        // 吹飞周围敌人
        Collider[] enemies = Physics.OverlapSphere(player.position, windBlowRadius, damageableLayers);
        foreach (var col in enemies)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;

            // 计算吹飞方向（从玩家指向敌人）
            Vector3 pushDir = (h.transform.position - player.position);
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude < 0.01f) pushDir = Vector3.forward;
            pushDir.Normalize();

            // 用 StatusEffectReceiver 的 ApplyKnockback 或平滑推
            StatusEffectReceiver receiver = col.GetComponentInParent<StatusEffectReceiver>();
            if (receiver != null)
                receiver.ApplyKnockback(pushDir, windBlowForce, 0.4f);
            else
                h.StartCoroutine(Projectile.SmoothKnockback(h.transform, pushDir, windBlowForce, 0.4f));
        }

        // 挂载持续BUFF特效
        GameObject buffVFXInstance = null;
        if (bladeWind_BuffVFX != null)
        {
            buffVFXInstance = Instantiate(bladeWind_BuffVFX, player);
            buffVFXInstance.transform.localPosition = Vector3.zero;
        }

        // 应用BUFF
        PlayerStats.Instance.moveSpeedMultiplier += moveBonus;
        PlayerStats.Instance.fireRateMultiplier += fireRateBonus;

        yield return new WaitForSeconds(duration);

        PlayerStats.Instance.moveSpeedMultiplier -= moveBonus;
        PlayerStats.Instance.fireRateMultiplier -= fireRateBonus;
        if (buffVFXInstance != null) Destroy(buffVFXInstance);
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

    }

    /// <summary>
    /// 环绕武器大招：速度BUFF协程，BUFF结束后恢复旋转速度
    /// </summary>
    private System.Collections.IEnumerator OrbiterSpeedBuff(WeaponPart weapon, Orbiter[] orbiters, float multiplier, float duration)
    {
        // BUFF期间阻止能量积累
        if (weapon != null) weapon.isUltimateBuffActive = true;

        yield return new WaitForSeconds(duration);

        // 恢复 WeaponPart 上的倍率（影响未来生成的 Orbiter）
        if (weapon != null)
        {
            weapon.orbitalSpeedMultiplier /= multiplier;
            weapon.isUltimateBuffActive = false; // BUFF结束，恢复能量积累
        }

        // 恢复当前仍存活的 Orbiter 的旋转速度
        foreach (var orb in orbiters)
        {
            if (orb != null)
            {
                orb.selfRotationSpeed /= multiplier;
            }
        }

    }

    /// <summary>
    /// 生命汲取大招BUFF协程：持续时间结束后关闭开关（包括范围增大）
    /// </summary>
    private System.Collections.IEnumerator LifeSiphonBuff(SupportAura aura, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (aura != null)
        {
            aura.isLifeSiphonActive = false;
            aura.isRadiusBoostActive = false;
        }
    }

    /// <summary>
    /// Aura+Hurricane 连携大招BUFF协程：持续时间结束后关闭推开开关
    /// </summary>
    private System.Collections.IEnumerator AuraPushBuff(SupportAura aura, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (aura != null)
        {
            aura.isPushActive = false;
            aura.isRadiusBoostActive = false;
            aura.isLifeSiphonActive = false;
        }
    }

    /// <summary>
    /// 灵刃风暴大招结束 - 恢复飞刀状态
    /// </summary>
    private System.Collections.IEnumerator DaggerUltimateBuff(WeaponPart weapon, FlameDaggerController[] daggers, float duration)
    {
        yield return new WaitForSeconds(duration);
        foreach (var dagger in daggers)
        {
            if (dagger != null && dagger.sourceWeapon == weapon)
            {
                dagger.isUltimateActive = false;
                dagger.transform.localScale *= 0.5f;
                dagger.RemoveUltimateVfx(); // 移除大招特效
            }
        }
    }

    /// <summary>
    /// 炎刃流星融合大招结束 - 恢复火焰生成状态
    /// </summary>
    private System.Collections.IEnumerator DaggerFlameUltimateBuff(WeaponPart weapon, FlameDaggerController[] daggers, float duration)
    {
        yield return new WaitForSeconds(duration);
        foreach (var dagger in daggers)
        {
            if (dagger != null && dagger.sourceWeapon == weapon)
            {
                dagger.isFlameUltimateActive = false;
                dagger.isUltimateActive = false;
                dagger.transform.localScale *= 0.5f;
                dagger.RemoveUltimateVfx(); // 移除大招特效
            }
        }
    }

    /// <summary>
    /// 通用：BUFF型大招结束后恢复能量积累
    /// </summary>
    private System.Collections.IEnumerator ClearUltimateBuffFlag(WeaponPart weapon, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (weapon != null)
        {
            weapon.isUltimateBuffActive = false;
        }
    }
}
