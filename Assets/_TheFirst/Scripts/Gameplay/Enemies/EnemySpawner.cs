// --- EnemySpawner.cs (最终完整版 - 解决了预警等待问题) ---
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("预警特效配置")]
    public GameObject enemySpawnWarningPrefab;
    public float enemySpawnWarningDuration = 1f;

    [Header("生成点和延迟设置")]
    public float spawnRadiusMin = 10f;
    public float spawnRadiusMax = 15f;

    [Header("后备生成设置 (Fallback Spawn Settings)")]
    public List<Transform> predefinedFallbackSpawnPoints;
    private int nextFallbackPointIndex = 0;
    private Vector3? _lastSuccessfulSpawnPosition = null;
    public int maxPrimarySpawnAttempts = 15;
    public int maxFallbackAnnulusAttempts = 25;
    public float fallbackAnnulusMinRadius = 3f;
    public float fallbackAnnulusMaxRadius = 25f;

    [Header("地面检测设置")]
    public float raycastStartYOffset = 50f;
    public float maxRaycastDistance = 100f;
    public LayerMask groundLayerMask;
    public float enemyPivotOffsetY = 0.1f;

    [Header("爆发生成配置 (Burst Spawn)")]
    public int burstSpawnThreshold = 20;
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

            StartCoroutine(SpawnGroupRoutine(group, waveNum, healthG, damageG, speedG));
        }
        /*Debug.Log($"Spawner: 开始生成组 {groupIndex + 1}/{config.enemyGroups.Count} - 类型: {group.enemyType.enemyName}, 数量: {group.count}");

            int finalBurstThreshold;
            float finalBurstDuration;

            if (group.overrideSpawnerBurstSettings)
            {
                // 如果勾选了覆盖，则使用 group 自己的独立设置
                finalBurstThreshold = group.burstSpawnThreshold;
                finalBurstDuration = group.burstSpawnTotalDuration;
                Debug.Log($"组 {groupIndex + 1} 使用了独立的爆发设置。");
            }
            else
            {
                // 否则，使用 EnemySpawner 上的全局设置
                finalBurstThreshold = this.burstSpawnThreshold;
                finalBurstDuration = this.burstSpawnTotalDuration;
            }

            bool isBurstSpawn = group.count > burstSpawnThreshold;
            float interval;

            if (isBurstSpawn)
            {
                interval = finalBurstDuration / group.count;
                Debug.Log($"<color=cyan>启用爆发生成模式！将在 {finalBurstDuration}s 内陆续生成 {group.count} 个敌人, 每个间隔: {interval.ToString("F3")}s</color>");
            }
            else
            {
                interval = group.spawnIntervalWithinGroup > 0 ? group.spawnIntervalWithinGroup : 0.1f;
            }

            // 【新的生成循环】
            // 这个主循环现在只负责按顺序“触发”每个敌人的生成流程
            List<(Vector3 pos, Quaternion rot)> spawnPoints = new List<(Vector3 pos, Quaternion rot)>();
            for (int i = 0; i < group.count; i++)
            {
                if (playerTransform == null) yield break;

                // 1. 找到一个生成点
                if (TryFindValidSpawnPoint(out Vector3 spawnPosition, out Quaternion spawnRotation, group.directionHint))
                {
                    // 2. 启动一个独立的“子协程”来处理这一个敌人的完整生命周期
                    StartCoroutine(SpawnSingleEnemyWithWarning(spawnPosition, spawnRotation, group, waveNum, healthG, damageG, speedG));
                }
                else
                {
                    Debug.LogError($"CRITICAL SPAWN FAILURE: 未能为波次 {waveNum} 的 {group.enemyType.name} 找到任何有效出生点!");
                    WaveManager.Instance?.AnEnemyFailedToSpawn();
                }

                // 3. 等待间隔，然后去触发下一个敌人的生成流程
                if (interval > 0)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        _currentSpawnRoutine = null;
        WaveManager.Instance?.NotifySpawnerFinishedCurrentWave();*/
    }
    private IEnumerator SpawnGroupRoutine(EnemySpawnGroup group, int waveNum, float healthG, float damageG, float speedG)
    {
        Debug.Log($"Spawner: 开始生成组 - 类型: {group.enemyType.enemyName}, 数量: {group.count}");

        // --- 这部分逻辑是从旧的主协程中完整移动过来的 ---
        int finalBurstThreshold;
        float finalBurstDuration;

        if (group.overrideSpawnerBurstSettings)
        {
            finalBurstThreshold = group.burstSpawnThreshold;
            finalBurstDuration = group.burstSpawnTotalDuration;
        }
        else
        {
            finalBurstThreshold = this.burstSpawnThreshold;
            finalBurstDuration = this.burstSpawnTotalDuration;
        }

        // 【修正】这里的 burstSpawnThreshold 应该使用 finalBurstThreshold
        bool isBurstSpawn = group.count > finalBurstThreshold;
        float interval;

        if (isBurstSpawn)
        {
            interval = finalBurstDuration / group.count;
            Debug.Log($"<color=cyan>启用爆发生成模式！将在 {finalBurstDuration}s 内陆续生成 {group.count} 个敌人, 每个间隔: {interval.ToString("F3")}s</color>");
        }
        else
        {
            interval = group.spawnIntervalWithinGroup > 0 ? group.spawnIntervalWithinGroup : 0.1f;
        }

        for (int i = 0; i < group.count; i++)
        {
            if (playerTransform == null) yield break;

            if (TryFindValidSpawnPoint(out Vector3 spawnPosition, out Quaternion spawnRotation, group.directionHint))
            {
                // 注意：SpawnSingleEnemyWithWarning 本身也是一个协程，这没有问题。
                StartCoroutine(SpawnSingleEnemyWithWarning(spawnPosition, spawnRotation, group, waveNum, healthG, damageG, speedG));
            }
            else
            {
                Debug.LogError($"CRITICAL SPAWN FAILURE: 未能为波次 {waveNum} 的 {group.enemyType.name} 找到任何有效出生点!");
                WaveManager.Instance?.AnEnemyFailedToSpawn();
            }

            if (interval > 0)
            {
                yield return new WaitForSeconds(interval);
            }
        }
    }
    private IEnumerator SpawnSingleEnemyWithWarning(Vector3 position, Quaternion rotation, EnemySpawnGroup group, int waveNum, float healthG, float damageG, float speedG)
    {
        // 1. 在指定位置生成预警特效
        if (enemySpawnWarningPrefab != null && enemySpawnWarningDuration > 0)
        {
            GameObject warningEffect = Instantiate(enemySpawnWarningPrefab, position + Vector3.up * 0.1f, Quaternion.identity);
            Destroy(warningEffect, enemySpawnWarningDuration);
        }

        // 2. 等待预警时间
        if (enemySpawnWarningDuration > 0)
        {
            yield return new WaitForSeconds(enemySpawnWarningDuration);
        }

        // 3. 预警结束后，生成实际的敌人
        // (这是您原有的、完整的属性计算和敌人初始化逻辑)
        EnemyType type = group.enemyType;
        float baseHealth, baseDamage, baseSpeed;
        Vector3 scale = Vector3.one;
        
        if (group.overrideStats)
        {
            baseHealth = type.baseHealth * group.statOverrides.healthMultiplier;
            baseDamage = type.baseDamage * group.statOverrides.damageMultiplier;
            baseSpeed = type.baseSpeed * group.statOverrides.speedMultiplier;
            scale = group.statOverrides.scale;
        }
        else if (group.isElite && type.canBeElite)
        {
            baseHealth = type.baseHealth * type.eliteHealthMultiplier;
            baseDamage = type.baseDamage * type.eliteDamageMultiplier;
            baseSpeed = type.baseSpeed * type.eliteSpeedMultiplier;
            scale = type.eliteScale;
        }
        else
        {
            baseHealth = type.baseHealth;
            baseDamage = type.baseDamage;
            baseSpeed = type.baseSpeed;
        }

        float finalHealth = baseHealth * (1f + (waveNum - 1) * healthG);
        float finalDamage = baseDamage * (1f + (waveNum - 1) * damageG);
        float finalSpeed = baseSpeed * (1f + (waveNum - 1) * speedG);

        GameObject enemyGO = Instantiate(type.enemyPrefab, position, rotation);
        enemyGO.transform.localScale = scale;
        if (type.isSuicideBomber) { enemyGO.GetComponent<EnemyExplosionAttack>()?.Initialize(type); }
        enemyGO.GetComponent<Health>()?.InitializeHealth(Mathf.RoundToInt(finalHealth), type);
        enemyGO.GetComponent<EnemyAI>()?.InitializeEnemy(finalSpeed, Mathf.RoundToInt(finalDamage));
        if (type.isBoss && enemyGO.GetComponent<Health>() != null) { WaveManager.Instance?.RegisterBossInstance(enemyGO.GetComponent<Health>()); }
        Animator animator = enemyGO.GetComponentInChildren<Animator>();
        if (animator != null) { AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0); animator.Play(stateInfo.fullPathHash, 0, Random.Range(0f, 1f)); }
    }

    // 辅助方法，将您原有的多种寻找生成点的逻辑整合到一个方法中，方便调用
    bool TryFindValidSpawnPoint(out Vector3 position, out Quaternion rotation, SpawnDirectionHint hint = SpawnDirectionHint.Random)
    {
        float minAngle = 0f, maxAngle = 360f;
        switch (hint)
        {
            case SpawnDirectionHint.North: minAngle = -45f; maxAngle = 45f; break;
            case SpawnDirectionHint.East: minAngle = 45f; maxAngle = 135f; break;
            case SpawnDirectionHint.South: minAngle = 135f; maxAngle = 225f; break;
            case SpawnDirectionHint.West: minAngle = 225f; maxAngle = 315f; break;
        }

        if (TryFindValidSpawnPointInAnnulus(playerTransform.position, spawnRadiusMin, spawnRadiusMax, maxPrimarySpawnAttempts, out position, out rotation, minAngle, maxAngle) ||
            (_lastSuccessfulSpawnPosition.HasValue && IsSpawnPointStillValid(_lastSuccessfulSpawnPosition.Value, playerTransform.position, out position, out rotation)) ||
            TryFindValidSpawnPointInAnnulus(playerTransform.position, fallbackAnnulusMinRadius, fallbackAnnulusMaxRadius, maxFallbackAnnulusAttempts, out position, out rotation) ||
            TryFindWithPredefinedPoints(out position, out rotation))
        {
            _lastSuccessfulSpawnPosition = position;
            return true;
        }
        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    bool TryFindWithPredefinedPoints(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero; rotation = Quaternion.identity;
        if (predefinedFallbackSpawnPoints == null || predefinedFallbackSpawnPoints.Count == 0) return false;

        for (int k = 0; k < predefinedFallbackSpawnPoints.Count; k++)
        {
            Transform fallbackCandidate = predefinedFallbackSpawnPoints[nextFallbackPointIndex];
            nextFallbackPointIndex = (nextFallbackPointIndex + 1) % predefinedFallbackSpawnPoints.Count;
            if (fallbackCandidate != null && IsSpawnPointStillValid(fallbackCandidate.position, playerTransform.position, out position, out rotation, true, fallbackCandidate.rotation))
            {
                return true;
            }
        }
        return false;
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

        if (!TryFindValidSpawnPoint(out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            Debug.LogError("Debug_SpawnSingleEnemy: 未能找到有效的出生点！");
            return;
        }

        float baseHealth, baseDamage, baseSpeed;
        Vector3 scale = Vector3.one;

        if (enemyTypeToSpawn.canBeElite)
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

        GameObject enemyGO = Instantiate(enemyTypeToSpawn.enemyPrefab, spawnPosition, spawnRotation);
        enemyGO.transform.localScale = scale;

        if (enemyTypeToSpawn.isSuicideBomber)
        {
            enemyGO.GetComponent<EnemyExplosionAttack>()?.Initialize(enemyTypeToSpawn);
        }

        enemyGO.GetComponent<Health>()?.InitializeHealth(Mathf.RoundToInt(baseHealth));
        enemyGO.GetComponent<EnemyAI>()?.InitializeEnemy(baseSpeed, Mathf.RoundToInt(baseDamage));

        WaveManager.Instance?.RegisterDebugEnemy();
    }
}