// WeaponUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponUI : MonoBehaviour
{
    public static WeaponUI Instance { get; private set; }

    [Header("UI 引用")]
    [Tooltip("放置图标的父容器 (Horizontal Layout Group)")]
    public Transform iconContainer;
    [Tooltip("图标预制件 (包含 Image 组件)")]
    public GameObject iconPrefab;

    [Header("能量条设置")]
    [Tooltip("能量条填充颜色")]
    public Color energyBarColor = new Color(0.2f, 0.8f, 1f, 1f);
    [Tooltip("能量条满时的颜色")]
    public Color energyBarFullColor = new Color(1f, 0.9f, 0.2f, 1f);

    [Header("设置")]
    public int maxIcons = 6;
    [Tooltip("只显示已装备的武器槽位")]
    public bool hideEmptySlots = true;

    // 内部类：武器槽位
    private class WeaponSlot
    {
        public GameObject rootObject;
        public Image backgroundImage;
        public Image iconImage;
        public Image xpBarFill;
        public WeaponPart linkedWeapon;
        public float displayedFillAmount; // 用于平滑过渡
        public float lastEnergy; // 上次能量值，用于检测变化
    }

    private List<WeaponSlot> slots = new List<WeaponSlot>();

    /// <summary>
    /// 根据WeaponPart获取对应的UI槽位RectTransform
    /// </summary>
    public RectTransform GetSlotRectForWeapon(WeaponPart weapon)
    {
        foreach (var slot in slots)
        {
            if (slot.linkedWeapon == weapon && slot.rootObject != null)
            {
                return slot.rootObject.GetComponent<RectTransform>();
            }
        }
        return null;
    }

    /// <summary>
    /// 从世界位置发射XP粒子飞向目标武器的图标
    /// </summary>
    public void SpawnXpParticlesToWeapon(WeaponPart weapon, Vector3 worldPosition)
    {
        RectTransform targetSlot = GetSlotRectForWeapon(weapon);
        if (targetSlot != null && XpParticleManager.Instance != null)
        {
            XpParticleManager.Instance.SpawnXpParticles(worldPosition, targetSlot);
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        InitializeSlots();
        UpdateWeaponIcons();
    }

    private void InitializeSlots()
    {
        // 清空现有槽位
        foreach (Transform child in iconContainer) Destroy(child.gameObject);
        slots.Clear();

        for (int i = 0; i < maxIcons; i++)
        {
            GameObject go = Instantiate(iconPrefab, iconContainer);
            go.name = $"WeaponSlot_{i}";

            // 获取组件
            Image bg = go.GetComponent<Image>();
            Image icon = null;
            Image xpBar = null;

            // 查找图标 (第一个子节点)
            if (go.transform.childCount > 0)
            {
                icon = go.transform.GetChild(0).GetComponent<Image>();
            }

            // 递归查找经验条
            xpBar = FindXpBar(go.transform);

            if (bg != null && icon != null)
            {
                // 初始隐藏
                go.SetActive(false);

                slots.Add(new WeaponSlot
                {
                    rootObject = go,
                    backgroundImage = bg,
                    iconImage = icon,
                    xpBarFill = xpBar
                });

                // 调试日志
                Debug.Log($"[WeaponUI] 槽位{i} 初始化 - XpBar找到: {xpBar != null}");
            }
            else
            {
                Debug.LogError($"[WeaponUI] 槽位{i} 结构不对！需要根节点Image和子节点Image");
            }
        }
    }

    // 递归查找经验条
    private Image FindXpBar(Transform parent)
    {
        // 打印所有子节点帮助调试
        Debug.Log($"[WeaponUI] 查找XpBar - 父节点: {parent.name}, 子节点数: {parent.childCount}");
        
        // 使用GetComponentsInChildren查找所有Image
        Image[] allImages = parent.GetComponentsInChildren<Image>(true);
        foreach (Image img in allImages)
        {
            Debug.Log($"[WeaponUI] 发现Image: {img.gameObject.name}");
            
            // 检查名字是否包含xp或exp
            string lowerName = img.gameObject.name.ToLower();
            if (lowerName.Contains("xp") || lowerName.Contains("exp") || lowerName.Contains("bar"))
            {
                Debug.Log($"[WeaponUI] 找到经验条: {img.gameObject.name}");
                return img;
            }
        }
        
        return null;
    }

    /// <summary>
    /// 刷新所有武器图标
    /// </summary>
    public void UpdateWeaponIcons()
    {
        var controller = WeaponController.Instance;
        if (controller == null) return;

        List<WeaponPart> weaponsToShow = new List<WeaponPart>();

        // 收集所有武器
        if (controller.builtInBladeWeapon != null &&
            controller.builtInBladeWeapon.StatBlock != null &&
            controller.builtInBladeWeapon.isActiveAndEnabled)
        {
            weaponsToShow.Add(controller.builtInBladeWeapon);
        }

        foreach (var owned in controller.ownedWeapons)
        {
            if (owned.weaponPartInstance != null && owned.weaponPartInstance.StatBlock != null)
            {
                // 去重
                if (controller.builtInBladeWeapon != null &&
                    controller.builtInBladeWeapon.StatBlock != null &&
                    owned.weaponPartInstance.StatBlock == controller.builtInBladeWeapon.StatBlock)
                {
                    continue;
                }
                weaponsToShow.Add(owned.weaponPartInstance);
            }
        }

        // 更新槽位
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponSlot slot = slots[i];

            if (i < weaponsToShow.Count)
            {
                WeaponPart weapon = weaponsToShow[i];
                slot.linkedWeapon = weapon;

                // 显示槽位
                slot.rootObject.SetActive(true);

                // 更新图标
                slot.iconImage.sprite = weapon.StatBlock.weaponIcon;
                slot.iconImage.enabled = true;
                slot.iconImage.color = Color.white;

                // 显示能量条
                if (slot.xpBarFill != null)
                {
                    slot.xpBarFill.enabled = true;
                    slot.xpBarFill.color = energyBarColor;
                }
            }
            else
            {
                slot.linkedWeapon = null;

                // 隐藏空槽位
                if (hideEmptySlots)
                {
                    slot.rootObject.SetActive(false);
                }
                else
                {
                    slot.rootObject.SetActive(true);
                    slot.iconImage.enabled = false;
                    if (slot.xpBarFill != null) slot.xpBarFill.enabled = false;
                }
            }
        }
    }

    void Update()
    {
        UpdateEnergyBars();
        UpdateIcons(); // 实时更新图标（进化后会变）
    }

    // 实时更新图标（进化后图标会变）
    private void UpdateIcons()
    {
        foreach (var slot in slots)
        {
            if (slot.linkedWeapon == null) continue;
            if (slot.linkedWeapon.StatBlock == null) continue;

            // 实时同步图标
            if (slot.iconImage.sprite != slot.linkedWeapon.StatBlock.weaponIcon)
            {
                slot.iconImage.sprite = slot.linkedWeapon.StatBlock.weaponIcon;
            }
        }
    }

    private void UpdateEnergyBars()
    {
        foreach (var slot in slots)
        {
            if (slot.linkedWeapon == null || slot.xpBarFill == null) continue;

            WeaponPart weapon = slot.linkedWeapon;
            if (weapon.StatBlock == null || !weapon.StatBlock.usesEnergy)
            {
                slot.xpBarFill.fillAmount = 0f;
                continue;
            }

            // 计算目标能量百分比
            float maxEnergy = weapon.StatBlock.maxEnergy;
            float targetPercent = 0f;
            if (maxEnergy > 0f)
            {
                targetPercent = Mathf.Clamp01(weapon.currentEnergy / maxEnergy);
            }

            // 平滑过渡能量条
            slot.displayedFillAmount = Mathf.Lerp(slot.displayedFillAmount, targetPercent, Time.deltaTime * 8f);
            slot.xpBarFill.fillAmount = slot.displayedFillAmount;

            // 检测能量变化，触发脉冲效果
            if (weapon.currentEnergy > slot.lastEnergy + 0.1f)
            {
                PulseIcon(slot);
            }
            slot.lastEnergy = weapon.currentEnergy;

            // 能量满时变色
            slot.xpBarFill.color = targetPercent >= 1f ? energyBarFullColor : energyBarColor;
        }
    }

    // 图标脉冲动画 - 追踪正在播放的协程，防止重复触发导致缩放叠加
    private Dictionary<Transform, Coroutine> activePulseCoroutines = new Dictionary<Transform, Coroutine>();
    
    private void PulseIcon(WeaponSlot slot)
    {
        if (slot.iconImage == null) return;
        Transform target = slot.iconImage.transform;
        
        // 如果已有脉冲动画在播放，先停止并重置
        if (activePulseCoroutines.TryGetValue(target, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
            target.localScale = Vector3.one; // 立即重置为标准大小
        }
        
        activePulseCoroutines[target] = StartCoroutine(PulseCoroutine(target));
    }

    private System.Collections.IEnumerator PulseCoroutine(Transform target)
    {
        // 【修复】始终以 Vector3.one 为基准，不再取当前值
        Vector3 originalScale = Vector3.one;
        Vector3 pulseScale = originalScale * 1.15f;
        
        // 放大
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 10f;
            target.localScale = Vector3.Lerp(originalScale, pulseScale, t);
            yield return null;
        }
        
        // 缩小回原始
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 8f;
            target.localScale = Vector3.Lerp(pulseScale, originalScale, t);
            yield return null;
        }
        
        target.localScale = originalScale;
        
        // 动画完成，移除追踪
        activePulseCoroutines.Remove(target);
    }
}