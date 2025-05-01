using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections.Generic; // ��Ҫ List
using System.Linq; // ������Ҫ Linq

// ��Ϸ״̬ö��
public enum GameState { Building, Combat, GameOver }

public class GameManager : MonoBehaviour
{
    [Header("��ǰ״̬")]
    [SerializeField] private GameState currentState = GameState.Building;

    [Header("ϵͳ����")]
    [SerializeField] private MechBuilder mechBuilder;
    [SerializeField] private Rigidbody chassisRigidbody;
    [SerializeField] private Transform chassisCoreTransform; // ��Ҫ ChassisCore �� Transform ����
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("UI ����")]
    [SerializeField] private GameObject buildUIContainer;
    [SerializeField] private GameObject combatUIContainer;
    [SerializeField] private GameObject gameOverPanel;

    [Header("ս��ģʽ����")]
    [SerializeField] private GameObject mechRootPrefabOrObject; // ����������Ԥ������
    private GameObject currentMechRootInstance = null;
    private MechController rootMechController = null;
    private Health playerHealthComponent = null;
    public Transform playerTransform { get; private set; } // �������ű�������� (MechRoot) �� Transform

    [Header("���������")]
    [SerializeField] private GameObject buildCameraObject;
    [SerializeField] private GameObject combatCameraObject;

    // --- vvv ������������� vvv ---
    [Header("������� (Ground Drop Settings)")]
    [Tooltip("���¼������������")]
    public float groundCheckDistance = 10f;
    [Tooltip("���׻�׼��(ͨ����MechRoot��Pivot)�����·���ߵ����������߶�")]
    public float pivotHeightAboveGround = 0.1f; // ��Ҫ����ģ�ͺ�Pivot��ϸ����
    [Tooltip("���ڼ�����Ĳ� (�� Inspector ��ѡ������ Default, Ground)")]
    public LayerMask groundLayerMask = 1; // Ĭ��ֻ��� Default ��, **������� Inspector ���޸�!**
    [Tooltip("�����������߼��Ļ��׵ײ������� (����� ChassisCore �ľֲ�����)")]
    public List<Vector3> groundCheckOffsets = new List<Vector3>() {
        Vector3.zero, // ���ĵ�
        new Vector3(0.5f, 0, 0.5f), // ��ǰ�� (��Щֵ��Ҫ������Ļ��״�ųߴ����)
        new Vector3(-0.5f, 0, 0.5f), // ��ǰ��
        new Vector3(0.5f, 0, -0.5f), // �Һ�
        new Vector3(-0.5f, 0, -0.5f)  // ���
    };
    // --- ^^^ ������������� ^^^ ---

    [Header("�¼�")]
    public UnityEvent OnEnterBuildMode;
    public UnityEvent OnEnterCombatMode;

    // --- ����ģʽ ---
    public static GameManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; /* DontDestroyOnLoad(gameObject); */ }
    }
    // ---------------

    void Start()
    {
        // --- ִ�б�Ҫ�Ŀ�ֵ��� ---
        if (chassisCoreTransform == null && chassisRigidbody != null) chassisCoreTransform = chassisRigidbody.transform;
        bool checkFailed = false;
        if (CheckNull(chassisCoreTransform, "Chassis Core Transform")) checkFailed = true;
        if (CheckNull(mechRootPrefabOrObject, "Mech Root Prefab Or Object")) checkFailed = true;
        if (CheckNull(buildCameraObject, "Build Camera Object")) checkFailed = true;
        if (CheckNull(combatCameraObject, "Combat Camera Object")) checkFailed = true;
        if (CheckNull(enemySpawner, "Enemy Spawner")) checkFailed = true;
        if (CheckNull(buildUIContainer, "Build UI Container")) checkFailed = true;
        if (CheckNull(combatUIContainer, "Combat UI Container")) checkFailed = true;
        if (CheckNull(gameOverPanel, "Game Over Panel")) checkFailed = true;
        if (CheckNull(mechBuilder, "Mech Builder")) checkFailed = true;
        if (CheckNull(chassisRigidbody, "Chassis Rigidbody")) checkFailed = true; // Ҳ���һ�� Rigidbody

        if (checkFailed)
        {
            Debug.LogError("GameManager Start: һ��������Ҫ������δ�� Inspector �����ã��ű��ѽ��á�", this);
            enabled = false;
            return;
        }
        // -----------------------

        EnterBuildMode(); // ��Ϸ��ʼʱ���뽨��ģʽ
    }

    // �������������ڼ�������Ƿ�Ϊ�ղ���ӡ����
    private bool CheckNull(object obj, string fieldName)
    {
        if (obj == null || obj.Equals(null))
        { // Unity ���������� == null
            Debug.LogError($"GameManager Error: '{fieldName}' δ�� Inspector ������!", this);
            return true;
        }
        return false;
    }


    public void EnterBuildMode()
    {
        Debug.Log("Entering Build Mode...");
        currentState = GameState.Building;
        Time.timeScale = 1f; // �ָ�ʱ��

        // ������ӹ�ϵ & ���� MechRoot
        if (currentMechRootInstance != null)
        {
            if (playerHealthComponent != null)
            {
                playerHealthComponent.OnDeath.RemoveListener(HandlePlayerDeath);
                playerHealthComponent = null;
            }
            if (chassisCoreTransform != null)
            {
                chassisCoreTransform.SetParent(null, true);
            }

            // �����ǳ��������� Prefab ʵ��������
            if (mechRootPrefabOrObject != null && mechRootPrefabOrObject.scene.IsValid() && currentMechRootInstance == mechRootPrefabOrObject)
            {
                currentMechRootInstance.SetActive(false);
            }
            else
            {
                Destroy(currentMechRootInstance);
            }
            currentMechRootInstance = null;
            rootMechController = null;
            playerTransform = null;
        }

        // ���ý���ϵͳ
        if (mechBuilder != null) mechBuilder.enabled = true;
        if (chassisRigidbody != null) chassisRigidbody.isKinematic = true;

        // ����ս��ϵͳ
        if (enemySpawner != null) enemySpawner.enabled = false;

        // �л� UI
        if (combatUIContainer != null) combatUIContainer.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (buildUIContainer != null) buildUIContainer.SetActive(true);

        // �л������
        if (combatCameraObject != null) combatCameraObject.SetActive(false);
        if (buildCameraObject != null)
        {
            buildCameraObject.SetActive(true);
            var buildCamController = buildCameraObject.GetComponent<BuildCameraController>();
            if (buildCamController != null) buildCamController.enabled = true;
        }

        OnEnterBuildMode?.Invoke();
        Debug.Log("Entered Build Mode setup complete.");
    }


    public void EnterCombatMode()
    {
        Debug.Log("EnterCombatMode: --- Start ---");
        if (currentState != GameState.Building) { Debug.LogWarning("Already in Combat/GameOver, cannot re-enter Combat."); return; } // ��ֹ����

        // ��ȫ��� (Start ʱ�Ѽ�飬������ȷ��һ��)
        if (chassisCoreTransform == null || mechRootPrefabOrObject == null) { Debug.LogError("Cannot enter Combat Mode: References missing!"); return; }

        currentState = GameState.Combat;
        Time.timeScale = 1f;
        Debug.Log("EnterCombatMode: State set to Combat.");

        // --- ���ý���ϵͳ ---
        Debug.Log("EnterCombatMode: Disabling Build Systems...");
        if (mechBuilder != null) mechBuilder.enabled = false;
        var buildCamController = buildCameraObject?.GetComponent<BuildCameraController>();
        if (buildCamController != null) buildCamController.enabled = false;
        mechBuilder?.ClearSelection(); // ������ܵ��������
        Debug.Log("EnterCombatMode: Build Systems Disabled.");

        // --- ����/���� MechRoot ---
        Debug.Log("EnterCombatMode: Creating/Enabling MechRoot...");
        if (currentMechRootInstance == null)
        {
            // ... (ʵ�����򼤻� MechRoot ���߼�) ...
            if (mechRootPrefabOrObject.scene.IsValid()) { /*...*/ } else { currentMechRootInstance = Instantiate(mechRootPrefabOrObject); }
            if (currentMechRootInstance == null) { /* ���������� */ EnterBuildMode(); return; }
            currentMechRootInstance.name = "MechRoot_ActiveInstance";
            // ... (��ȡ rootMechController �� playerHealthComponent, �����¼�) ...
            rootMechController = currentMechRootInstance.GetComponent<MechController>();
            if (rootMechController == null) { /* Error */ EnterBuildMode(); return; }
            playerHealthComponent = currentMechRootInstance.GetComponent<Health>();
            if (playerHealthComponent != null) { playerHealthComponent.OnDeath.RemoveListener(HandlePlayerDeath); playerHealthComponent.OnDeath.AddListener(HandlePlayerDeath); }
            else { /* Error */ }
        }
        else { /* ʹ������ʵ�� */ }
        // -------------------------

        // --- ���� MechRoot ��ʼλ��/��ת ---
        currentMechRootInstance.transform.position = chassisCoreTransform.position;
        currentMechRootInstance.transform.rotation = chassisCoreTransform.rotation;
        playerTransform = currentMechRootInstance.transform; // ��������������� playerTransform
        Debug.Log("EnterCombatMode: MechRoot initial transform set.");

        // --- *** 2. �����ø��ӹ�ϵ *** ---
        Debug.Log("EnterCombatMode: Parenting ChassisCore to MechRoot...");
        chassisCoreTransform.SetParent(currentMechRootInstance.transform, true); // worldPositionStays = true
        Debug.Log("EnterCombatMode: Parenting Done.");
        // --- *** ���ӹ�ϵ���ý��� *** ---

        // --- vvv ����߼� vvv ---
        Debug.Log("EnterCombatMode: Starting ground drop logic..."); // <-- Log A: ��ʼִ������߼�
        float highestGroundHitY = -Mathf.Infinity;
        bool groundFound = false;

        // --- ������ѭ�������� ---
        if (chassisCoreTransform != null && groundCheckOffsets != null && groundCheckOffsets.Count > 0)
        {
            Debug.Log("EnterCombatMode: Ground check condition PASSED. Entering foreach loop..."); // <-- Log B: ����ѭ������������

            foreach (Vector3 localOffset in groundCheckOffsets)
            {
                Debug.Log("EnterCombatMode: Inside foreach loop for offset: " + localOffset); // <-- Log C: ѭ���ڲ���ʼִ��

                // �����������
                Vector3 worldOffsetPoint = chassisCoreTransform.TransformPoint(localOffset);
                Vector3 rayOrigin = new Vector3(worldOffsetPoint.x, chassisCoreTransform.position.y + 1.0f, worldOffsetPoint.z);

                // --- ���ӻ����� ---
                Debug.DrawRay(rayOrigin, Vector3.down * groundCheckDistance, Color.cyan, 3f); // ���� 3 ����ʾ��ɫ����
                // -----------------

                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance + 1.0f, groundLayerMask))
                {
                    groundFound = true;
                    if (hit.point.y > highestGroundHitY)
                    {
                        highestGroundHitY = hit.point.y;
                    }
                }
            } // --- foreach ѭ������ ---
        }
        else // --- �������ѭ�������������� ---
        {
            // ��ӡ�������ĸ�����ʧ����
            Debug.LogError($"EnterCombatMode: Ground check condition FAILED! " +
                           $"chassisCoreTransform is {(chassisCoreTransform == null ? "NULL" : "OK")}, " +
                           $"groundCheckOffsets is {(groundCheckOffsets == null ? "NULL" : "OK")}, " +
                           $"Count is {groundCheckOffsets?.Count ?? -1}"); // <-- Log D: �������ʧ������
        }


        if (groundFound)
        {
            // --- �����Щ��־ ---
            Debug.Log($"Ground found! Highest ground Y = {highestGroundHitY}"); // ��ӡ�ҵ�����ߵ��� Y
            float targetRootY = highestGroundHitY + pivotHeightAboveGround;
            Debug.Log($"Pivot Height Offset = {pivotHeightAboveGround}, Calculated Target MechRoot Y = {targetRootY}"); // ��ӡ�������Ŀ�� Y
            Vector3 currentRootPos = currentMechRootInstance.transform.position;
            Debug.Log($"MechRoot current Y BEFORE adjustment = {currentRootPos.y}"); // ��ӡ����ǰ Y
            // ------------------

            currentRootPos.y = targetRootY;
            currentMechRootInstance.transform.position = currentRootPos; // Ӧ�õ������λ��

            // --- ��������־ ---
            Debug.Log($"MechRoot Y AFTER adjustment = {currentMechRootInstance.transform.position.y}"); // ��ӡ������ Y
            // ------------------
            Debug.Log($"EnterCombatMode: Mech dropped command executed."); // ������־
        }
        else
        {
            Debug.LogWarning("EnterCombatMode: Multi-raycast could not find ground...");
        }
        // --- ^^^ ����߼����� ^^^ ---

        // --- ���� GameManager �� Player Transform ������ ---
        if (currentMechRootInstance != null) playerTransform = currentMechRootInstance.transform;
        // ----------------------------------------------

        // --- ���ø��ӹ�ϵ ---
        Debug.Log("EnterCombatMode: Parenting ChassisCore to MechRoot...");
        chassisCoreTransform.SetParent(currentMechRootInstance.transform, true); // ��������任
        Debug.Log("EnterCombatMode: Parenting Done.");

        // --- ��������״̬ ---
        Debug.Log("EnterCombatMode: Setting ChassisCore Physics State (Kinematic)...");
        if (chassisRigidbody != null) chassisRigidbody.isKinematic = true; // ���� Kinematic
        Debug.Log("EnterCombatMode: Physics State Set.");

        // --- ����ս�������� ---
        Debug.Log("EnterCombatMode: Enabling Combat Controller...");
        if (rootMechController != null) rootMechController.enabled = true;
        Debug.Log("EnterCombatMode: Combat Controller Enabled.");

        // --- �л� UI ---
        Debug.Log("EnterCombatMode: Switching UI...");
        if (buildUIContainer != null) buildUIContainer.SetActive(false);
        if (combatUIContainer != null) combatUIContainer.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        Debug.Log("EnterCombatMode: UI Switched.");

        // --- �л������ ---
        Debug.Log("EnterCombatMode: Switching Cameras...");
        if (buildCameraObject != null) buildCameraObject.SetActive(false);
        if (combatCameraObject != null) combatCameraObject.SetActive(true);
        if (combatCameraObject != null && !combatCameraObject.CompareTag("MainCamera")) { Debug.LogWarning("Combat Camera Object might need 'MainCamera' tag!"); }
        Debug.Log("EnterCombatMode: Cameras Switched.");

        // --- ������������ ---
        Debug.Log("EnterCombatMode: Enabling Enemy Spawner...");
        if (enemySpawner != null) enemySpawner.enabled = true;
        Debug.Log("EnterCombatMode: Enemy Spawner Enabled.");

        // --- �����¼� ---
        Debug.Log("EnterCombatMode: Invoking OnEnterCombatMode Event...");
        OnEnterCombatMode?.Invoke();
        Debug.Log("EnterCombatMode: --- Finished Successfully ---");
    }


    public void SwitchToCombatMode()
    {
        Debug.Log($"SwitchToCombatMode called. Current state: {currentState}");
        if (currentState == GameState.Building) { EnterCombatMode(); }
        else { Debug.LogWarning("Already in Combat/Game Over mode, cannot switch to Combat."); }
    }


    public void SwitchToBuildMode()
    {
        Debug.Log($"SwitchToBuildMode called. Current state: {currentState}");
        if (currentState == GameState.Combat || currentState == GameState.GameOver) { EnterBuildMode(); }
        else { Debug.LogWarning("Already in Building mode, cannot switch to Build."); }
    }


    public GameState GetCurrentState() { return currentState; }


    void HandlePlayerDeath()
    {
        Debug.Log("GAME OVER!");
        if (currentState == GameState.GameOver) return;
        currentState = GameState.GameOver;
        Time.timeScale = 0f; // ��ͣʱ��

        if (rootMechController != null) rootMechController.enabled = false;
        if (enemySpawner != null) enemySpawner.enabled = false;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        // Unsubscribe handled in EnterBuildMode or can be done here too
        // if (playerHealthComponent != null) playerHealthComponent.OnDeath.RemoveListener(HandlePlayerDeath);
    }

    public void RestartGame()
    {
        Debug.Log("Restarting game...");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}