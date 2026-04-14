using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 武器分支选择管理器
// 当武器经验满时，显示分支选择UI（复用UpgradeCardUI）
public class WeaponBranchManager : MonoBehaviour
{
    public static WeaponBranchManager Instance { get; private set; }

    [Header("UI引用")]
    [Tooltip("分支选择面板（可复用upgradePanel）")]
    public GameObject branchPanel;
    [Tooltip("卡片容器（可复用cardContainer）")]
    public Transform cardContainer;

    [Header("卡片预制件")]
    [Tooltip("分支选择卡片预制件（可复用unlockCardPrefab）")]
    public GameObject branchCardPrefab;

    [Header("动画设置")]
    public float delayBetweenCards = 0.15f;

    // 当前等待分支选择的武器
    private WeaponPart pendingWeapon;
    private List<GameObject> activeCards = new List<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (branchPanel != null) branchPanel.SetActive(false);
    }

    // 订阅武器的分支选择事件
    public void RegisterWeapon(WeaponPart weapon)
    {
        weapon.OnBranchChoiceRequired += ShowBranchChoice;
    }

    public void UnregisterWeapon(WeaponPart weapon)
    {
        weapon.OnBranchChoiceRequired -= ShowBranchChoice;
    }

    // 显示分支选择UI
    private void ShowBranchChoice(WeaponPart weapon)
    {
        pendingWeapon = weapon;
        var options = weapon.GetBranchOptions();

        // 清理旧卡片
        foreach (Transform child in cardContainer) Destroy(child.gameObject);
        activeCards.Clear();

        // 1. 生成分支卡片
        foreach (var branchOption in options)
        {
            if (branchOption == null) continue;

            GameObject cardGO = Instantiate(branchCardPrefab, cardContainer);
            
            // 创建临时的SkillTreeNodeData用于显示
            SkillTreeNodeData tempNode = ScriptableObject.CreateInstance<SkillTreeNodeData>();
            tempNode.skillName = branchOption.weaponName;
            tempNode.skillIcon = branchOption.weaponIcon;
            tempNode.associatedWeapon = branchOption;

            // 创建临时的UpgradeOption
            UpgradeOption tempOption = new UpgradeOption();
            tempOption.description = $"分支进化: {branchOption.weaponName}";
            tempOption.rarity = Rarity.Rare;

            // 设置卡片（使用分支选择专用方法）
            var cardUI = cardGO.GetComponent<UpgradeCardUI>();
            if (cardUI != null)
            {
                WeaponStatBlock capturedBranch = branchOption; // 闭包捕获
                cardUI.SetupForBranch(tempNode, tempOption, () => OnBranchSelected(capturedBranch));
            }

            activeCards.Add(cardGO);
        }

        // 2. 【新增】检测并添加融合选项
        if (WeaponFusionManager.Instance != null)
        {
            var fusions = WeaponFusionManager.Instance.GetAvailableFusions(weapon);
            foreach (var recipe in fusions)
            {
                if (recipe == null || recipe.resultWeapon == null) continue;

                GameObject cardGO = Instantiate(branchCardPrefab, cardContainer);
                
                SkillTreeNodeData tempNode = ScriptableObject.CreateInstance<SkillTreeNodeData>();
                tempNode.skillName = recipe.resultWeapon.weaponName;
                tempNode.skillIcon = recipe.cardIcon != null ? recipe.cardIcon : recipe.resultWeapon.weaponIcon;
                tempNode.associatedWeapon = recipe.resultWeapon;

                UpgradeOption tempOption = new UpgradeOption();
                tempOption.description = $"【融合】{recipe.recipeName}";
                tempOption.rarity = Rarity.Epic; // 融合标记为史诗稀有度

                var cardUI = cardGO.GetComponent<UpgradeCardUI>();
                if (cardUI != null)
                {
                    WeaponFusionRecipeSO capturedRecipe = recipe;
                    cardUI.SetupForBranch(tempNode, tempOption, () => OnFusionSelected(capturedRecipe));
                }

                activeCards.Add(cardGO);
            }
        }

        // 3. 检查是否有选项
        if (activeCards.Count == 0)
        {
            Debug.LogWarning($"[分支选择] {weapon.StatBlock.weaponName} 没有配置分支选项！");
            Time.timeScale = 1f;
            return;
        }

        branchPanel.SetActive(true);
        StartCoroutine(ShowCardsSequentially());
    }

    private IEnumerator ShowCardsSequentially()
    {
        // 等待一帧让布局系统完成计算
        yield return null;
        
        // 强制刷新布局
        Canvas.ForceUpdateCanvases();
        
        foreach (var card in activeCards)
        {
            card.SetActive(true);
            var cardUI = card.GetComponent<UpgradeCardUI>();
            if (cardUI != null)
            {
                // 刷新初始位置（布局完成后）
                cardUI.RefreshInitialPosition();
                cardUI.Show();
            }
            yield return new WaitForSecondsRealtime(delayBetweenCards);
        }
    }

    // 玩家选择分支后调用
    public void OnBranchSelected(WeaponStatBlock selectedBranch)
    {
        if (pendingWeapon == null) return;

        pendingWeapon.ApplyBranch(selectedBranch);
        pendingWeapon = null;

        // 关闭面板
        branchPanel.SetActive(false);
        foreach (var card in activeCards) Destroy(card);
        activeCards.Clear();
    }

    /// <summary>
    /// 玩家选择融合后调用
    /// </summary>
    public void OnFusionSelected(WeaponFusionRecipeSO recipe)
    {
        if (pendingWeapon == null || recipe == null) return;

        // 根据融合类型执行不同逻辑
        switch (recipe.fusionType)
        {
            case FusionType.Replace:
                // 替换当前武器为融合结果
                pendingWeapon.ApplyBranch(recipe.resultWeapon);
                break;
                
            case FusionType.Merge:
                // 移除条件中的武器，生成新武器
                RemoveConditionWeapons(recipe);
                pendingWeapon.ApplyBranch(recipe.resultWeapon);
                break;
                
            case FusionType.Upgrade:
                // 进化并解锁新武器到卡池
                pendingWeapon.ApplyBranch(recipe.resultWeapon);
                UnlockWeaponsToPool(recipe.unlockToPool);
                break;
        }

        pendingWeapon = null;
        branchPanel.SetActive(false);
        foreach (var card in activeCards) Destroy(card);
        activeCards.Clear();
    }

    /// <summary>
    /// 移除融合条件中的武器
    /// </summary>
    private void RemoveConditionWeapons(WeaponFusionRecipeSO recipe)
    {
        if (WeaponController.Instance == null) return;
        
        foreach (var cond in recipe.conditions)
        {
            if (cond.type == ConditionType.Weapon && cond.requiredWeapon != null)
            {
                WeaponController.Instance.RemoveWeaponByStatBlock(cond.requiredWeapon);
            }
        }
    }

    /// <summary>
    /// 解锁武器到卡池
    /// </summary>
    private void UnlockWeaponsToPool(string[] weaponIds)
    {
        if (weaponIds == null) return;
        
        foreach (var id in weaponIds)
        {
            // TODO: 调用UpgradeSystem解锁武器
            // UpgradeSystem.Instance?.UnlockWeaponToPool(id);
        }
    }
}
