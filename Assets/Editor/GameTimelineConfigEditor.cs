// 放在 Assets/Scripts/Editor/GameTimelineConfigEditor.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(GameTimelineConfig))]
public class GameTimelineConfigEditor : Editor
{
    private SerializedProperty totalGameDurationProp;
    private SerializedProperty timelineEventsProp;

    private void OnEnable()
    {
        totalGameDurationProp = serializedObject.FindProperty("totalGameDuration");
        timelineEventsProp = serializedObject.FindProperty("timelineEvents");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. 标题和总时长
        EditorGUILayout.LabelField("核心设置", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(totalGameDurationProp);

        float totalDuration = totalGameDurationProp.floatValue;
        EditorGUILayout.HelpBox($"游戏总时长: {totalDuration / 60f:F1} 分钟", MessageType.Info);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("时间轴事件列表", EditorStyles.boldLabel);

        // 2. 工具栏按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加新事件"))
        {
            timelineEventsProp.InsertArrayElementAtIndex(timelineEventsProp.arraySize);
        }
        if (GUILayout.Button("按时间自动排序"))
        {
            SortEvents();
        }
        EditorGUILayout.EndHorizontal();

        // 3. 自定义列表渲染
        if (timelineEventsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("列表为空，请添加事件。", MessageType.Warning);
        }
        else
        {
            for (int i = 0; i < timelineEventsProp.arraySize; i++)
            {
                SerializedProperty eventProp = timelineEventsProp.GetArrayElementAtIndex(i);
                DrawEventItem(eventProp, i);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEventItem(SerializedProperty eventProp, int index)
    {
        SerializedProperty useRandom = eventProp.FindPropertyRelative("useRandomTimeRange");
        SerializedProperty fixedTime = eventProp.FindPropertyRelative("fixedTriggerTime");
        SerializedProperty minTime = eventProp.FindPropertyRelative("minTriggerTime");
        SerializedProperty waveConfig = eventProp.FindPropertyRelative("waveToSpawn");
        SerializedProperty eventName = eventProp.FindPropertyRelative("eventName");

        // 获取显示用的时间字符串
        string timeStr;
        if (useRandom.boolValue)
            timeStr = $"{FormatTime(minTime.floatValue)} (Random)";
        else
            timeStr = $"{FormatTime(fixedTime.floatValue)}";

        // 获取波次名称
        string waveName = waveConfig.objectReferenceValue != null ? waveConfig.objectReferenceValue.name : "未分配波次";
        string label = $"[{timeStr}] {waveName}";
        if (!string.IsNullOrEmpty(eventName.stringValue)) label += $" - {eventName.stringValue}";

        // 绘制折叠页
        EditorGUILayout.BeginVertical("box");
        eventProp.isExpanded = EditorGUILayout.Foldout(eventProp.isExpanded, label, true);

        if (eventProp.isExpanded)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(eventName);
            EditorGUILayout.PropertyField(waveConfig);
            EditorGUILayout.PropertyField(useRandom);
            if (useRandom.boolValue)
            {
                EditorGUILayout.PropertyField(eventProp.FindPropertyRelative("minTriggerTime"));
                EditorGUILayout.PropertyField(eventProp.FindPropertyRelative("maxTriggerTime"));
            }
            else
            {
                EditorGUILayout.PropertyField(fixedTime);
            }

            // 删除按钮
            if (GUILayout.Button("删除此事件"))
            {
                timelineEventsProp.DeleteArrayElementAtIndex(index);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
    }

    private void SortEvents()
    {
        // 由于 SerializedProperty 很难直接排序，我们需要通过操作底层对象来排序
        GameTimelineConfig config = (GameTimelineConfig)target;
        if (config.timelineEvents != null)
        {
            // 简单的按 fixedTriggerTime 排序 (如果用随机时间，取 minTriggerTime)
            config.timelineEvents = config.timelineEvents
                .OrderBy(e => e.useRandomTimeRange ? e.minTriggerTime : e.fixedTriggerTime)
                .ToList();

            // 标记对象已脏，需要保存
            EditorUtility.SetDirty(config);
        }
    }

    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60);
        int s = Mathf.FloorToInt(seconds % 60);
        return $"{m:00}:{s:00}";
    }
}