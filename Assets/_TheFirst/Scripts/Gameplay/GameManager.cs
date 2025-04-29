using UnityEngine;
using UnityEngine.Events;

// (GameState ö�ٶ���) ...
public enum GameState { Building, Combat }

public class GameManager : MonoBehaviour
{
    [Header("��ǰ״̬")]
    [SerializeField] // �� Inspector �пɼ���������ֱ���޸�
    private GameState currentState = GameState.Building; // ��ʼ״̬Ϊ����

    [Header("ϵͳ����")]
    [SerializeField] private MechBuilder mechBuilder; // ������Ҫ����/����
    // BuildCameraController ����Ӧ���� buildCameraObject �ϣ�������Ҫ��������
    [SerializeField] private Rigidbody chassisRigidbody;
    [SerializeField] private Transform chassisCoreTransform;

    [Header("UI ����")]
    [SerializeField] private GameObject buildUIContainer;
    [SerializeField] private GameObject combatUIContainer;

    [Header("ս��ģʽ����")]
    [SerializeField] private GameObject mechRootPrefabOrObject;
    private GameObject currentMechRootInstance = null;
    private MechController rootMechController = null; // �����ϵĿ������ű�

    // --- ��������������� ---
    [Header("��������� (��ק�����еĶ���)")]
    [SerializeField] private GameObject buildCameraObject; // ��������������� BuildCameraController �Ķ���
    [SerializeField] private GameObject combatCameraObject; // ����ս�������(������ű�/Cinemachine)�Ķ���
    // -----------------------
    [Header("ϵͳ����")]
    // ... (��������) ...
    [SerializeField] private EnemySpawner enemySpawner; // *** ���� ***
    public Transform playerTransform { get; private set; } // ����ֻ������

    // (��ѡ) �¼�������֪ͨ�����ű�״̬�Ѹı�
    public UnityEvent OnEnterBuildMode;
    public UnityEvent OnEnterCombatMode;

