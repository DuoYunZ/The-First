using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 武器融合管理器 - 检测可用的融合配方
/// </summary>
public class WeaponFusionManager : MonoBehaviour
{
    public static WeaponFusionManager Instance { get; private set; }
    
    [Header("配方库")]
    [Tooltip("所有融合配方")]
    public List<WeaponFusionRecipeSO> allRecipes = new List<WeaponFusionRecipeSO>();
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    /// <summary>
    /// 检测武器经验满时可用的融合配方
    /// </summary>
    public List<WeaponFusionRecipeSO> GetAvailableFusions(WeaponPart weapon)
    {
        var available = new List<WeaponFusionRecipeSO>();
        if (weapon == null || weapon.StatBlock == null) return available;
        
        foreach (var recipe in allRecipes)
        {
            if (CanTriggerRecipe(weapon, recipe))
            {
                available.Add(recipe);
            }
        }
        
        return available;
    }
    
    /// <summary>
    /// 检查配方是否可触发
    /// </summary>
    private bool CanTriggerRecipe(WeaponPart weapon, WeaponFusionRecipeSO recipe)
    {
        // 1. 检查触发武器是否匹配
        if (recipe.triggerWeapon != weapon.StatBlock) return false;
        
        // 2. 检查武器阶段
        if (weapon.currentStage != recipe.requiredStage) return false;
        
        // 3. 检查所有条件
        foreach (var cond in recipe.conditions)
        {
            if (!CheckCondition(cond)) return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 检查单个条件
    /// </summary>
    private bool CheckCondition(FusionCondition cond)
    {
        switch (cond.type)
        {
            case ConditionType.Weapon:
                return HasWeapon(cond.requiredWeapon, cond.requiredWeaponStage);
            case ConditionType.Passive:
                return HasPassive(cond.requiredPassiveId);
            case ConditionType.Talent:
                return HasTalent(cond.requiredTalentId);
        }
        return false;
    }
    
    /// <summary>
    /// 检查是否拥有指定武器
    /// </summary>
    private bool HasWeapon(WeaponStatBlock wsb, WeaponStage stage)
    {
        if (wsb == null || WeaponController.Instance == null) return false;
        
        foreach (var owned in WeaponController.Instance.ownedWeapons)
        {
            if (owned.weaponPartInstance != null && 
                owned.weaponPartInstance.StatBlock == wsb &&
                owned.weaponPartInstance.currentStage >= stage)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 检查是否拥有被动道具
    /// </summary>
    private bool HasPassive(string passiveId)
    {
        // TODO: 根据你的被动道具系统实现
        if (string.IsNullOrEmpty(passiveId)) return false;
        // return PassiveManager.Instance?.HasPassive(passiveId) ?? false;
        return false;
    }
    
    /// <summary>
    /// 检查是否拥有天赋
    /// </summary>
    private bool HasTalent(string talentId)
    {
        // TODO: 根据你的天赋系统实现
        if (string.IsNullOrEmpty(talentId)) return false;
        // return TalentManager.Instance?.HasTalent(talentId) ?? false;
        return false;
    }
}
