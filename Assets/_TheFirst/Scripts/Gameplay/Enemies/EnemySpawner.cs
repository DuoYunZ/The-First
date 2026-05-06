// --- EnemySpawner.cs (最终完整版 - 解决了预警等待问题) ---
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EnemySpawnGroup;

public class EnemySpawner : MonoBehaviour
{
    [Header("预警特效配置")]
    public GameObject enemySpawnWarningPrefab;
    public float enemySpawnWarningDuration = 1f;

    [Header("常规生成点 (Chasing AI)")]
    public float spawnRadiusMin = 10f;
    public float spawnRadiusMax = 15f;

    [Header("奔袭生成点 (Stampede AI)")]
    public float stampedeCardinalRadiusMin = 30f;
    public float stampedeCardinalRadiusMax = 35f;
    public float stampedeDiagonalRadiusMin = 42f;
    public float stampedeDiagonalRadiusMax = 47f;

    [Header("后备生成设置 (Fallback Spawn Settings)")]
    public List<Transform> predefinedFallbackSpawnPoints;
    private int nextFallbackPointIndex = 0;
    private Vector3? _lastSuccessfulSpawnPosition = null;
    public int maxPrimarySpawnAttempts = 15;
    public int maxFallbackAnnulusAttempts = 25;
    public float fallbackAnnulusMinRadius = 3f;
    public float fallbackAnnulusMaxRadius = 25f;

    [Header("地图边界限制")]
    [Tooltip("地图中心点（世界坐标，通常是场景圆形区域的中心）")]
    public Transform mapCenter;
    [Tooltip("地图的有效半径（怪物不会生成到这个圆之外）")]
    public float mapRadius = 25f;

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

    void OnEnable() { AcquirePlayerReference(); }
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
        if (playerTransform == null) { Debug.LogError("EnemySpawner: 玩家引用为空!", this); GameTimelineManager.Instance?.AnEnemyFailedToSpawn(); return; }
        if (waveConfig == null || waveConfig.enemyGroups == null || waveConfig.enemyGroups.Count == 0) { Debug.LogError("EnemySpawner: 传入的 WaveConfig 无效或没有敌人组!", this); GameTimelineManager.Instance?.AnEnemyFailedToSpawn(); return; }

        if (_currentSpawnRoutine != null) StopCoroutine(_currentSpawnRoutine);

