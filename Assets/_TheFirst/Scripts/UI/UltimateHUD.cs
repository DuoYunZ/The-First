using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大招HUD - 队列式显示大招图标
/// 武器能量满时依次在队列末尾添加图标，释放后移除
/// </summary>
public class UltimateHUD : MonoBehaviour
{
    public static UltimateHUD Instance { get; private set; }

    [Header("UI 引用")]
    [Tooltip("大招图标容器（如 HorizontalLayoutGroup）")]
    public Transform iconContainer;
    [Tooltip("大招图标预制件（子物体中需包含名为 Icon_weapon 的 Image）")]
    public GameObject ultimateIconPrefab;

    // 队列：记录当前显示的图标和对应的武器
    private List<QueueEntry> iconQueue = new List<QueueEntry>();

    private class QueueEntry
    {
        public WeaponPart weapon;
        public GameObject iconGO;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (UltimateManager.Instance == null || WeaponController.Instance == null) return;
        SyncQueue();
    }

    /// <summary>
    /// 每帧同步队列：新满能量的武器追加到末尾，不再满的移除
    /// </summary>
    private void SyncQueue()
    {
        var controller = WeaponController.Instance;

        // 收集当前所有满能量的武器
        List<WeaponPart> currentCharged = new List<WeaponPart>();

        if (controller.builtInBladeWeapon != null && controller.builtInBladeWeapon.IsEnergyFull)
            currentCharged.Add(controller.builtInBladeWeapon);

        foreach (var owned in controller.ownedWeapons)
        {
            if (owned.weaponPartInstance != null && owned.weaponPartInstance.IsEnergyFull)
                currentCharged.Add(owned.weaponPartInstance);
        }

        // 移除：队列中不再满能量的
        for (int i = iconQueue.Count - 1; i >= 0; i--)
        {
            if (!currentCharged.Contains(iconQueue[i].weapon))
            {
                if (iconQueue[i].iconGO != null) Destroy(iconQueue[i].iconGO);
                iconQueue.RemoveAt(i);
            }
        }

        // 添加：满能量但不在队列中的追加到末尾
        foreach (var weapon in currentCharged)
        {
            if (!QueueContains(weapon))
            {
                AddToQueue(weapon);
            }
        }
    }

    private bool QueueContains(WeaponPart weapon)
    {
        foreach (var entry in iconQueue)
        {
            if (entry.weapon == weapon) return true;
        }
        return false;
    }

    private void AddToQueue(WeaponPart weapon)
    {
        if (iconContainer == null || ultimateIconPrefab == null) return;

        GameObject go = Instantiate(ultimateIconPrefab, iconContainer);

        // 查找子物体 Icon_weapon 并替换图标
        Transform iconChild = go.transform.Find("Icon_weapon");
        if (iconChild != null)
        {
            Image img = iconChild.GetComponent<Image>();
            if (img != null)
            {
                Sprite icon = weapon.StatBlock.ultimateIcon;
                if (icon == null) icon = weapon.StatBlock.weaponIcon;
                img.sprite = icon;
                img.enabled = true;
            }
        }

        go.SetActive(true);

        // --- 自动附加拖拽组件（同时支持 Tooltip 悬停） ---
        UltimateIconDraggable draggable = go.GetComponent<UltimateIconDraggable>();
        if (draggable == null) draggable = go.AddComponent<UltimateIconDraggable>();

        // 传入武器引用，用于 Tooltip 显示大招描述
        draggable.weapon = weapon;

        iconQueue.Add(new QueueEntry
        {
            weapon = weapon,
            iconGO = go
        });

        // 更新索引
        draggable.queueIndex = iconQueue.Count - 1;

    }

    // 连线管理
    private List<GameObject> connectionLines = new List<GameObject>();

