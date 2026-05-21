using UnityEngine;
using TMPro;
using System.Text;

public class Debug_PlayerStatsDisplay : MonoBehaviour
{
    [Header("UI引用")]
    public TextMeshProUGUI statsDisplayText; // 用于显示属性的UI文本

    private PlayerStats playerStats;
    private StringBuilder sb = new StringBuilder();

    void Start()
    {
        // 延迟获取，确保PlayerStats已初始化
        Invoke("GetPlayerStatsReference", 0.3f);
    }

    void GetPlayerStatsReference()
    {
        playerStats = PlayerStats.Instance;
        if (playerStats == null)
        {
            statsDisplayText.text = "PlayerStats.Instance 未找到！";
            enabled = false;
        }
    }

    // 每帧都更新显示的数值
    void Update()
    {
        if (playerStats == null) return;

        // 使用 StringBuilder 提高效率
        sb.Clear();
        sb.AppendLine("<b>--- 玩家实时属性 ---</b>");
        sb.AppendLine($"伤害 (Dmg): <color=yellow>{playerStats.damageMultiplier * 100:F0}%</color> (+{playerStats.flatDamageBonus})");
        sb.AppendLine($"范围伤害 (AoE Dmg): <color=yellow>{playerStats.aoeDamageMultiplier * 100:F0}%</color> (+{playerStats.flatAoeDamageBonus})");
        sb.AppendLine($"范围半径 (AoE Rad): <color=yellow>{playerStats.aoeRadiusMultiplier * 100:F0}%</color>");
        sb.AppendLine($"射速 (Fire Rate): <color=yellow>{playerStats.fireRateMultiplier:F2}x</color>");
        sb.AppendLine($"弹道速度 (Proj Spd): <color=yellow>{playerStats.projectileSpeedMultiplier:F2}x</color>");
        sb.AppendLine($"子弹数量 (Proj): <color=yellow>+{playerStats.bonusProjectileCount}</color>");
        sb.AppendLine($"穿透 (Pierce): <color=yellow>+{playerStats.bonusPierceCount}</color>");
        sb.AppendLine($"环绕/部署数量 (Orbit): <color=yellow>+{playerStats.bonusOrbitalCount}</color>");
        sb.AppendLine($"持续时间 (Duration): <color=yellow>{playerStats.durationMultiplier:F2}x</color>");
        sb.AppendLine($"移动速度 (Move): <color=yellow>{playerStats.moveSpeedMultiplier:F2}x</color>");
        sb.AppendLine($"拾取范围 (Pickup): <color=yellow>{playerStats.pickupRadiusMultiplier:F2}x</color>");
        sb.AppendLine($"经验 (XP): <color=yellow>{playerStats.experienceGainMultiplier:F2}x</color>");
        sb.AppendLine($"幸运 (Luck): <color=yellow>{playerStats.luck * 100:F0}%</color>");
        sb.AppendLine($"冲刺余烬 (Dash Blast): <color=yellow>Lv{playerStats.dashExplosionLevel}</color>");
        // ... 您可以按需添加更多属性的显示 ...

        statsDisplayText.text = sb.ToString();
    }
}
