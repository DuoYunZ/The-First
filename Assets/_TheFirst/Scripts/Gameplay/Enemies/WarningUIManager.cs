using UnityEngine;
using System.Collections;

public class WarningUIManager : MonoBehaviour
{
    public static WarningUIManager Instance { get; private set; }

    [Header("预警UI元素 (8个方向)")]
    [Tooltip("北侧（上）预警UI对象")]
    public GameObject northWarningUI;
    [Tooltip("东北（右上）预警UI对象")]
    public GameObject northeastWarningUI; // <--- 新增
    [Tooltip("东侧（右）预警UI对象")]
    public GameObject eastWarningUI;
    [Tooltip("东南（右下）预警UI对象")]
    public GameObject southeastWarningUI; // <--- 新增
    [Tooltip("南侧（下）预警UI对象")]
    public GameObject southWarningUI;
    [Tooltip("西南（左下）预警UI对象")]
    public GameObject southwestWarningUI; // <--- 新增
    [Tooltip("西侧（左）预警UI对象")]
    public GameObject westWarningUI;
    [Tooltip("西北（左上）预警UI对象")]
    public GameObject northwestWarningUI; // <--- 新增

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 游戏开始时隐藏所有预警
        if (northWarningUI != null) northWarningUI.SetActive(false);
        if (northeastWarningUI != null) northeastWarningUI.SetActive(false); // <--- 新增
        if (eastWarningUI != null) eastWarningUI.SetActive(false);
        if (southeastWarningUI != null) southeastWarningUI.SetActive(false); // <--- 新增
        if (southWarningUI != null) southWarningUI.SetActive(false);
        if (southwestWarningUI != null) southwestWarningUI.SetActive(false); // <--- 新增
        if (westWarningUI != null) westWarningUI.SetActive(false);
        if (northwestWarningUI != null) northwestWarningUI.SetActive(false); // <--- 新增    
}

    /// <summary>
    /// 根据方向显示一个UI预警，并在指定时间后自动隐藏
    /// </summary>
    public void ShowStampedeGroupWarning(SpawnDirectionHint direction, float duration)
    {
        GameObject warningUI = null;

        switch (direction)
        {
            case SpawnDirectionHint.North:
                warningUI = northWarningUI;
                break;
            case SpawnDirectionHint.Northeast:
                warningUI = northeastWarningUI; // <--- 新增
                break;
            case SpawnDirectionHint.East:
                warningUI = eastWarningUI;
                break;
            case SpawnDirectionHint.Southeast:
                warningUI = southeastWarningUI; // <--- 新增
                break;
            case SpawnDirectionHint.South:
                warningUI = southWarningUI;
                break;
            case SpawnDirectionHint.Southwest:
                warningUI = southwestWarningUI; // <--- 新增
                break;
            case SpawnDirectionHint.West:
                warningUI = westWarningUI;
                break;
            case SpawnDirectionHint.Northwest:
                warningUI = northwestWarningUI; // <--- 新增
                break;
            case SpawnDirectionHint.Random:
            default:
                break;
        }

        if (warningUI != null)
        {
            StartCoroutine(ShowWarningRoutine(warningUI, duration));
        }
    }

    private IEnumerator ShowWarningRoutine(GameObject warningUI, float duration)
    {
        warningUI.SetActive(true);
        yield return new WaitForSeconds(duration);
        warningUI.SetActive(false);
    }
}