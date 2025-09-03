// EnemySpawner.cs (完整修正版)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("预警特效配置")]
    public GameObject enemySpawnWarningPrefab;
    public float enemySpawnWarningDuration = 1f; // 预警显示时长，单位秒

    [Header("生成点和延迟设置")]
    public float spawnRadiusMin = 10f;
    public float spawnRadiusMax = 15f;

    [Header("后备生成设置 (Fallback Spawn Settings)")]
    [Tooltip("如果动态生成失败，将尝试使用这些预设的后备出生点列表")]
    public List<Transform> predefinedFallbackSpawnPoints;
    private int nextFallbackPointIndex = 0;
    private Vector3? _lastSuccessfulSpawnPosition = null;

    [Tooltip("主要环形区域生成尝试次数")]
    public int maxPrimarySpawnAttempts = 15;
    [Tooltip("后备环形区域生成尝试次数")]
    public int maxFallbackAnnulusAttempts = 25;
    [Tooltip("后备环形区域的最小生成半径")]
    public float fallbackAnnulusMinRadius = 3f;
    [Tooltip("后备环形区域的最大生成半径")]
    public float fallbackAnnulusMaxRadius = 25f;

    [Header("地面检测设置")]
    public float raycastStartYOffset = 50f;
    public float maxRaycastDistance = 100f;
    public LayerMask groundLayerMask;
    public float enemyPivotOffsetY = 0.1f;

    [Header("爆发生成配置 (Burst Spawn)")]
    [Tooltip("当一个组的敌人数量超过此值时，启用爆发生成模式")]
    public int burstSpawnThreshold = 20;
    [Tooltip("在爆发模式下，生成完一整组敌人所用的总时间（秒）")]
    public float burstSpawnTotalDuration = 1.5f;

    private Transform playerTransform = null;
    private Coroutine _currentSpawnRoutine = null;

    void OnEnable()
    {
        AcquirePlayerReference();
    }

    void AcquirePlayerReference()
    {
        if (playerTransform == null && GameManager.Instance != null)
        {
            playerTransform = GameManager.Instance.playerTransform;
        }
    }

    public void InstructToSpawnWaveConfig(WaveConfig waveConfig, int actualWaveNumber,
                                          float healthG, float damageG, float speedG)
    {
        AcquirePlayerReference();
        if (playerTransform == null) { Debug.LogError("EnemySpawner: 玩家引用为空!", this); WaveManager.Instance?.NotifySpawnerFinishedCurrentWave(); return; }
        if (waveConfig == null || waveConfig.enemyGroups == null || waveConfig.enemyGroups.Count == 0) { Debug.LogError("EnemySpawner: 传入的 WaveConfig 无效或没有敌人组!", this); WaveManager.Instance?.NotifySpawnerFinishedCurrentWave(); return; }

        if (_currentSpawnRoutine != null) StopCoroutine(_currentSpawnRoutine);

        _currentSpawnRoutine = StartCoroutine(SpawnEnemiesFromConfigRoutine(
            waveConfig, actualWaveNumber, healthG, damageG, speedG
        ));
    }

    IEnumerator SpawnEnemiesFromConfigRoutine(WaveConfig config, int waveNum,
                                          float healthG, float damageG, float speedG)
    {
        Debug.Log($"Spawner: 开始为波次 {waveNum} ({config.waveName}) 生成敌人组。总组数: {config.enemyGroups.Count}");

        for (int groupIndex = 0; groupIndex < config.enemyGroups.Count; groupIndex++)
        {
            EnemySpawnGroup group = config.enemyGroups[groupIndex];
            if (group.enemyType == null)
            {
                Debug.LogWarning($"波次 {waveNum} 的第 {groupIndex + 1} 个敌人组没有设置 EnemyType，已跳过。");
                continue;
            }

            if (group.delayAfterPreviousGroupStarts > 0 && groupIndex > 0)
            {
                yield return new WaitForSeconds(group.delayAfterPreviousGroupStarts);
            }

            Debug.Log($"Spawner: 开始生成组 {groupIndex + 1}/{config.enemyGroups.Count} - 类型: {group.enemyType.enemyName}, 数量: {group.count}");

            bool isBurstSpawn = group.count > burstSpawnThreshold;
            float interval;
            if (isBurstSpawn)
            {
                // 爆发模式：用总时长除以数量，得到极短的间隔
                interval = burstSpawnTotalDuration / group.count;
                Debug.Log($"<color=cyan>启用爆发生成模式！总时长: {burstSpawnTotalDuration}s, 每个敌人间隔: {interval.ToString("F3")}s</color>");
            }
            else
            {
                // 普通模式：使用配置中指定的间隔
                interval = group.spawnIntervalWithinGroup > 0 ? group.spawnIntervalWithinGroup : 0.1f;
            }

            float minAngle = 0f, maxAngle = 360f;
            switch (group.directionHint)
            {
                case SpawnDirectionHint.North: minAngle = -45f; maxAngle = 45f; break;
                case SpawnDirectionHint.East: minAngle = 45f; maxAngle = 135f; break;
                case SpawnDirectionHint.South: minAngle = 135f; maxAngle = 225f; break;
                case SpawnDirectionHint.West: minAngle = 225f; maxAngle = 315f; break;
            }

            for (int i = 0; i < group.count; i++)
            {
                if (playerTransform == null)
                {
                    Debug.LogWarning("EnemySpawner: 玩家已不存在，停止生成协程。");
                    yield break;
                }

                Vector3 spawnPosition = Vector3.zero;
                Quaternion spawnRotation = Quaternion.identity;
                bool successfullyFoundPoint = false;

                if (TryFindValidSpawnPointInAnnulus(playerTransform.position, spawnRadiusMin, spawnRadiusMax, maxPrimarySpawnAttempts, out spawnPosition, out spawnRotation, minAngle, maxAngle))
                {
                    successfullyFoundPoint = true;
                    _lastSuccessfulSpawnPosition = spawnPosition;
                }
                else if (_lastSuccessfulSpawnPosition.HasValue && IsSpawnPointStillValid(_lastSuccessfulSpawnPosition.Value, playerTransform.position, out spawnPosition, out spawnRotation))
                {
                    successfullyFoundPoint = true;
                    _lastSuccessfulSpawnPosition = spawnPosition;
                }
                else if (TryFindValidSpawnPointInAnnulus(playerTransform.position, fallbackAnnulusMinRadius, fallbackAnnulusMaxRadius, maxFallbackAnnulusAttempts, out spawnPosition, out spawnRotation))
                {
                    successfullyFoundPoint = true;
                    _lastSuccessfulSpawnPosition = spawnPosition;
                }
                else if (predefinedFallbackSpawnPoints != null && predefinedFallbackSpawnPoints.Count > 0)
                {
                    for (int k = 0; k < predefinedFallbackSpawnPoints.Count; k++)
                    {
                        Transform fallbackCandidate = predefinedFallbackSpawnPoints[nextFallbackPointIndex];
                        nextFallbackPointIndex = (nextFallbackPointIndex + 1) % predefinedFallbackSpawnPoints.Count;
                        if (fallbackCandidate != null && IsSpawnPointStillValid(fallbackCandidate.position, playerTransform.position, out spawnPosition, out spawnRotation, true, fallbackCandidate.rotation))
                        {
                            successfullyFoundPoint = true;
                            _lastSuccessfulSpawnPosition = spawnPosition;
                            break;
                        }
                    }
                }

                if (successfullyFoundPoint)
                {
                    // 1. 生成预警特效
                    if (enemySpawnWarningPrefab != null)
                    {
                        Vector3 warningPos = spawnPosition + Vector3.up * 0.1f;
                        GameObject warningEffect = Instantiate(enemySpawnWarningPrefab, warningPos, Quaternion.identity);
                        Destroy(warningEffect, enemySpawnWarningDuration);
                    }

                    // 2. 等待预警时间
                    if (enemySpawnWarningDuration > 0)
                    {
                        yield return new WaitForSeconds(enemySpawnWarningDuration);
                    }

                    // 3. 预警结束后，生成敌人
                    EnemyType type = group.enemyType;

                    // 1. 根据优先级，确定基础属性 (覆盖 > 精英 > 普通)
                    float baseHealth, baseDamage, baseSpeed;
                    Vector3 scale = Vector3.one;
                    // Color tint = Color.white; // (可选) 如果需要颜色覆盖

                    if (group.overrideStats)
                    {
                        // 优先级1：使用自定义覆盖属性
                        baseHealth = type.baseHealth * group.statOverrides.healthMultiplier;
                        baseDamage = type.baseDamage * group.statOverrides.damageMultiplier;
                        baseSpeed = type.baseSpeed * group.statOverrides.speedMultiplier;
                        scale = group.statOverrides.scale;
                    }
                    else if (group.isElite && type.canBeElite)
                    {
                        // 优先级2：使用精英属性
                        baseHealth = type.baseHealth * type.eliteHealthMultiplier;
                        baseDamage = type.baseDamage * type.eliteDamageMultiplier;
                        baseSpeed = type.baseSpeed * type.eliteSpeedMultiplier;
                        scale = type.eliteScale;
                        // tint = type.eliteColorTint;
                    }
                    else
                    {
                        // 优先级3：使用标准基础属性
                        baseHealth = type.baseHealth;
                        baseDamage = type.baseDamage;
                        baseSpeed = type.baseSpeed;
                    }
                                       
                    // 2. 在选定的基础上，应用全局的波次成长
                    float finalHealth = baseHealth * (1f + (waveNum - 1) * healthG);
                    float finalDamage = baseDamage * (1f + (waveNum - 1) * damageG);
                    float finalSpeed = baseSpeed * (1f + (waveNum - 1) * speedG);

                    // 3. 实例化敌人并应用最终属性
                    GameObject enemyGO = Instantiate(type.enemyPrefab, spawnPosition, spawnRotation);
                    if (type.isSuicideBomber)
                    {
                        // 获取自爆脚本的引用
                        EnemyExplosionAttack explosionScript = enemyGO.GetComponent<EnemyExplosionAttack>();
                        if (explosionScript != null)
                        {
                            // 将 EnemyType 数据传递给它
                            explosionScript.Initialize(type);
                        }
                        else
                        {
                            Debug.LogWarning($"自爆怪 '{type.name}' 的预制件上缺少 EnemyExplosionAttack 脚本！", enemyGO);
                        }
                    }
                    enemyGO.transform.localScale = scale; // 应用大小缩放
                    // Renderer enemyRenderer = enemyGO.GetComponentInChildren<Renderer>();
                    // if (enemyRenderer != null) enemyRenderer.material.color = tint;

                    Health healthScript = enemyGO.GetComponent<Health>();
                    if (healthScript != null) healthScript.InitializeHealth(Mathf.RoundToInt(finalHealth));

                    EnemyAI aiScript = enemyGO.GetComponent<EnemyAI>();
                    if (aiScript != null) aiScript.InitializeEnemy(finalSpeed, Mathf.RoundToInt(finalDamage));

                    if (type.isBoss && healthScript != null)
                    {
                        WaveManager.Instance?.RegisterBossInstance(healthScript);
                    }

                    // 随机化 Animator 的起始帧 (您的已有逻辑，保持不变)
                    Animator animator = enemyGO.GetComponentInChildren<Animator>();
                    if (animator != null)
                    {
                        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                        animator.Play(stateInfo.fullPathHash, 0, Random.Range(0f, 1f));
                    }
                    // --- 修改结束 ---
                }
                else
                {
                    Debug.LogError($"CRITICAL SPAWN FAILURE: 未能为波次 {waveNum} 的 {group.enemyType.name} 找到任何有效出生点!");
                    WaveManager.Instance?.AnEnemyFailedToSpawn();
                }
                
                yield return new WaitForSeconds(interval);
            }
        }
        _currentSpawnRoutine = null;
        WaveManager.Instance?.NotifySpawnerFinishedCurrentWave();
    }

    bool TryFindValidSpawnPointInAnnulus(Vector3 center, float minRadius, float maxRadius, int attempts,
                                     out Vector3 foundPosition, out Quaternion foundRotation,
                                     float minAngleDegrees = 0f, float maxAngleDegrees = 360f)
    {
        foundPosition = Vector3.zero;
        foundRotation = Quaternion.identity;

        if (playerTransform == null)
        {
            AcquirePlayerReference();
            if (playerTransform == null) { return false; }
        }

        for (int i = 0; i < attempts; i++)
        {
            float randomAngleDegrees = Random.Range(minAngleDegrees, maxAngleDegrees);
            float randomAngleRadians = randomAngleDegrees * Mathf.Deg2Rad;

            Vector3 spawnDirection = new Vector3(Mathf.Sin(randomAngleRadians), 0, Mathf.Cos(randomAngleRadians));
            float randomDistance = Random.Range(minRadius, maxRadius);
            Vector3 potentialSpawnPointXZ = center + spawnDirection * randomDistance;

            Vector3 rayOrigin = new Vector3(potentialSpawnPointXZ.x, playerTransform.position.y + raycastStartYOffset, potentialSpawnPointXZ.z);

            RaycastHit hitInfo;
            if (Physics.Raycast(rayOrigin, Vector3.down, out hitInfo, maxRaycastDistance, groundLayerMask))
            {
                foundPosition = hitInfo.point + Vector3.up * enemyPivotOffsetY;
                Vector3 directionToPlayer = (playerTransform.position - foundPosition);
                directionToPlayer.y = 0;
                if (directionToPlayer.sqrMagnitude > 0.01f)
                {
                    foundRotation = Quaternion.LookRotation(directionToPlayer.normalized);
                }
                else
                {
                    foundRotation = Quaternion.identity;
                }
                return true;
            }
        }
        return false;
    }

    bool IsSpawnPointStillValid(Vector3 potentialSpawnBase, Vector3 playerPositionForOrientation, out Vector3 validatedPosition, out Quaternion validatedRotation, bool usePotentialPointYForRayOrigin = false, Quaternion defaultRotation = default)
    {
        validatedPosition = potentialSpawnBase;
        validatedRotation = defaultRotation == default ? Quaternion.identity : defaultRotation;

        float rayOriginYBase = usePotentialPointYForRayOrigin ? potentialSpawnBase.y : playerPositionForOrientation.y;
        Vector3 rayOrigin = new Vector3(potentialSpawnBase.x, rayOriginYBase + raycastStartYOffset, potentialSpawnBase.z);

        RaycastHit hitInfo;
        if (Physics.Raycast(rayOrigin, Vector3.down, out hitInfo, maxRaycastDistance, groundLayerMask))
        {
            validatedPosition = hitInfo.point + (Vector3.up * enemyPivotOffsetY);
            Vector3 directionToPlayer = (playerPositionForOrientation - validatedPosition);
            directionToPlayer.y = 0;
            if (directionToPlayer.sqrMagnitude > 0.01f)
            {
                validatedRotation = Quaternion.LookRotation(directionToPlayer.normalized);
            }
            return true;
        }
        return false;
    }

    public void StopAndClearSpawning()
    {
        if (_currentSpawnRoutine != null)
        {
            StopCoroutine(_currentSpawnRoutine);
            _currentSpawnRoutine = null;
        }
        Debug.Log("EnemySpawner: 生成已停止并清理。");
    }
    public void Debug_SpawnSingleEnemy(EnemyType enemyTypeToSpawn)
    {
        if (playerTransform == null)
        {
            AcquirePlayerReference();
            if (playerTransform == null)
            {
                Debug.LogError("Debug_SpawnSingleEnemy: 玩家引用为空，无法生成！");
                return;
            }
        }

        // 尝试在常规范围内寻找出生点
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        if (!TryFindValidSpawnPointInAnnulus(playerTransform.position, spawnRadiusMin, spawnRadiusMax, 20, out spawnPosition, out spawnRotation))
        {
            Debug.LogError("Debug_SpawnSingleEnemy: 未能找到有效的出生点！");
            return;
        }

        // 【关键】复用我们已有的、完整的属性计算和初始化逻辑
        // 我们假设调试生成的怪物不受波次成长影响 (waveNum = 1, growth factors = 0)

        // 1. 确定基础属性 (只考虑普通和精英，不考虑覆盖)
        float baseHealth, baseDamage, baseSpeed;
        Vector3 scale = Vector3.one;

        if (enemyTypeToSpawn.canBeElite) // 假设调试时可以生成精英
        {
            baseHealth = enemyTypeToSpawn.baseHealth * enemyTypeToSpawn.eliteHealthMultiplier;
            baseDamage = enemyTypeToSpawn.baseDamage * enemyTypeToSpawn.eliteDamageMultiplier;
            baseSpeed = enemyTypeToSpawn.baseSpeed * enemyTypeToSpawn.eliteSpeedMultiplier;
            scale = enemyTypeToSpawn.eliteScale;
        }
        else
        {
            baseHealth = enemyTypeToSpawn.baseHealth;
            baseDamage = enemyTypeToSpawn.baseDamage;
            baseSpeed = enemyTypeToSpawn.baseSpeed;
        }

        // 2. 实例化并应用属性
        GameObject enemyGO = Instantiate(enemyTypeToSpawn.enemyPrefab, spawnPosition, spawnRotation);
        enemyGO.transform.localScale = scale;

        // 【重要】确保自爆怪的Initialize方法被调用
        if (enemyTypeToSpawn.isSuicideBomber)
        {
            enemyGO.GetComponent<EnemyExplosionAttack>()?.Initialize(enemyTypeToSpawn);
        }

        enemyGO.GetComponent<Health>()?.InitializeHealth(Mathf.RoundToInt(baseHealth));
        enemyGO.GetComponent<EnemyAI>()?.InitializeEnemy(baseSpeed, Mathf.RoundToInt(baseDamage));

        // 注意：调试生成的敌人，我们需要手动通知 WaveManager
        // 这是一个简化处理，在测试模式下可以接受
        WaveManager.Instance?.RegisterDebugEnemy();
    }
}