    /// <summary>
    /// 获取当前队列（大招管理器读取用）
    /// </summary>
    public List<WeaponPart> GetQueue()
    {
        List<WeaponPart> result = new List<WeaponPart>();
        foreach (var entry in iconQueue)
        {
            result.Add(entry.weapon);
        }
        return result;
    }

    /// <summary>
    /// 从队列中移除指定武器的图标（释放大招后调用）
    /// </summary>
    public void RemoveFromQueue(WeaponPart weapon)
    {
        for (int i = iconQueue.Count - 1; i >= 0; i--)
        {
            if (iconQueue[i].weapon == weapon)
            {
                if (iconQueue[i].iconGO != null) Destroy(iconQueue[i].iconGO);
                iconQueue.RemoveAt(i);
                break;
            }
        }
        UpdateQueueIndices();
    }

    private void UpdateQueueIndices()
    {
        for (int i = 0; i < iconQueue.Count; i++)
        {
            var draggable = iconQueue[i].iconGO.GetComponent<UltimateIconDraggable>();
            if (draggable != null) draggable.queueIndex = i;
        }
    }

    private void LateUpdate()
    {
        // 我们在LateUpdate里画线，确保UI布局已更新
        UpdateConnectionLines();
    }

    private void UpdateConnectionLines()
    {
        // 先清理所有连携装饰（连线 + 边框）
        CleanupComboDecorations();

        if (UltimateManager.Instance == null || iconQueue.Count < 2) return;

        for (int i = 0; i < iconQueue.Count - 1; i++)
        {
            WeaponPart w1 = iconQueue[i].weapon;
            WeaponPart w2 = iconQueue[i + 1].weapon;

            bool hasCombo = false;
            foreach (var combo in UltimateManager.Instance.comboUltimates)
            {
                if (combo != null && combo.MatchesWeapons(w1.StatBlock, w2.StatBlock))
                {
                    hasCombo = true;
                    break;
                }
            }

            if (hasCombo && iconQueue[i].iconGO != null && iconQueue[i + 1].iconGO != null)
            {
                CreateComboLine(
                    iconQueue[i].iconGO.GetComponent<RectTransform>(), 
                    iconQueue[i + 1].iconGO.GetComponent<RectTransform>()
                );
            }
        }
    }

    private void CreateComboLine(RectTransform rtA, RectTransform rtB)
    {
        // === 主连线（发光效果） ===
        // 外层光晕线（宽、半透明）
        GameObject glowObj = new GameObject("ComboGlow", typeof(RectTransform), typeof(Image));
        glowObj.transform.SetParent(iconContainer.parent, false);
        glowObj.transform.SetAsFirstSibling();

        Image glowImg = glowObj.GetComponent<Image>();
        // 呼吸动画颜色：用时间驱动的 alpha 脉冲
        float pulse = Mathf.Sin(Time.unscaledTime * 4f) * 0.3f + 0.5f;
        glowImg.color = new Color(1f, 0.5f, 0f, pulse * 0.4f); // 橙色光晕

        SetupLineTransform(glowObj.GetComponent<RectTransform>(), rtA, rtB, 24f); // 宽光晕
        connectionLines.Add(glowObj);

        // 内层核心线（窄、明亮）
        GameObject coreObj = new GameObject("ComboCore", typeof(RectTransform), typeof(Image));
        coreObj.transform.SetParent(iconContainer.parent, false);
        coreObj.transform.SetAsFirstSibling();

        Image coreImg = coreObj.GetComponent<Image>();
        coreImg.color = new Color(1f, 0.85f, 0.3f, 0.9f); // 亮金色

        SetupLineTransform(coreObj.GetComponent<RectTransform>(), rtA, rtB, 6f); // 窄核心
        connectionLines.Add(coreObj);

        // === 连携图标发光高亮边框 ===
        AddGlowBorder(rtA.gameObject);
        AddGlowBorder(rtB.gameObject);
    }