    // ---- ����ģʽ (��ѡ������ȫ�ַ��� GameManager) ----
    public static GameManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // �����Ҫ�糡������ GameManager
        }
    }
    // ---- ����ģʽ���� ----


    void Start()
    {
        // ... (��� chassisCoreTransform, mechRootPrefabOrObject) ...
        // --- ���������������� ---
        if (buildCameraObject == null || combatCameraObject == null)
        {
            Debug.LogError("GameManager: �����ս�����������δ����!", this);
            enabled = false;
            return;
        }
        if (enemySpawner == null)
        {
            Debug.LogError("GameManager: Enemy Spawner δ����!", this);
            enabled = false;
            return;
        }
        // -----------------------

        EnterBuildMode();
    }

    // ���뽨��ģʽ���߼�
    public void EnterBuildMode()
    {
        Debug.Log("Entering Build Mode...");
        currentState = GameState.Building;

        // --- ������ӹ�ϵ & ����/���� MechRoot ---
        if (currentMechRootInstance != null && chassisCoreTransform != null) { /* ... */ }
        if (currentMechRootInstance != null) { /* ... */ }
        // ------------------------------------

        // --- ����/���ýű��Ͷ��� ---
        if (mechBuilder != null) mechBuilder.enabled = true;
        if (rootMechController != null) rootMechController.enabled = false; // ����ս��������
        if (chassisRigidbody != null) chassisRigidbody.isKinematic = true;
        // -------------------------

        // --- �л� UI ---
        if (buildUIContainer != null) buildUIContainer.SetActive(true);
        if (combatUIContainer != null) combatUIContainer.SetActive(false);
        // ---------------

        // --- �л������ ---
        if (combatCameraObject != null) combatCameraObject.SetActive(false); // ����ս�����
        if (buildCameraObject != null) buildCameraObject.SetActive(true);  // ���ý������
        // ȷ�� BuildCameraController (������� BuildCameraObject �ϵ����) Ҳ������
        var buildCamController = buildCameraObject?.GetComponent<BuildCameraController>();
        if (buildCamController != null) buildCamController.enabled = true;
        // ------------------

        // --- *** ������ֹͣ�������� *** ---
        if (enemySpawner != null) enemySpawner.enabled = false; // ���ߵ��� enemySpawner.StopSpawning();

        playerTransform = null; // <--- �������

        OnEnterBuildMode?.Invoke();
    }

    // ����ս��ģʽ���߼�
    public void EnterCombatMode()
    {
        Debug.Log("EnterCombatMode: --- Start ---");

        // --- ��ʽ��־��¼ Inspector ���� ---
        Debug.Log($"EnterCombatMode: Checking references - chassisCoreTransform is {(chassisCoreTransform == null ? "!!! NULL !!!" : chassisCoreTransform.name)}");
        Debug.Log($"EnterCombatMode: Checking references - mechRootPrefabOrObject is {(mechRootPrefabOrObject == null ? "!!! NULL !!!" : mechRootPrefabOrObject.name)}");
        // ---------------------------------

        // ��ȫ���
        if (chassisCoreTransform == null || mechRootPrefabOrObject == null)
        {
            Debug.LogError("�޷�����ս��ģʽ��Chassis Core Transform �� Mech Root δ���ã����� GameManager Inspector �м�鸳ֵ��");
            return; // ��ֹ����ִ��
        }
        Debug.Log("EnterCombatMode: Initial reference checks passed.");

        currentState = GameState.Combat;
        Debug.Log("EnterCombatMode: State set to Combat.");

        // ���ý���ϵͳ
        Debug.Log("EnterCombatMode: Disabling Build Systems...");
        if (mechBuilder != null) mechBuilder.enabled = false;
        var buildCamController = buildCameraObject?.GetComponent<BuildCameraController>();
        if (buildCamController != null) buildCamController.enabled = false;
        mechBuilder?.ClearSelection();
        Debug.Log("EnterCombatMode: Build Systems Disabled.");

        // ����/���� MechRoot
        Debug.Log("EnterCombatMode: Creating/Enabling MechRoot...");
        if (currentMechRootInstance == null)
        {
            if (mechRootPrefabOrObject.scene.IsValid())
            { // ��������
                currentMechRootInstance = mechRootPrefabOrObject;
                currentMechRootInstance.SetActive(true);
                Debug.Log($"EnterCombatMode: Activated existing MechRoot from scene: {currentMechRootInstance.name}");
            }
            else
            { // Prefab
                Debug.Log($"EnterCombatMode: Attempting to Instantiate prefab: {mechRootPrefabOrObject.name}");
                currentMechRootInstance = Instantiate(mechRootPrefabOrObject);
                // --- �� Instantiate �����̼�� ---
                if (currentMechRootInstance == null)
                {
                    Debug.LogError("!!! FATAL: Instantiate(mechRootPrefabOrObject) ������ NULL! ����Ԥ���ļ��Ƿ��������Ч��", mechRootPrefabOrObject);
                    EnterBuildMode(); // ���Իָ�
                    return; // ��ֹ����ִ��
                }
                Debug.Log($"EnterCombatMode: Instantiated MechRoot from prefab: {currentMechRootInstance.name}");
                // -------------------------------
            }
            currentMechRootInstance.name = "MechRoot_ActiveInstance";
            if (enemySpawner != null) enemySpawner.enabled = true;

            // --- �ڷ��� Transform ǰ�ٴν����ϸ��� ---
            if (currentMechRootInstance == null)
            {
                Debug.LogError("!!! FATAL: currentMechRootInstance ������ Transform ǰ�����Ϊ NULL!", this); EnterBuildMode(); return;
            }
            if (currentMechRootInstance.transform == null)
            {
                Debug.LogError("!!! FATAL: currentMechRootInstance.transform �� NULL!", currentMechRootInstance); EnterBuildMode(); return;
            }
            if (chassisCoreTransform == null)
            { // �ٴμ�� ChassisCore
                Debug.LogError("!!! FATAL: chassisCoreTransform ������ Transform ǰ�����Ϊ NULL!", this); EnterBuildMode(); return;
            }
            Debug.Log($"EnterCombatMode: ��������λ��/��ת. MechRoot: {currentMechRootInstance.name}, Chassis: {chassisCoreTransform.name}");
            // ---------------------------------------

            // ����λ�ú���ת (��Լ��ԭ���� Line 147 ����)
            currentMechRootInstance.transform.position = chassisCoreTransform.position; // <-- �������������
            currentMechRootInstance.transform.rotation = chassisCoreTransform.rotation; // <-- ��������

            Debug.Log("EnterCombatMode: MechRoot Transform set."); // �����ִ�е����˵����������û����

            // ��ȡ������
            rootMechController = currentMechRootInstance.GetComponent<MechController>();
            if (rootMechController == null) { /* ... */ return; }
            playerTransform = currentMechRootInstance.transform; // �����������
        }
        else
        {
            Debug.Log("EnterCombatMode: Using existing MechRoot instance.");
        }

        // ���ø��ӹ�ϵ
        Debug.Log("EnterCombatMode: Parenting...");
        chassisCoreTransform.SetParent(currentMechRootInstance.transform, true);
        Debug.Log("EnterCombatMode: Parenting Done.");

        // ��������״̬
        Debug.Log("EnterCombatMode: Setting Physics State (Kinematic)...");
        if (chassisRigidbody != null) chassisRigidbody.isKinematic = true;
        Debug.Log("EnterCombatMode: Physics State Set.");

        // ����ս��������
        Debug.Log("EnterCombatMode: Enabling Combat Controller...");
        if (rootMechController != null) rootMechController.enabled = true;
        Debug.Log("EnterCombatMode: Combat Controller Enabled.");

        // �л� UI
        Debug.Log("EnterCombatMode: Switching UI...");
        if (buildUIContainer != null) buildUIContainer.SetActive(false);
        if (combatUIContainer != null) combatUIContainer.SetActive(true);
        Debug.Log("EnterCombatMode: UI Switched.");

        // �л������
        Debug.Log("EnterCombatMode: Switching Cameras...");
        if (buildCameraObject != null) buildCameraObject.SetActive(false);
        if (combatCameraObject != null) combatCameraObject.SetActive(true);
        if (combatCameraObject != null && !combatCameraObject.CompareTag("MainCamera")) { /* ... */ }
        Debug.Log("EnterCombatMode: Cameras Switched.");

        // �����¼�
        Debug.Log("EnterCombatMode: Invoking Event...");
        OnEnterCombatMode?.Invoke();
        Debug.Log("EnterCombatMode: --- Finished Successfully ---");
    }


    // �ṩһ������������ UI ��ť����
    public void SwitchToCombatMode()
    {
        if (currentState == GameState.Building)
        {
            EnterCombatMode();
        }
        else
        {
            Debug.LogWarning("�Ѿ���ս��ģʽ��δ֪״̬���޷��л���ս��ģʽ��");
        }
    }

    // (��ѡ) �ṩ�л��ؽ���ģʽ�ķ���
    public void SwitchToBuildMode()
    {
        if (currentState == GameState.Combat)
        {
            EnterBuildMode();
            // ������Ҫ���û���λ�á�״̬��
            // ResetMechPosition();
        }
        else
        {
            Debug.LogWarning("�Ѿ��ڽ���ģʽ��δ֪״̬���޷��л�������ģʽ��");
        }
    }


    // (��ѡ) ��ȡ��ǰ״̬
    public GameState GetCurrentState()
    {
        return currentState;
    }
}