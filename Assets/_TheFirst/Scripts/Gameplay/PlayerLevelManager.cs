using UnityEngine;
using UnityEngine.Events;

// ������ MechRoot ��
public class PlayerLevelManager : MonoBehaviour
{
    [Header("�ȼ��뾭��")]
    public int currentLevel = 1;
    public int currentExperience = 0;
    public int experienceToNextLevel = 10; // ������һ������ľ���

    [Header("�����¼�")]
    public UnityEvent OnLevelUp; // ����ʱ���� (���ڴ�����ѡ������)

    // (��ѡ) ���ڼ�����һ�����辭������߻�ʽ����
    // public AnimationCurve xpCurve;
    // public float baseXp = 10;
    // public float levelMultiplier = 1.5f;

    /// <summary>
    /// ���Ӿ���ֵ��
    /// </summary>
    /// <param name="amount">���ӵľ�������</param>
    public void AddExperience(int amount)
    {
        currentExperience += amount;
        Debug.Log($"��� {amount} XP. ��ǰ: {currentExperience} / {experienceToNextLevel}");

        // ����Ƿ�����
        while (currentExperience >= experienceToNextLevel) // ʹ�� while �Դ���һ�λ�úܶྭ�������༶�����
        {
            LevelUp();
        }

        // (��ѡ) ��������¾����� UI
        // UpdateXpUI();
    }

    /// <summary>
    /// ���������߼���
    /// </summary>
    private void LevelUp()
    {
        currentLevel++;
        currentExperience -= experienceToNextLevel; // ��ȥ��ǰ�ȼ�����ľ���

        // ������������һ������ľ��� (������һ���򵥵�ʾ����ʽ)
        experienceToNextLevel = CalculateNextLevelXP(currentLevel);

        Debug.Log($"--- LEVEL UP! --- �ﵽ�ȼ� {currentLevel}. ��һ����Ҫ {experienceToNextLevel} XP.");

        // ���������¼�
        OnLevelUp?.Invoke();

        // (��ѡ) �������������������Ч����Ч��
    }

    /// <summary>
    /// ��������ָ���ȼ�����ľ���ֵ (ʾ��)
    /// </summary>
    private int CalculateNextLevelXP(int level)
    {
        // �򵥵��������� + �̶�����ֵ (������Զ�������ӵĹ�ʽ)
        return 10 + (level - 1) * 5;
        // ����ָ������: return Mathf.RoundToInt(baseXp * Mathf.Pow(levelMultiplier, level - 1));
    }

    // (��ѡ) ��ȡ��ǰ�ȼ�����Ϣ�ķ���
    public int GetLevel() => currentLevel;
    public int GetCurrentXP() => currentExperience;
    public int GetXPToNextLevel() => experienceToNextLevel;
    public float GetXPPercentage() => (float)currentExperience / experienceToNextLevel;
}