    private void SetupLineTransform(RectTransform rt, RectTransform rtA, RectTransform rtB, float thickness)
    {
        rt.pivot = new Vector2(0, 0.5f);
        Vector3 posA = rtA.position;
        Vector3 posB = rtB.position;
        rt.position = posA;

        Vector3 dir = posB - posA;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.rotation = Quaternion.Euler(0, 0, angle);
        rt.sizeDelta = new Vector2(distance, thickness);
    }

    private void AddGlowBorder(GameObject iconGO)
    {
        // 避免重复添加
        Outline existing = iconGO.GetComponent<Outline>();
        if (existing != null) return;

        Outline outline = iconGO.AddComponent<Outline>();
        float pulse = Mathf.Sin(Time.unscaledTime * 3f) * 0.3f + 0.7f;
        outline.effectColor = new Color(1f, 0.7f, 0f, pulse);
        outline.effectDistance = new Vector2(4f, 4f);

        // 标记为连携边框，每帧清理时一起移除
        outline.name = "ComboOutline";
        connectionLines.Add(null); // 占位，实际清理在下面
    }

    /// <summary>
    /// 清理连携装饰（连线 + 发光边框）
    /// </summary>
    private void CleanupComboDecorations()
    {
        foreach (var line in connectionLines) if (line != null) Destroy(line);
        connectionLines.Clear();

        // 移除所有图标上的 Outline 组件（连携高亮用）
        foreach (var entry in iconQueue)
        {
            if (entry.iconGO == null) continue;
            Outline o = entry.iconGO.GetComponent<Outline>();
            if (o != null) Destroy(o);
        }
    }

    public int FindClosestIconIndex(Vector3 dropPos, int ignoreIndex)
    {
        int closestIndex = -1;
        float minDistance = float.MaxValue;
        float threshold = 150f; // 吸附阈值

        for (int i = 0; i < iconQueue.Count; i++)
        {
            if (i == ignoreIndex) continue;
            float dist = Vector3.Distance(dropPos, iconQueue[i].iconGO.GetComponent<RectTransform>().position);
            if (dist < minDistance && dist < threshold)
            {
                minDistance = dist;
                closestIndex = i;
            }
        }
        return closestIndex;
    }

    public void SwapQueuePositions(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= iconQueue.Count || indexB < 0 || indexB >= iconQueue.Count) return;

        // 交换队列元素
        var temp = iconQueue[indexA];
        iconQueue[indexA] = iconQueue[indexB];
        iconQueue[indexB] = temp;

        // 交换 UI 上的 SiblingIndex
        for (int i = 0; i < iconQueue.Count; i++)
        {
            iconQueue[i].iconGO.transform.SetSiblingIndex(i);
        }

        // 【修复重叠】强制刷新 LayoutGroup 布局
        LayoutGroup layout = iconContainer.GetComponent<LayoutGroup>();
        if (layout != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(iconContainer as RectTransform);
        }

        // 更新下标
        UpdateQueueIndices();
    }
}

