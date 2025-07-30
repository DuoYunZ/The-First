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
                    GameObject enemyGO = Instantiate(group.enemyType.enemyPrefab, spawnPosition, spawnRotation);
                    Health healthScript = enemyGO.GetComponent<Health>();
                    EnemyAI aiScript = enemyGO.GetComponent<EnemyAI>();
                    Animator animator = enemyGO.GetComponentInChildren<Animator>();

                    if (animator != null)
                    {
                        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                        animator.Play(stateInfo.fullPathHash, 0, Random.Range(0f, 1f));
                    }

                    int scaledHealth = Mathf.RoundToInt(group.enemyType.baseHealth * (1f + (waveNum - 1) * healthG));
                    float scaledSpeed = group.enemyType.baseSpeed * (1f + (waveNum - 1) * speedG);
                    int scaledDamage = Mathf.RoundToInt(group.enemyType.baseDamage * (1f + (waveNum - 1) * damageG));

                    if (group.isElite && group.enemyType.canBeElite)
                    {
                        scaledHealth = Mathf.RoundToInt(scaledHealth * group.enemyType.eliteHealthMultiplier);
                        scaledSpeed *= group.enemyType.eliteSpeedMultiplier;
                        scaledDamage = Mathf.RoundToInt(scaledDamage * group.enemyType.eliteDamageMultiplier); // 这就是上次中断的地方
                        enemyGO.transform.localScale = group.enemyType.eliteScale;
                        Renderer enemyRenderer = enemyGO.GetComponentInChildren<Renderer>();
                        if (enemyRenderer != null) enemyRenderer.material.color = group.enemyType.eliteColorTint;
                    }

                    if (healthScript != null) healthScript.InitializeHealth(scaledHealth);
                    if (aiScript != null) aiScript.InitializeEnemy(scaledSpeed, scaledDamage);
                    if (group.enemyType.isBoss && healthScript != null)
                    {
                        WaveManager.Instance?.RegisterBossInstance(healthScript);
                    }
                }
                else
                {
                    Debug.LogError($"CRITICAL SPAWN FAILURE: 未能为波次 {waveNum} 的 {group.enemyType.name} 找到任何有效出生点!");
                    WaveManager.Instance?.AnEnemyFailedToSpawn();
                }

                float interval = group.spawnIntervalWithinGroup > 0 ? group.spawnIntervalWithinGroup : 0.1f;
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
}