        _currentSpawnRoutine = StartCoroutine(SpawnEnemiesFromConfigRoutine(
            waveConfig, actualWaveNumber, healthG, damageG, speedG
        ));
    }

    IEnumerator SpawnEnemiesFromConfigRoutine(WaveConfig config, int waveNum,
                                           float healthG, float damageG, float speedG)
    {
        List<Coroutine> groupCoroutines = new List<Coroutine>();
        for (int groupIndex = 0; groupIndex < config.enemyGroups.Count; groupIndex++)
        {
            EnemySpawnGroup group = config.enemyGroups[groupIndex];
            if (group.enemyType == null) { continue; }
            groupCoroutines.Add(StartCoroutine(GroupSpawnWrapper(group, waveNum, healthG, damageG, speedG)));
        }
        foreach (Coroutine coroutine in groupCoroutines) { yield return coroutine; }
        _currentSpawnRoutine = null;
        // 已移除 WaveManager 通知
    }

    private IEnumerator GroupSpawnWrapper(EnemySpawnGroup group, int waveNum, float healthG, float damageG, float speedG)
    {
        if (group.delayAfterPreviousGroupStarts > 0)
        {
            yield return new WaitForSeconds(group.delayAfterPreviousGroupStarts);
        }
        StartCoroutine(SpawnGroupRoutine(group, waveNum, healthG, damageG, speedG));
    }

    private IEnumerator SpawnGroupRoutine(EnemySpawnGroup group, int waveNum, float healthG, float damageG, float speedG)
    {
        bool isStampede = group.enemyType.aiType == AIType.StraightLineStampede;

        if (isStampede)
        {
            if (WarningUIManager.Instance != null) { WarningUIManager.Instance.ShowStampedeGroupWarning(group.directionHint, enemySpawnWarningDuration); }
            else { Debug.LogWarning("WarningUIManager 未找到! 无法显示奔袭预警。"); }
        }

        int finalBurstThreshold;
        float finalBurstDuration;
        if (group.overrideSpawnerBurstSettings) { finalBurstThreshold = group.burstSpawnThreshold; finalBurstDuration = group.burstSpawnTotalDuration; }
        else { finalBurstThreshold = this.burstSpawnThreshold; finalBurstDuration = this.burstSpawnTotalDuration; }
        bool isBurstSpawn = group.count > finalBurstThreshold;
        float interval;
        if (isBurstSpawn) { interval = finalBurstDuration / group.count; }
        else { interval = group.spawnIntervalWithinGroup > 0 ? group.spawnIntervalWithinGroup : 0.1f; }

        if (group.formation == FormationType.None)
        {
            for (int i = 0; i < group.count; i++)
            {
                if (playerTransform == null) yield break;
                if (TryFindValidSpawnPoint(out Vector3 spawnPosition, out Quaternion spawnRotation, group.directionHint, isStampede, false))
                {
                    StartCoroutine(SpawnSingleEnemyWithWarning(spawnPosition, spawnRotation, group, waveNum, healthG, damageG, speedG));
                }
                else { GameTimelineManager.Instance?.AnEnemyFailedToSpawn(); }
                if (interval > 0) { yield return new WaitForSeconds(interval); }
            }
        }
        else
        {
            if (!TryFindValidSpawnPoint(out Vector3 anchorPos, out Quaternion anchorRot, group.directionHint, isStampede, true))
            {
                Debug.LogError($"阵型生成失败: 未能找到锚点!");
                for (int i = 0; i < group.count; i++) GameTimelineManager.Instance?.AnEnemyFailedToSpawn();
                yield break;
            }

            Vector3 forwardDir = anchorRot * Vector3.forward;
            Vector3 rightDir;
            switch (group.directionHint)
            {
                case SpawnDirectionHint.North:
                case SpawnDirectionHint.South:
                    rightDir = Vector3.right; break;
                case SpawnDirectionHint.East:
                case SpawnDirectionHint.West:
                    rightDir = Vector3.forward; break;
                case SpawnDirectionHint.Northeast:
                case SpawnDirectionHint.Northwest:
                case SpawnDirectionHint.Southeast:
                case SpawnDirectionHint.Southwest:
                case SpawnDirectionHint.Random:
                default:
                    rightDir = anchorRot * Vector3.right; break;
            }

            for (int i = 0; i < group.count; i++)
            {
                if (playerTransform == null) yield break;

                Vector3 spawnPositionOffset = Vector3.zero;
                switch (group.formation)
                {
                    case FormationType.Line:
                        float lineOffset = (i - (group.count - 1) / 2.0f) * group.formationSpacing;
                        spawnPositionOffset = (rightDir * lineOffset);
                        break;
                    case FormationType.V_Shape:
                        float half = (group.count - 1) / 2.0f;
                        float vOffset = (i - half) * group.formationSpacing;
                        spawnPositionOffset = (rightDir * vOffset) + (forwardDir * Mathf.Abs(vOffset) * group.vShapeDepthFactor);
                        break;
                    case FormationType.Grid:
                        if (group.gridColumns <= 0) group.gridColumns = 1;
                        int row = i / group.gridColumns;
                        int col = i % group.gridColumns;
                        float xOffset = (col - (group.gridColumns - 1) / 2.0f) * group.formationSpacing;
                        float zOffset = row * group.formationSpacing;
                        spawnPositionOffset = (rightDir * xOffset) - (forwardDir * zOffset);
                        break;
                }

                // --- vvv 核心修复 vvv ---
                // (旧代码: spawnPositionOffset -= (forwardDir * group.formationOffset);)
                // 【新代码】将偏移量应用到“横向” (rightDir)，即您图中的红色箭头方向
                spawnPositionOffset += (rightDir * group.formationOffset);
                // --- ^^^ 修复结束 ^^^ ---

                Vector3 finalSpawnPos = anchorPos + spawnPositionOffset;

                if (IsSpawnPointStillValid(finalSpawnPos, playerTransform.position, out Vector3 validatedPos, out Quaternion validatedRot, false, anchorRot))
                {
                    StartCoroutine(SpawnSingleEnemyWithWarning(validatedPos, anchorRot, group, waveNum, healthG, damageG, speedG));
                }
                else
                {
                    // 地面射线失败时，使用原定 XZ 坐标 + 锚点 Y 高度，保持阵型不错位
                    Vector3 fallbackPos = new Vector3(finalSpawnPos.x, anchorPos.y, finalSpawnPos.z);
                    StartCoroutine(SpawnSingleEnemyWithWarning(fallbackPos, anchorRot, group, waveNum, healthG, damageG, speedG));
                }
                if (interval > 0) { yield return new WaitForSeconds(interval); }
            }
        }
    }

    private IEnumerator SpawnSingleEnemyWithWarning(Vector3 position, Quaternion rotation, EnemySpawnGroup group, int waveNum, float healthG, float damageG, float speedG)
    {
        EnemyType type = group.enemyType;
        if (type.aiType == AIType.StraightLineStampede)
        {
            if (enemySpawnWarningDuration > 0) { yield return new WaitForSeconds(enemySpawnWarningDuration); }
        }
        else
        {
            if (enemySpawnWarningPrefab != null && enemySpawnWarningDuration > 0)
            {
                GameObject warningEffect = Instantiate(enemySpawnWarningPrefab, position + Vector3.up * 0.1f, Quaternion.identity);
                Destroy(warningEffect, enemySpawnWarningDuration);
            }
            if (enemySpawnWarningDuration > 0) { yield return new WaitForSeconds(enemySpawnWarningDuration); }
        }
        float baseHealth, baseDamage, baseSpeed;
        Vector3 scale = Vector3.one;
        bool spawnAsElite = false; // 追踪是否为精英怪
        if (group.overrideStats) { baseHealth = type.baseHealth * group.statOverrides.healthMultiplier; baseDamage = type.baseDamage * group.statOverrides.damageMultiplier; baseSpeed = type.baseSpeed * group.statOverrides.speedMultiplier; scale = group.statOverrides.scale; }
        else if (group.isElite && type.canBeElite) { baseHealth = type.baseHealth * type.eliteHealthMultiplier; baseDamage = type.baseDamage * type.eliteDamageMultiplier; baseSpeed = type.baseSpeed * type.eliteSpeedMultiplier; scale = type.eliteScale; spawnAsElite = true; }
        else { baseHealth = type.baseHealth; baseDamage = type.baseDamage; baseSpeed = type.baseSpeed; }
        float finalHealth = baseHealth * (1f + (waveNum - 1) * healthG);
        float finalDamage = baseDamage * (1f + (waveNum - 1) * damageG);
        float finalSpeed = baseSpeed * (1f + (waveNum - 1) * speedG);
        Vector3 moveDirection = rotation * Vector3.forward;
        Quaternion spawnRotation;
        if (type.aiType == AIType.StraightLineStampede) { spawnRotation = Quaternion.LookRotation(moveDirection); }
        else { spawnRotation = rotation; }
        GameObject enemyGO = Instantiate(type.enemyPrefab, position, spawnRotation);
        enemyGO.transform.localScale = scale;
        switch (type.aiType)
        {
            case AIType.StraightLineStampede:
                StraightMoverAI moverAI = enemyGO.GetComponent<StraightMoverAI>();
                if (moverAI != null) { moverAI.Initialize(finalSpeed, type.lifetime, moveDirection, Mathf.RoundToInt(finalDamage)); }
                else { Debug.LogWarning($"怪物 {type.name} 被标记为 StraightLineStampede 但缺少 StraightMoverAI 脚本！", enemyGO); }
                enemyGO.GetComponent<Health>()?.InitializeHealth(Mathf.RoundToInt(finalHealth), type, spawnAsElite);
                break;

            case AIType.Pinball: //
                PinballAI pinballAI = enemyGO.GetComponent<PinballAI>();
                if (pinballAI != null)
                {
                    // [!] 修复：使用 'type.lifetime' 和 'Mathf.RoundToInt(finalDamage)'
                    pinballAI.Initialize(finalSpeed, type.lifetime, Mathf.RoundToInt(finalDamage)); //
                }
                else
                {
                    Debug.LogError($"怪物预制件 {enemyGO.name} 缺少 PinballAI 脚本！"); //
                }
                // [!] 修复：Pinball 怪物也需要初始化 Health
                enemyGO.GetComponent<Health>()?.InitializeHealth(Mathf.RoundToInt(finalHealth), type, spawnAsElite); //
                break;

            case AIType.Chasing:
            default:
                if (type.isSuicideBomber) { enemyGO.GetComponent<EnemyExplosionAttack>()?.Initialize(type); }
                enemyGO.GetComponent<Health>()?.InitializeHealth(Mathf.RoundToInt(finalHealth), type, spawnAsElite);
                enemyGO.GetComponent<EnemyAI>()?.InitializeEnemy(finalSpeed, Mathf.RoundToInt(finalDamage));
                if (type.isBoss && enemyGO.GetComponent<Health>() != null) { WaveManager.Instance?.RegisterBossInstance(enemyGO.GetComponent<Health>()); }
                break;


        }
        Animator animator = enemyGO.GetComponentInChildren<Animator>();
        if (animator != null) { AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0); animator.Play(stateInfo.fullPathHash, 0, Random.Range(0f, 1f)); }
    }

    bool TryFindValidSpawnPoint(out Vector3 position, out Quaternion rotation, SpawnDirectionHint hint = SpawnDirectionHint.Random, bool useStampedeRadius = false, bool forceCenterAngle = false)
    {
        float minAngle, maxAngle;
        if (forceCenterAngle)
        {
            float centerAngle = 0f;
            switch (hint)
            {
                case SpawnDirectionHint.North: centerAngle = 0f; break;
                case SpawnDirectionHint.Northeast: centerAngle = 45f; break;
                case SpawnDirectionHint.East: centerAngle = 90f; break;
                case SpawnDirectionHint.Southeast: centerAngle = 135f; break;
                case SpawnDirectionHint.South: centerAngle = 180f; break;
                case SpawnDirectionHint.Southwest: centerAngle = 225f; break;
                case SpawnDirectionHint.West: centerAngle = 270f; break;
                case SpawnDirectionHint.Northwest: centerAngle = 315f; break;
                case SpawnDirectionHint.Random: centerAngle = Random.Range(0f, 360f); break;
            }
            minAngle = centerAngle;
            maxAngle = centerAngle;
        }
        else
        {
            switch (hint)
            {
                case SpawnDirectionHint.North: minAngle = -22.5f; maxAngle = 22.5f; break;
                case SpawnDirectionHint.Northeast: minAngle = 22.5f; maxAngle = 67.5f; break;
                case SpawnDirectionHint.East: minAngle = 67.5f; maxAngle = 112.5f; break;
                case SpawnDirectionHint.Southeast: minAngle = 112.5f; maxAngle = 157.5f; break;
                case SpawnDirectionHint.South: minAngle = 157.5f; maxAngle = 202.5f; break;
                case SpawnDirectionHint.Southwest: minAngle = 202.5f; maxAngle = 247.5f; break;
                case SpawnDirectionHint.West: minAngle = 247.5f; maxAngle = 292.5f; break;
                case SpawnDirectionHint.Northwest: minAngle = 292.5f; maxAngle = 337.5f; break;
                case SpawnDirectionHint.Random: default: minAngle = 0f; maxAngle = 360f; break;
            }
        }

        float minR, maxR;
        if (useStampedeRadius)
        {
            switch (hint)
            {
                case SpawnDirectionHint.North:
                case SpawnDirectionHint.East:
                case SpawnDirectionHint.South:
                case SpawnDirectionHint.West:
                    minR = stampedeCardinalRadiusMin; maxR = stampedeCardinalRadiusMax; break;
                case SpawnDirectionHint.Northeast:
                case SpawnDirectionHint.Southeast:
                case SpawnDirectionHint.Southwest:
                case SpawnDirectionHint.Northwest:
                    minR = stampedeDiagonalRadiusMin; maxR = stampedeDiagonalRadiusMax; break;
                case SpawnDirectionHint.Random:
                default:
                    minR = stampedeCardinalRadiusMin; maxR = stampedeCardinalRadiusMax; break;
            }
        }
        else { minR = spawnRadiusMin; maxR = spawnRadiusMax; }

        if (TryFindValidSpawnPointInAnnulus(playerTransform.position, minR, maxR, maxPrimarySpawnAttempts, out position, out rotation, minAngle, maxAngle, useStampedeRadius) ||
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
            { return true; }
        }
        return false;
    }

    bool TryFindValidSpawnPointInAnnulus(Vector3 center, float minRadius, float maxRadius, int attempts,
                                     out Vector3 foundPosition, out Quaternion foundRotation,
                                     float minAngleDegrees = 0f, float maxAngleDegrees = 360f,
                                     bool ignoreMapBounds = false)
    {
        foundPosition = Vector3.zero; foundRotation = Quaternion.identity;
        if (playerTransform == null) { AcquirePlayerReference(); if (playerTransform == null) { return false; } }

        for (int i = 0; i < attempts; i++)
        {
            float randomAngleDegrees;
            if (minAngleDegrees == maxAngleDegrees) { randomAngleDegrees = minAngleDegrees; }
            else { randomAngleDegrees = Random.Range(minAngleDegrees, maxAngleDegrees); }

            float randomAngleRadians = randomAngleDegrees * Mathf.Deg2Rad;
            Vector3 spawnDirection = new Vector3(Mathf.Sin(randomAngleRadians), 0, Mathf.Cos(randomAngleRadians));
            float randomDistance = Random.Range(minRadius, maxRadius);
            Vector3 potentialSpawnPointXZ = center + spawnDirection * randomDistance;

            // 特殊机制怪物（如奔袭、弹球）不受地图边界限制
            if (!ignoreMapBounds)
            {
                potentialSpawnPointXZ = ClampToMapBounds(potentialSpawnPointXZ);
            }

            Vector3 rayOrigin = new Vector3(potentialSpawnPointXZ.x, playerTransform.position.y + raycastStartYOffset, potentialSpawnPointXZ.z);
            RaycastHit hitInfo;
            if (Physics.Raycast(rayOrigin, Vector3.down, out hitInfo, maxRaycastDistance, groundLayerMask))
            {
                foundPosition = hitInfo.point + Vector3.up * enemyPivotOffsetY;
                Vector3 directionToPlayer = (playerTransform.position - foundPosition);
                directionToPlayer.y = 0;
                if (directionToPlayer.sqrMagnitude > 0.01f) { foundRotation = Quaternion.LookRotation(directionToPlayer.normalized); }
                else { foundRotation = Quaternion.identity; }
                return true;
            }
        }
        return false;
    }

    bool IsSpawnPointStillValid(Vector3 potentialSpawnBase, Vector3 playerPositionForOrientation, out Vector3 validatedPosition, out Quaternion validatedRotation, bool usePotentialPointYForRayOrigin = false, Quaternion defaultRotation = default)
    {
        validatedPosition = potentialSpawnBase; validatedRotation = defaultRotation == default ? Quaternion.identity : defaultRotation;
        float rayOriginYBase = usePotentialPointYForRayOrigin ? potentialSpawnBase.y : playerPositionForOrientation.y;
        Vector3 rayOrigin = new Vector3(potentialSpawnBase.x, rayOriginYBase + raycastStartYOffset, potentialSpawnBase.z);
        RaycastHit hitInfo;
        if (Physics.Raycast(rayOrigin, Vector3.down, out hitInfo, maxRaycastDistance, groundLayerMask))
        {
            validatedPosition = hitInfo.point + (Vector3.up * enemyPivotOffsetY);
            Vector3 directionToPlayer = (playerPositionForOrientation - validatedPosition);
            directionToPlayer.y = 0;
            if (directionToPlayer.sqrMagnitude > 0.01f) { validatedRotation = Quaternion.LookRotation(directionToPlayer.normalized); }
            return true;
        }
        return false;
    }

    /// <summary>
    /// 将坐标点钳制到地图圆形边界内（忽略Y轴）
    /// </summary>
    Vector3 ClampToMapBounds(Vector3 point)
    {
        if (mapCenter == null) return point; // 未设置中心则不限制

        Vector3 center = mapCenter.position;
        // 只在XZ平面上计算距离
        Vector3 offset = new Vector3(point.x - center.x, 0, point.z - center.z);
        float distFromCenter = offset.magnitude;

        if (distFromCenter > mapRadius)
        {
            // 超出边界，将点拉回到圆的边缘内侧（留1单位余量避免贴边）
            Vector3 clampedOffset = offset.normalized * (mapRadius - 1f);
            point = new Vector3(center.x + clampedOffset.x, point.y, center.z + clampedOffset.z);
        }

        return point;
    }

    public void StopAndClearSpawning()
    {
        if (_currentSpawnRoutine != null)
        {
            StopCoroutine(_currentSpawnRoutine);
            _currentSpawnRoutine = null;
        }
        StopAllCoroutines();
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

        enemyGO.GetComponent<Health>()?.InitializeHealth(Mathf.RoundToInt(baseHealth), enemyTypeToSpawn, enemyTypeToSpawn.canBeElite);
        enemyGO.GetComponent<EnemyAI>()?.InitializeEnemy(baseSpeed, Mathf.RoundToInt(baseDamage));

        WaveManager.Instance?.RegisterDebugEnemy();
    }
}