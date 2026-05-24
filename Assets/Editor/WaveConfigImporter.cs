using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class WaveConfigImporter : EditorWindow
{
    // --- 路径已更新为你提供的路径 ---
    private const string ENEMY_TYPE_PATH = "Assets/_TheFirst/GameData/Enemies";
    private const string OUTPUT_PATH = "Assets/_TheFirst/GeneratedWaves";

    private string rawData = "";
    private GameTimelineConfig targetTimeline;

    [MenuItem("Tools/Wave Importer (Excel)")]
    public static void ShowWindow()
    {
        GetWindow<WaveConfigImporter>("Wave Importer");
    }

    void OnGUI()
    {
        GUILayout.Label("1. 设置目标时间轴 (Timeline Config)", EditorStyles.boldLabel);
        targetTimeline = (GameTimelineConfig)EditorGUILayout.ObjectField("Target Timeline", targetTimeline, typeof(GameTimelineConfig), false);

        GUILayout.Space(10);
        GUILayout.Label("2. 粘贴 Excel 数据 (不要带表头)", EditorStyles.boldLabel);
        GUILayout.Label("格式: TriggerTime | WaveName | Duration | EnemyName | Count | Interval");
        rawData = EditorGUILayout.TextArea(rawData, GUILayout.Height(200));

        GUILayout.Space(10);
        if (GUILayout.Button("生成波次并填充 Timeline", GUILayout.Height(40)))
        {
            if (targetTimeline == null)
            {
                EditorUtility.DisplayDialog("错误", "请先拖入目标 Timeline Config 文件！", "OK");
                return;
            }
            ImportData();
        }

        GUILayout.Space(5);
        GUILayout.Label($"注：EnemyType 搜索路径: {ENEMY_TYPE_PATH}");
        GUILayout.Label($"注：生成文件路径: {OUTPUT_PATH}");
    }

    void ImportData()
    {
        // 1. 准备目录
        if (!Directory.Exists(OUTPUT_PATH)) Directory.CreateDirectory(OUTPUT_PATH);

        // 2. 加载所有 EnemyType 以便查找
        string[] guids = AssetDatabase.FindAssets("t:EnemyType", new[] { ENEMY_TYPE_PATH });
        Dictionary<string, EnemyType> enemyDict = new Dictionary<string, EnemyType>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyType enemy = AssetDatabase.LoadAssetAtPath<EnemyType>(path);
            if (enemy != null)
            {
                // 使用文件名作为 Key (忽略大小写)
                enemyDict[enemy.name.ToLower()] = enemy;
            }
        }

        // 3. 解析文本
        string[] lines = rawData.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        Dictionary<string, WaveDataBuild> waveBuilder = new Dictionary<string, WaveDataBuild>();

        foreach (string line in lines)
        {
            string[] cols = line.Split('\t');
            if (cols.Length < 6) continue;

            // 简单的防错处理
            if (!float.TryParse(cols[0], out float time)) continue;
            string waveName = cols[1].Trim();
            if (!float.TryParse(cols[2], out float duration)) duration = 60f;
            string enemyName = cols[3].Trim();
            if (!int.TryParse(cols[4], out int count)) count = 1;
            if (!float.TryParse(cols[5], out float interval)) interval = 1f;

            if (!waveBuilder.ContainsKey(waveName))
            {
                waveBuilder[waveName] = new WaveDataBuild
                {
                    waveName = waveName,
                    triggerTime = time,
                    duration = duration,
                    groups = new List<EnemySpawnGroup>()
                };
            }

            if (enemyName == "-" || string.IsNullOrEmpty(enemyName)) continue;

            if (enemyDict.TryGetValue(enemyName.ToLower(), out EnemyType foundEnemy))
            {
                // --- 【修复点】这里使用了正确的字段名 ---
                waveBuilder[waveName].groups.Add(new EnemySpawnGroup
                {
                    enemyType = foundEnemy,
                    count = count,
                    spawnIntervalWithinGroup = interval,   // 修正：对应 EnemySpawnGroup.cs 里的变量名
                    delayAfterPreviousGroupStarts = 0f,     // 修正：对应 EnemySpawnGroup.cs 里的变量名

                    // 其他字段保持默认
                    formation = EnemySpawnGroup.FormationType.None,
                    gridColumns = 5,
                    formationSpacing = 2f
                });
            }
            else
            {
                Debug.LogWarning($"找不到名为 '{enemyName}' 的 EnemyType 文件！跳过此条目。");
            }
        }

        // 4. 创建 ScriptableObjects
        List<TimelineEvent> newEvents = new List<TimelineEvent>();

        foreach (var kvp in waveBuilder)
        {
            WaveDataBuild data = kvp.Value;

            WaveConfig waveConfig = ScriptableObject.CreateInstance<WaveConfig>();
            waveConfig.waveName = data.waveName;
            waveConfig.maxWaveDuration = data.duration;
            waveConfig.enemyGroups = data.groups;

            string assetPath = $"{OUTPUT_PATH}/{data.waveName}.asset";
            // 如果文件已存在，覆盖它
            AssetDatabase.CreateAsset(waveConfig, assetPath);

            TimelineEvent evt = new TimelineEvent();
            evt.eventName = data.waveName;
            evt.waveToSpawn = waveConfig;
            evt.fixedTriggerTime = data.triggerTime;
            evt.useRandomTimeRange = false;

            newEvents.Add(evt);
        }

        AssetDatabase.SaveAssets();

        // 5. 更新 Timeline
        targetTimeline.timelineEvents = newEvents;
        EditorUtility.SetDirty(targetTimeline);

        Debug.Log($"<color=green>成功导入！生成了 {newEvents.Count} 个波次文件至 {OUTPUT_PATH}，并已更新 Timeline。</color>");
    }

    class WaveDataBuild
    {
        public string waveName;
        public float triggerTime;
        public float duration;
        public List<EnemySpawnGroup> groups;
    }
}