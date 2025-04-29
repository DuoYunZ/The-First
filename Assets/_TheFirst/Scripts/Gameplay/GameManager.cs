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

        OnEnterBuildMode?.Invoke();
    }

    // ����ս��ģʽ���߼�
    public void EnterCombatMode()
    {
        Debug.Log("EnterCombatMode: --- Start ---"); // ��־ 1

        // ��ȫ���
        if (chassisCoreTransform == null || mechRootPrefabOrObject == null)
        {
            Debug.LogError("�޷�����ս��ģʽ��Chassis Core Transform �� Mech Root δ���ã�");
            return;
        }
        Debug.Log("EnterCombatMode: Safety checks passed."); // ��־ 2

        currentState = GameState.Combat;
        Debug.Log("EnterCombatMode: State set to Combat."); // ��־ 3

        // ���ý���ϵͳ
        Debug.Log("EnterCombatMode: Disabling Build Systems..."); // ��־ 4
        if (mechBuilder != null) mechBuilder.enabled = false;
        var buildCamController = buildCameraObject?.GetComponent<BuildCameraController>();
        if (buildCamController != null) buildCamController.enabled = false;
        mechBuilder?.ClearSelection();
        Debug.Log("EnterCombatMode: Build Systems Disabled."); // ��־ 5

        // ����/���� MechRoot
        Debug.Log("EnterCombatMode: Creating/Enabling MechRoot..."); // ��־ 6
        if (currentMechRootInstance == null)
        {
            if (mechRootPrefabOrObject.scene.IsValid())
            {
                currentMechRootInstance = mechRootPrefabOrObject;
                currentMechRootInstance.SetActive(true);
                Debug.Log("EnterCombatMode: Activated existing MechRoot from scene."); // ��־ 6a
            }
            else
            {
                currentMechRootInstance = Instantiate(mechRootPrefabOrObject);
                Debug.Log("EnterCombatMode: Instantiated MechRoot from prefab."); // ��־ 6b
            }
            currentMechRootInstance.name = "MechRoot_ActiveInstance";
            currentMechRootInstance.transform.position = chassisCoreTransform.position;
            currentMechRootInstance.transform.rotation = chassisCoreTransform.rotation;

            rootMechController = currentMechRootInstance.GetComponent<MechController>();
            if (rootMechController == null)
            {
                Debug.LogError("MechRoot ������û���ҵ� MechController �ű�!", currentMechRootInstance);
                EnterBuildMode(); // �л�ȥ
                return;
            }
            Debug.Log("EnterCombatMode: Found MechController on MechRoot."); // ��־ 6c
        }
        else
        {
            Debug.Log("EnterCombatMode: Using existing MechRoot instance."); // ��־ 6d
        }

        // ���ø��ӹ�ϵ
        Debug.Log("EnterCombatMode: Parenting..."); // ��־ 7
        chassisCoreTransform.SetParent(currentMechRootInstance.transform, true);
        Debug.Log("EnterCombatMode: Parenting Done."); // ��־ 8

        // ��������״̬
        Debug.Log("EnterCombatMode: Setting Physics State (Kinematic)..."); // ��־ 9
        if (chassisRigidbody != null) chassisRigidbody.isKinematic = true;
        Debug.Log("EnterCombatMode: Physics State Set."); // ��־ 10

        // ����ս��������
        Debug.Log("EnterCombatMode: Enabling Combat Controller..."); // ��־ 11
        if (rootMechController != null) rootMechController.enabled = true; // ��ᴥ�� MechController �� OnEnable �� Start (�����һ������)
        Debug.Log("EnterCombatMode: Combat Controller Enabled."); // ��־ 12

        // �л� UI
        Debug.Log("EnterCombatMode: Switching UI..."); // ��־ 13
        if (buildUIContainer != null) buildUIContainer.SetActive(false);
        if (combatUIContainer != null) combatUIContainer.SetActive(true);
        Debug.Log("EnterCombatMode: UI Switched."); // ��־ 14

        // �л������
        Debug.Log("EnterCombatMode: Switching Cameras..."); // ��־ 15
        if (buildCameraObject != null) buildCameraObject.SetActive(false);
        if (combatCameraObject != null) combatCameraObject.SetActive(true);
        if (combatCameraObject != null && !combatCameraObject.CompareTag("MainCamera")) { /* Warning */ }
        Debug.Log("EnterCombatMode: Cameras Switched."); // ��־ 16

        // �����¼�
        Debug.Log("EnterCombatMode: Invoking Event..."); // ��־ 17
        OnEnterCombatMode?.Invoke();
        Debug.Log("EnterCombatMode: --- Finished Successfully ---"); // ��־ 18
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