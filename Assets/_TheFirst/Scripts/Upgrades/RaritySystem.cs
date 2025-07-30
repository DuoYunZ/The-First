using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 这是一个静态类，不需要挂载到任何游戏对象上
public static class RaritySystem
{
    // 定义不同品质的基础权重（概率）。您可以根据游戏平衡性随时调整这些数值。
    private static readonly Dictionary<Rarity, float> baseWeights = new Dictionary<Rarity, float>
    {
        { Rarity.Common,   70f }, // 白色
        { Rarity.Uncommon, 20f }, // 蓝色
        { Rarity.Rare,     8f  }, // 紫色
        { Rarity.Epic,     2f  }  // 橙色
    };

    /// <summary>
    /// 根据一个“升级选项”列表和玩家的幸运值，按品质概率随机抽取一个选项。
    /// </summary>
    /// <param name="options">可供选择的升级选项列表</param>
    /// <param name="luck">玩家的幸运值 (例如, 1.1 代表 10% 的幸运加成)</param>
    /// <returns>随机抽取出的一个升级选项</returns>
    public static UpgradeOption GetRandomOptionByRarity(List<UpgradeOption> options, float luck = 1.0f)
    {
        if (options == null || options.Count == 0) return null;

        // 1. 根据幸运值调整权重
        Dictionary<Rarity, float> adjustedWeights = new Dictionary<Rarity, float>();
        float totalWeight = 0;

        // 幸运值会降低白色品质的权重，并提升其他高品质的权重
        // 您可以自定义更复杂的幸运值影响公式
        adjustedWeights[Rarity.Common] = baseWeights[Rarity.Common] / luck;
        adjustedWeights[Rarity.Uncommon] = baseWeights[Rarity.Uncommon] * luck;
        adjustedWeights[Rarity.Rare] = baseWeights[Rarity.Rare] * luck * 1.1f;
        adjustedWeights[Rarity.Epic] = baseWeights[Rarity.Epic] * luck * 1.2f;

        // 2. 随机选择一个“品质”
        foreach (var weight in adjustedWeights.Values)
        {
            totalWeight += weight;
        }
        float randomPoint = Random.value * totalWeight;
        Rarity chosenRarity = Rarity.Common; // 默认为白色

        // 从最高品质开始向下检查，看随机点落在哪个区间
        foreach (var rarity in adjustedWeights.Keys.OrderByDescending(k => k))
        {
            if (randomPoint < adjustedWeights[rarity])
            {
                chosenRarity = rarity;
                break;
            }
            randomPoint -= adjustedWeights[rarity];
        }

        // 3. 从传入的选项列表中，筛选出所有符合选中品质的选项
        var availableOptionsOfRarity = options.Where(opt => opt.rarity == chosenRarity).ToList();

        // 4. 如果该品质有可选的升级项，则随机返回一个
        if (availableOptionsOfRarity.Count > 0)
        {
            return availableOptionsOfRarity[Random.Range(0, availableOptionsOfRarity.Count)];
        }
        else
        {
            // 后备方案：如果抽中的品质没有任何可用选项（例如，您没有配置橙色品质的火炮升级）
            // 则从所有低于该品质的选项中随机挑选一个，确保玩家总能获得升级
            var fallbackOptions = options.Where(opt => opt.rarity < chosenRarity).ToList();
            if (fallbackOptions.Count > 0)
            {
                return fallbackOptions[Random.Range(0, fallbackOptions.Count)];
            }

            // 最终后备：如果没有任何更低品质的，就从所有选项里随便选一个
            return options[Random.Range(0, options.Count)];
        }
    }
}