/// <summary>
/// 大招图标可拖拽组件 - 拖拽交换队列位置 + 鼠标悬停 Tooltip
/// 动态挂载到生成的大招图标预制件上
/// </summary>
public class UltimateIconDraggable : MonoBehaviour,
    UnityEngine.EventSystems.IBeginDragHandler,
    UnityEngine.EventSystems.IDragHandler,
    UnityEngine.EventSystems.IEndDragHandler,
    UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler
{
    [HideInInspector] public int queueIndex; // 在队列中的索引
    [HideInInspector] public WeaponPart weapon; // 对应的武器引用（用于读取大招描述）

    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector3 originalPosition;
    private Transform originalParent;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 向上找Canvas
        canvas = GetComponentInParent<Canvas>();
    }

    // ===== 鼠标悬停 Tooltip =====

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (TooltipUI.Instance == null || weapon == null || weapon.StatBlock == null) return;

        string desc = null;

        // 优先检测连携：如果队列前两个能组成连携，且当前图标是其中之一，则显示连携描述
        if (UltimateHUD.Instance != null && UltimateManager.Instance != null)
        {
            var queue = UltimateHUD.Instance.GetQueue();
            if (queue.Count >= 2 && (weapon == queue[0] || weapon == queue[1]))
            {
                // 检查是否能组成连携
                foreach (var combo in UltimateManager.Instance.comboUltimates)
                {
                    if (combo != null && combo.MatchesWeapons(queue[0].StatBlock, queue[1].StatBlock))
                    {
                        // 找到连携，使用连携描述
                        desc = combo.comboDescription;
                        if (LocalizationManager.CurrentLanguage == SystemLanguage.English
                            && !string.IsNullOrEmpty(combo.comboDescriptionEN))
                        {
                            desc = combo.comboDescriptionEN;
                        }
                        // 在描述前附加连携名称
                        if (!string.IsNullOrEmpty(desc))
                        {
                            string comboTitle = combo.comboName;
                            desc = $"<b>{comboTitle}</b>\n{desc}";
                        }
                        break;
                    }
                }
            }
        }

        // 没有连携描述时，回退到单体大招描述
        if (string.IsNullOrEmpty(desc))
        {
            desc = weapon.StatBlock.ultimateDescription;
            if (LocalizationManager.CurrentLanguage == SystemLanguage.English
                && !string.IsNullOrEmpty(weapon.StatBlock.ultimateDescriptionEN))
            {
                desc = weapon.StatBlock.ultimateDescriptionEN;
            }
        }

        if (string.IsNullOrEmpty(desc)) return;

        // 使用图标顶部中心作为气泡锚点（固定位置，不随鼠标变化）
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Vector2 topCenter = new Vector2(
            (corners[1].x + corners[2].x) * 0.5f,
            corners[1].y);
        TooltipUI.Instance.Show(desc, topCenter);
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (TooltipUI.Instance != null)
            TooltipUI.Instance.Hide();
    }

    void OnDestroy()
    {
        // 图标被销毁时（释放大招后），确保隐藏残留的气泡
        if (TooltipUI.Instance != null)
            TooltipUI.Instance.Hide();
    }

    // ===== 拖拽逻辑 =====

    public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
    {
        // 只响应左键拖拽
        if (eventData.button != UnityEngine.EventSystems.PointerEventData.InputButton.Left) return;

        // 拖拽开始时隐藏 Tooltip
        if (TooltipUI.Instance != null)
            TooltipUI.Instance.Hide();

        originalPosition = rectTransform.position;
        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false; // 允许检测下方物体
        canvasGroup.alpha = 0.7f; // 半透明表示拖拽中

        // 脱离 LayoutGroup 防止布局冲突，移到 Canvas 根层
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
            transform.SetParent(rootCanvas.transform, true);
    }

    public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (eventData.button != UnityEngine.EventSystems.PointerEventData.InputButton.Left) return;

        // 跟随鼠标移动
        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (eventData.button != UnityEngine.EventSystems.PointerEventData.InputButton.Left) return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        // 【修复】先保存拖拽结束时的屏幕位置，再回到容器
        Vector3 dropPosition = rectTransform.position;

        // 回到原来的容器
        transform.SetParent(originalParent, false);

        // 用拖拽结束时保存的位置来检测目标图标
        if (UltimateHUD.Instance != null)
        {
            int targetIndex = UltimateHUD.Instance.FindClosestIconIndex(dropPosition, queueIndex);
            if (targetIndex >= 0 && targetIndex != queueIndex)
            {
                UltimateHUD.Instance.SwapQueuePositions(queueIndex, targetIndex);
                return;
            }
        }

        // 没有交换，恢复原位（让LayoutGroup重新排列）
        transform.SetSiblingIndex(queueIndex);
        LayoutRebuilder.ForceRebuildLayoutImmediate(originalParent as RectTransform);
    }
}
