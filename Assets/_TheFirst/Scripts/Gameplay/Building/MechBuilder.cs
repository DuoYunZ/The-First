using System.Collections.Generic;
using UnityEngine;
using System.Linq; // ��Ҫ���� Linq ������ز��������

public class MechBuilder : MonoBehaviour
{
    [Header("��Ҫ���� (Setup)")]
    [Tooltip("ָ�����ڵ�������������")]
    public Camera mainCamera;
    [Tooltip("ָ������/���̵ĸ��� (Rigidbody) ���")]
    public Rigidbody chassisRigidbody;

    [Header("����� (Part Inventory)")]
    [Tooltip("�����п��õ����Ԥ����ק������")]
    public List<GameObject> availablePartPrefabs;

    [Header("�������� (Build Settings)")]
    [Tooltip("��������߼���������")]
    public float maxBuildDistance = 100f;
    [Tooltip("�������ʱӦ�õĶ�����תƫ���� (������ӵ�)")]
    public Vector3 placementRotationOffset = Vector3.zero;

    [Header("����/Ԥ������ (Ghost/Preview Settings)")]
    [Tooltip("������ʾ����Ч���İ�͸������ (��ʼ״̬)")]
    public Material ghostMaterial;
    [Tooltip("���鴦�ڿɷ���״̬ʱ�Ĳ���")]
    public Material ghostMaterialValid;
    [Tooltip("���鴦�ڲ��ɷ���״̬ʱ�Ĳ���")]
    public Material ghostMaterialInvalid;

    [Header("��������Ч������ (Snapping & Validity)")]
    [Tooltip("��⸽�����ӵ� (���̰�װ�� & ����Ӵ���) �İ뾶")]
    public float snapCheckRadius = 0.5f;
    [Tooltip("ָ�����ӵ����ڵ�����ͼ�� (��Ҫͬʱ�������̰�װ�������Ӵ���)")]
    public LayerMask connectionPointLayerMask; // *** �ؼ���ȷ���� Inspector ��ѡ���˰��� AttachmentPoint �� PartContactPoint �Ĳ� ***

    [Header("��ת���� (Rotation Control)")]
    [Tooltip("��ת���� (ÿ�ΰ�����ת���ٶ�)")]
    public float rotationStep = 45.0f;
    public KeyCode rotateCounterClockwiseKey = KeyCode.Q; // Yaw Left
    public KeyCode rotateClockwiseKey = KeyCode.E;     // Yaw Right
    public KeyCode rotatePitchForwardKey = KeyCode.R;    // Pitch Forward
    public KeyCode rotatePitchBackwardKey = KeyCode.F;   // Pitch Backward
    // public KeyCode rotateRollLeftKey = KeyCode.Z;    // (��ѡ) Roll Left
    // public KeyCode rotateRollRightKey = KeyCode.C;   // (��ѡ) Roll Right

    [Header("�Ƴ����� (Removal Settings)")]
    [Tooltip("�����ͣ�ڿ��Ƴ������ʱ�Ĳ��� (��ѡ��������ɫ)")]
    public Material highlightMaterial; // ���ڸ����Ĳ��� (���磬����ɫ)
    [Tooltip("����ʶ���ѷ�������� Tag")]
    public string placedPartTag = "PlacedPart";

    private GameObject currentlyHoveredPart = null; // ��ǰ�����ͣ�Ŀ��Ƴ����
    private Dictionary<Renderer, Material[]> originalPartMaterials = new Dictionary<Renderer, Material[]>(); // �洢�����������ԭʼ����

    // --- ˽�б��� ---
    private GameObject selectedPartPrefab = null;
    private GameObject currentGhostInstance = null;
    private List<PartContactPoint> currentGhostContactPoints = new List<PartContactPoint>(); // ��������ĽӴ���
    private Material[] originalMaterials = null; // �����ھɵĲ����滻�߼���������߼�����Ҫ�����Ƴ�

    private bool isGhostPlacementValid = false;
    private Transform potentialSnapTargetPoint = null; // Ǳ�ڵ�Ŀ�����ӵ� (�����ǵ��̰�װ�������Ӵ���)
    private PartContactPoint potentialGhostSourcePoint = null; // Ǳ�ڵ���������ϵ�Դ�Ӵ���

    // �ۻ����û���ת����
    private Quaternion currentRotationOffset = Quaternion.identity;

    void Start()
    {
        if (!ValidateSetup()) return; // ��֤��������

        if (chassisRigidbody != null)
        {
            chassisRigidbody.isKinematic = true; // ���뽨��ģʽ���̶�����
            Debug.Log($"Chassis Rigidbody '{chassisRigidbody.name}' ������Ϊ Kinematic ���ڽ��졣");
        }

        selectedPartPrefab = null; // ��ʼû��ѡ���κ����
        Debug.Log("MechBuilder ��ʼ����ɡ����� UI ��ťѡ�������");
    }

    // ������ Start ����֤��Ҫ������
    bool ValidateSetup()
    {
        if (mainCamera == null) { Debug.LogError("MechBuilder ����������� (Main Camera) δָ����", this); return false; }
        if (availablePartPrefabs == null || availablePartPrefabs.Count == 0 || availablePartPrefabs.Any(p => p == null)) { Debug.LogError("MechBuilder �������Ԥ���б� (Available Part Prefabs) δ���á�Ϊ�ջ���� null Ԫ�أ�", this); return false; }
        if (chassisRigidbody == null) { Debug.LogWarning("MechBuilder ���棺���ĸ��� (Chassis Rigidbody) δ�� Inspector ָ���������Զ�����...", this); chassisRigidbody = FindObjectOfType<Rigidbody>(); /* ����ȷ���� */ if (chassisRigidbody == null) { Debug.LogError("MechBuilder ���󣺺��ĸ��� (Chassis Rigidbody) �޷��Զ��ҵ���", this); return false; } }
        if (ghostMaterial == null || ghostMaterialValid == null || ghostMaterialInvalid == null) { Debug.LogWarning("MechBuilder ���棺���ֻ�ȫ���������δָ����״̬�������ܲ���ʾ��", this); }
        if (connectionPointLayerMask.value == 0) { Debug.LogWarning("MechBuilder ���棺���ӵ�ͼ������ (Connection Point Layer Mask) δ���ã����޷��������κε㡣", this); }
        return true;
    }
    /// <summary>
    /// ���ָ��������Ƿ���Ա��Ƴ�����û������������ӵ������棩��
    /// </summary>
    /// <param name="partToCheck">Ҫ������� GameObject</param>
    /// <returns>��������Ƴ��򷵻� true�����򷵻� false��</returns>
    private bool CanRemovePart(GameObject partToCheck)
    {
        if (partToCheck == null) return false;

        Rigidbody rbToCheck = partToCheck.GetComponent<Rigidbody>();
        // ����������û�� Rigidbody�����Ǽ��������ܱ��Ƴ���������һ����Ч״̬��
        if (rbToCheck == null) return false;

        // --- ������� ---
        // ���ҳ��������е� FixedJoint��ע�⣺�����������ǳ��࣬������ܻ������ܿ�����
        // �Ż�����ά��һ���ѷ�������͹ؽڵ��б�������ÿ�ζ� FindObjectsOfType��
        FixedJoint[] allJoints = FindObjectsOfType<FixedJoint>();
        foreach (FixedJoint joint in allJoints)
        {
            // ���������
            // 1. �ؽ���Ч
            // 2. �ؽ����ӵĸ��� (connectedBody) ������Ҫ���ĸ��� (rbToCheck)
            // 3. ����ؽڲ������������ڼ������屾�� (joint.gameObject != partToCheck)
            if (joint != null && joint.connectedBody == rbToCheck && joint.gameObject != partToCheck)
            {
                // �ҵ���һ���������һ����������������ϣ�
                // Debug.Log($"���: ��� '{joint.gameObject.name}' ���ӵ��� '{partToCheck.name}'�������Ƴ���"); // ���ڵ���
                return false; // �����Ƴ�
            }
        }

        // ���ѭ��������û���ҵ�������
        return true; // �����Ƴ�
    }
    // �� UI ��ť���ã�ѡ��Ҫ���õ����
    public void SelectPartToPlace(int partIndex)
    {
        if (partIndex < 0 || partIndex >= availablePartPrefabs.Count)
        {
            Debug.LogError($"����ѡ����Ч���������: {partIndex}");
            ClearSelection();
            return;
        }

        // ����ɵ����� (�������)
        if (currentGhostInstance != null)
        {
            Destroy(currentGhostInstance);
        }

        selectedPartPrefab = availablePartPrefabs[partIndex];
        currentRotationOffset = Quaternion.identity; // ������ת

        if (selectedPartPrefab != null)
        {
            Debug.Log($"��ѡ�����: {selectedPartPrefab.name}");
            // ʵ�����µ����� (λ�ú���ת�Ժ����)
            currentGhostInstance = Instantiate(selectedPartPrefab, Vector3.zero, Quaternion.identity);
            currentGhostInstance.name = selectedPartPrefab.name + "_Ghost";
            SetupGhostObject(currentGhostInstance); // ��������״̬
        }
        else
        {
            ClearSelection(); // ���Ԥ��Ϊ�գ������ѡ��
        }
    }

    // �����ǰѡ�������
    public void ClearSelection()
    {
        if (currentGhostInstance != null)
        {
            Destroy(currentGhostInstance);
        }
        selectedPartPrefab = null;
        currentGhostInstance = null;
        currentGhostContactPoints.Clear();
        isGhostPlacementValid = false;
        potentialSnapTargetPoint = null;
        potentialGhostSourcePoint = null;
        currentRotationOffset = Quaternion.identity; // Ҳ������תƫ��
        Debug.Log("���ѡ���������"); // ���Ա��������޸���־
    }


    // �������������������Ӿ�״̬
    private void SetupGhostObject(GameObject ghost) // ���� ghost ���� currentGhostInstance
    {
        if (ghost == null) return;

        // 1. ��ȡ���нӴ��㲢����
        currentGhostContactPoints.Clear();
        ghost.GetComponentsInChildren<PartContactPoint>(true, currentGhostContactPoints); // ��ȡ�����Ǽ���״̬��
        if (currentGhostContactPoints.Count == 0)
        {
            Debug.LogWarning($"ѡ������ '{ghost.name}' ��û���ҵ��κ� PartContactPoint ��������޷�ʹ�ýӴ�����롣", ghost);
        }

        // 2. ��������
        Collider[] colliders = ghost.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders) { col.enabled = false; }
        Rigidbody rb = ghost.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; }

        // 3. ���ó�ʼ������� (��Ч״̬)
        UpdateGhostAppearance(); // ʹ��ͨ�ú����������

        // 4. (��ѡ) ���ò㼶������������߼��
        // ghost.layer = LayerMask.NameToLayer("Ignore Raycast");
        // foreach (Transform child in ghost.transform) { child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); }
    }

    void Update()
    {
        /*HandleRotationInput(); // ������ת����

        if (currentGhostInstance != null)
        {
            UpdateGhostPosition(); // ��������λ�á�����״̬�����
        }

        HandlePlacementInput(); // �����������*/
        if (selectedPartPrefab != null) // --- ״̬1: ���ڷ������ ---
        {
            UnhighlightPart(); // ȷ��ȡ���Ƴ�����
            HandleRotationInput();
            UpdateGhostPosition();
            HandlePlacementInput(); // ��������
        }
        else // --- ״̬2: û��ѡ����������Խ����Ƴ����� ---
        {
            HandleRemovalInput(); // �����Ƴ��������͸���
        }
    }

    // �����û���ת����
    void HandleRotationInput()
    {
        if (currentGhostInstance == null) return; // û�������򲻴�����ת

        bool rotationInputDetected = false;
        // Yaw (Y-axis)
        if (Input.GetKeyDown(rotateCounterClockwiseKey)) { currentRotationOffset *= Quaternion.Euler(0, -rotationStep, 0); rotationInputDetected = true; }
        else if (Input.GetKeyDown(rotateClockwiseKey)) { currentRotationOffset *= Quaternion.Euler(0, rotationStep, 0); rotationInputDetected = true; }
        // Pitch (X-axis)
        else if (Input.GetKeyDown(rotatePitchForwardKey)) { currentRotationOffset *= Quaternion.Euler(rotationStep, 0, 0); rotationInputDetected = true; }
        else if (Input.GetKeyDown(rotatePitchBackwardKey)) { currentRotationOffset *= Quaternion.Euler(-rotationStep, 0, 0); rotationInputDetected = true; }
        // (��ѡ) Roll (Z-axis)
        // else if (Input.GetKeyDown(rotateRollLeftKey))  { currentRotationOffset *= Quaternion.Euler(0, 0, rotationStep); rotationInputDetected = true; }
        // else if (Input.GetKeyDown(rotateRollRightKey)) { currentRotationOffset *= Quaternion.Euler(0, 0, -rotationStep); rotationInputDetected = true; }

        // �������ת���룬ǿ�Ƹ�������λ���Է�ӳ�µ���ת (��ʹ���û��)
        // UpdateGhostPosition ���Զ�������ת��������ֻ��Ϊ��������ʵ�ʵ��� UpdateGhostPosition �͹���
        // if (rotationInputDetected) { /* UpdateGhostPosition(); // ���� Update �е��� */ }
    }
    // ���������������Ƴ��߼�
    void HandleRemovalInput()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitInfo;

        GameObject partHit = null; // ��ǰ���߻��е����
        bool isHitPartRemovable = false; // ���е�����Ƿ���Ƴ�

        // ���߼�⣬ֻ���Ĵ��� PlacedPart Tag ������
        if (Physics.Raycast(ray, out hitInfo, maxBuildDistance) && hitInfo.collider.CompareTag(placedPartTag))
        {
            // �ų����̺��� (�����Ҳ�� PlacedPart Tag)
            if (hitInfo.collider.gameObject != chassisRigidbody.gameObject)
            {
                partHit = hitInfo.collider.gameObject;
                // *** �ڸ���ǰ�ȼ���Ƿ���Ƴ� ***
                isHitPartRemovable = CanRemovePart(partHit);
            }
        }

        // --- ��������߼� ---
        if (partHit != null) // ȷʵ������һ���ɽ��������
        {
            if (isHitPartRemovable) // ������������Ա��Ƴ�
            {
                HighlightPart(partHit); // ������ (���)

                // ֻ����������Ƴ����Ѹ�����������£��Ŵ�����ɾ��
                if (Input.GetMouseButtonDown(0) && currentlyHoveredPart == partHit)
                {
                    TryRemovePart(partHit);
                }
            }
            else // ��������������������Ƴ�
            {
                // ȷ����û����ʾ��������ɫ��
                // ������֮ǰ��ͣ�ڱ�Ŀ��Ƴ�����ϣ�Ȼ���Ƶ���������Ƴ�����ϣ���Ҫȡ��֮ǰ�ĸ���
                if (currentlyHoveredPart != null && currentlyHoveredPart != partHit)
                {
                    UnhighlightPart();
                }
                else if (currentlyHoveredPart == partHit)
                {
                    // ������һֱ��ͣ����������Ƴ�����ϣ�������֮ǰ�ǿ��Ƴ��ģ�����״̬���ˣ�
                    UnhighlightPart(); // Ҳȡ������
                }

                // (��ѡ) ��������Ը������Ƴ������һ����ͬ���Ӿ������������ɫ������
                // ����������ֻʵ�ֲ���ɫ���������ﲻ��Ҫ��ʲô�ر�ġ�
                // Debug.Log($"��ͣ�ڲ����Ƴ������: {partHit.name}"); // ���ڵ���
            }
        }
        else // ����û�л����κοɽ��������
        {
            // ȡ���κο��ܴ��ڵĸ���
            UnhighlightPart();
        }
    }
    void TryRemovePart(GameObject partToRemove)
    {
        if (partToRemove == null) return;

        Rigidbody rbToRemove = partToRemove.GetComponent<Rigidbody>();
        if (rbToRemove == null)
        {
            Debug.LogError($"�޷��Ƴ� '{partToRemove.name}'����Ϊ��û�� Rigidbody �����", partToRemove);
            return;
        }

        // --- ������� ("���������") ---
        // ���ҳ��������� FixedJoint
        FixedJoint[] allJoints = FindObjectsOfType<FixedJoint>(); // ע�����ܣ���������ܴ����Ż�
        foreach (FixedJoint joint in allJoints)
        {
            // ����ҵ�һ���ؽ����ӵ��� *��Ҫ���Ƴ���* ����� Rigidbody ��
            // ��������ؽ� *������* ��Ҫ���Ƴ������屾��
            if (joint.connectedBody == rbToRemove && joint.gameObject != partToRemove)
            {
                Debug.LogWarning($"�޷��Ƴ� '{partToRemove.name}'����Ϊ��� '{joint.gameObject.name}' �������������档", partToRemove);
                // (��ѡ) �����������һ���Ӿ���������ʾ�����
                return; // ��ֹ�Ƴ�
            }
        }

        // --- ִ���Ƴ� ---
        Debug.Log($"�����Ƴ����: {partToRemove.name}");

        // 1. ��ȡ PartInfo ���ҵ����ӵ�
        PartInfo partInfo = partToRemove.GetComponent<PartInfo>();
        if (partInfo != null && partInfo.connectedToPoint != null)
        {
            // 2. ���¼������ӵ�
            partInfo.connectedToPoint.gameObject.SetActive(true);
            Debug.Log($"���¼������ӵ�: {partInfo.connectedToPoint.name}");
        }
        else
        {
            Debug.LogWarning($"�޷��ҵ� '{partToRemove.name}' ���ӵ���Ŀ�����Ϣ (PartInfo��ʧ��δ����)�������޷��ڸ�λ�����·��á�", partToRemove);
        }

        // 3. (��ѡ) ������ڸ�����������ȡ������
        if (currentlyHoveredPart == partToRemove)
        {
            UnhighlightPart(); // �������״̬
        }

        // 4. ���ٹؽ� (�������Ĺؽ�)
        FixedJoint selfJoint = partToRemove.GetComponent<FixedJoint>();
        if (selfJoint != null)
        {
            Destroy(selfJoint);
        }

        // 5. ������� GameObject
        // TODO: ��� Undo ֧�ֻ����
        // Undo.DestroyObjectImmediate(partToRemove); // ʹ�� Undo
        Destroy(partToRemove); // ��ͨ����
        Debug.Log($"��� '{partToRemove.name}' ���Ƴ���");
    }
    // ���������λ�ú�״̬
    // ���������λ�ú�״̬ (�µĶ����߼�)
    // ���������λ�ú�״̬ (�µĶ����߼�)
    private void UpdateGhostPosition()
    {
        if (currentGhostInstance == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitInfo;
        // �Ż���ֻ������ǹ��ĵĲ㣬��������������� (����������ض���)
        bool rayHit = Physics.Raycast(ray, out hitInfo, maxBuildDistance, connectionPointLayerMask.value | LayerMask.GetMask("Default") /* �������ɷ��ñ���� */ );

        isGhostPlacementValid = false;
        potentialSnapTargetPoint = null;
        potentialGhostSourcePoint = null; // ����Ǳ�ڵ�Դ��
        Transform detectedTargetPoint = null;

        // --- ���� 1: ���߼���Ѱ�������Ŀ�����ӵ� ---
        if (rayHit)
        {
            Vector3 mouseTargetPoint = hitInfo.point;
            Collider[] nearbyPoints = Physics.OverlapSphere(mouseTargetPoint, snapCheckRadius, connectionPointLayerMask);
            float closestDistSqr = float.MaxValue;

            foreach (Collider pointCollider in nearbyPoints)
            {
                // �ų���������ĽӴ���Collider (��Ҫ!)
                if (pointCollider.gameObject.activeSelf && !pointCollider.transform.IsChildOf(currentGhostInstance.transform))
                {
                    float distSqr = (mouseTargetPoint - pointCollider.transform.position).sqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        detectedTargetPoint = pointCollider.transform;
                    }
                }
            }
        }

        // --- ���� 2: ����ҵ�Ŀ��㣬ִ���µĶ����߼� ---
        if (detectedTargetPoint != null)
        {
            // --- 2a. �����ʼ��ת (�����û�����) ---
            // ������ת����Ŀ��� + ����ƫ�� + �û��ۻ���ת
            Quaternion initialRotation = detectedTargetPoint.rotation * Quaternion.Euler(placementRotationOffset) * currentRotationOffset;
            // �Ƚ�������ת�������ʼ״̬���Ա���ȷ������������е���������
            currentGhostInstance.transform.rotation = initialRotation;

            // --- 2b. �ҵ���Ŀ�귨������򡱵�����Ӵ��� ---
            Vector3 targetNormal = detectedTargetPoint.transform.up; // Ŀ����淨��
            PartContactPoint bestOpposingGhostPoint = null;
            float minDot = float.MaxValue; // ����Ҫ�ҵ����С (�) �ĵ�

            if (currentGhostContactPoints.Count > 0)
            {
                foreach (var ghostPoint in currentGhostContactPoints)
                {
                    if (ghostPoint == null) continue; // ��ȫ���
                    Vector3 ghostPointWorldUp = ghostPoint.transform.up; // ��ȡ�õ��ڵ�ǰ��ת�µ�������������
                    float dot = Vector3.Dot(ghostPointWorldUp, targetNormal);
                    if (dot < minDot)
                    {
                        minDot = dot;
                        bestOpposingGhostPoint = ghostPoint;
                    }
                }
            }

            // --- 2c. ����ҵ�����ѷ���㣬���㾫ȷ���� ---
            if (bestOpposingGhostPoint != null)
            {
                // --- ������ת��ֵ ---
                Vector3 currentGhostPointUp = bestOpposingGhostPoint.transform.up; // ��ǰ��ѵ����������
                Vector3 targetOppositeNormal = -targetNormal; // Ŀ�귨�ߵķ�����
                // ������Ҫ������ת���٣������� currentGhostPointUp ���뵽 targetOppositeNormal
                Quaternion deltaRotation = Quaternion.FromToRotation(currentGhostPointUp, targetOppositeNormal);

                // --- Ӧ��������ת ---
                // �������ֵ��תӦ�õ���ʼ��ת��
                Quaternion finalRotation = deltaRotation * initialRotation;
                currentGhostInstance.transform.rotation = finalRotation;

                // --- ��������λ�� ---
                // ��ת��ɺ󣬼�����Ҫ�ƶ����٣������� bestOpposingGhostPoint ��λ���� detectedTargetPoint �غ�
                Vector3 sourcePointWorldPos = bestOpposingGhostPoint.transform.position; // ��ȡ������ת��Դ���λ��
                Vector3 targetPointWorldPos = detectedTargetPoint.position;
                // ���㵱ǰ���飨����ת����λ����Ҫ�ټ��϶���λ��
                Vector3 deltaMovement = targetPointWorldPos - sourcePointWorldPos;
                // ��������λ��
                currentGhostInstance.transform.position = currentGhostInstance.transform.position + deltaMovement;

                // --- ����״̬ ---
                isGhostPlacementValid = true;
                potentialSnapTargetPoint = detectedTargetPoint;
                potentialGhostSourcePoint = bestOpposingGhostPoint; // ��¼����ʵ��ʹ�õ�Դ��
            }
            else // ����û�нӴ��㣬�޷�ִ�д˶���
            {
                Debug.LogWarning($"��� '{currentGhostInstance.name}' û���ҵ� PartContactPoint���޷�ִ�о�ȷ������롣", currentGhostInstance);
                // (��ѡ) ���˵��ɵ����Ķ������Ϊ��Ч
                currentGhostInstance.transform.position = detectedTargetPoint.position; // ���Ķ���
                isGhostPlacementValid = false; // ���߱��Ϊ��Ч��ǿ���û�ʹ�ô��Ӵ�������
                potentialSnapTargetPoint = detectedTargetPoint;
            }
        }
        // --- ���� 3: ���û�ҵ�Ŀ��㣬���������ƶ� (�߼�����) ---
        else if (rayHit) // �����л��е㣬������û���ӵ�
        {
            currentGhostInstance.transform.position = hitInfo.point;
            currentGhostInstance.transform.rotation = Quaternion.LookRotation(hitInfo.point - mainCamera.transform.position) * currentRotationOffset;
            isGhostPlacementValid = false;
        }
        else // ����δ�����κ�����
        {
            Vector3 posAtMaxDist = ray.origin + ray.direction * maxBuildDistance;
            currentGhostInstance.transform.position = posAtMaxDist;
            currentGhostInstance.transform.rotation = Quaternion.LookRotation(posAtMaxDist - mainCamera.transform.position) * currentRotationOffset;
            isGhostPlacementValid = false;
        }

        // --- ���� 4: ����������� (�߼�����) ---
        UpdateGhostAppearance();
    }


    // ���ݷ�����Ч�Ը����������
    private void UpdateGhostAppearance()
    {
        if (currentGhostInstance == null) return;

        Material materialToApply = isGhostPlacementValid ? ghostMaterialValid : ghostMaterialInvalid;

        // ���û��������Ч/��Ч���ʣ����˻�ʹ�û����������
        if (materialToApply == null) materialToApply = ghostMaterial;
        if (materialToApply == null) return; // ������������ʶ�û�У����޷�����

        Renderer[] renderers = currentGhostInstance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            // Ϊÿ�� Renderer �����в��ʲ������²���
            Material[] mats = new Material[rend.sharedMaterials.Length]; // ʹ�� sharedMaterials ��ȡ��λ����
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = materialToApply;
            }
            rend.materials = mats; // ʹ�� materials ��Ϊÿ�� Renderer ��������ʵ��
        }
    }


    // �����������
    void HandlePlacementInput()
    {
        if (Input.GetMouseButtonDown(0)) // ���������
        {
            // �����������飬λ����Ч���Ҽ�¼����Ч������Ŀ���
            if (currentGhostInstance != null && isGhostPlacementValid && potentialSnapTargetPoint != null)
            {
                PlacePart(potentialSnapTargetPoint, potentialGhostSourcePoint); // ����Ŀ����Դ����Ϣ
            }
            else if (currentGhostInstance != null) // �����鵫λ����Ч
            {
                Debug.Log("���λ����Ч��δ���������ӵ㣬�޷����������");
                // (���ԼӸ���ʾ��Ч��)
            }
            // else: û�����飬�����Ч��������
        }
        // (��ѡ) ����Ҽ����ȡ��ѡ��Ĺ���
        if (Input.GetMouseButtonDown(1) && currentGhostInstance != null)
        {
            ClearSelection();
        }
    }

    // ��ָ����Ŀ�����ӵ�������
    // ����: targetPoint - �����е�Ŀ�����ӵ� (���̰�װ�������Ӵ���)
    // ����: sourcePointOnGhost - (����Ϊnull) ����ʱ������ʹ�õ�Դ�Ӵ���
    void PlacePart(Transform targetPoint, PartContactPoint sourcePointOnGhost)
    {
        if (selectedPartPrefab == null || targetPoint == null) { /* ... ������ ... */ return; }

        Rigidbody targetRigidbody = targetPoint.GetComponentInParent<Rigidbody>();
        if (targetRigidbody == null) { /* ... ������ ... */ return; }

        // ʵ�������
        GameObject newPart = Instantiate(selectedPartPrefab, currentGhostInstance.transform.position, currentGhostInstance.transform.rotation);
        newPart.name = selectedPartPrefab.name;

        Rigidbody partRb = newPart.GetComponent<Rigidbody>();
        if (partRb == null) { /* ... ������ ... */ Destroy(newPart); return; }
        partRb.isKinematic = false;

        // ��� Tag �� PartInfo (���ֲ���)
        PartInfo partInfo = newPart.GetComponent<PartInfo>();
        if (partInfo != null) partInfo.connectedToPoint = targetPoint;
        else Debug.LogWarning($"��� '{newPart.name}' ȱ�� PartInfo ���...", newPart);
        newPart.tag = placedPartTag;

        // ���ø��ӹ�ϵ (���ֲ���)
        if (targetRigidbody == chassisRigidbody) newPart.transform.SetParent(chassisRigidbody.transform, true);
        // else { /* ��ѡ�����ӵ�������� */ }


        // --- �ؼ��޸ģ�ʼ����� FixedJoint ---
        Debug.Log($"��� FixedJoint ��: {newPart.name}");
        FixedJoint joint = newPart.AddComponent<FixedJoint>();
        joint.connectedBody = targetRigidbody;
        // (��ѡ) ���ö����������Ը�����Ҫȡ��ע�Ͳ�������ֵ
        // joint.breakForce = 1000;
        // joint.breakTorque = 1000;
        // --- �ؽ�����߼����� ---

        // --- *** ���������Ժ���������������Ӷ���֮�����ײ *** ---
        Collider newPartCollider = newPart.GetComponentInChildren<Collider>(); // ��ȡ������ϵ� Collider (���� GetComponent)
        Collider connectedBodyCollider = targetRigidbody.GetComponentInChildren<Collider>(); // ��ȡ���Ӷ����ϵ� Collider

        if (newPartCollider != null && connectedBodyCollider != null)
        {
            Physics.IgnoreCollision(newPartCollider, connectedBodyCollider, true); // ����Ϊ true ��ʾ������ײ
            Debug.Log($"���ú�����ײ: {newPartCollider.name} <-> {connectedBodyCollider.name}");
        }
        else
        {
            Debug.LogWarning($"δ�ܻ�ȡ����ײ�������ú���: NewPart({newPartCollider != null}), ConnectedBody({connectedBodyCollider != null})", newPart);
        }


        // ����ʹ�ù������ӵ� (�߼�����)
        // ... (���� targetPoint �� sourcePointOnGhost) ...
        PartContactPoint targetContactScript = targetPoint.GetComponent<PartContactPoint>();
        // ... (�����߼�) ...
        if (sourcePointOnGhost != null && sourcePointOnGhost.disableSelfOnConnect)
        {
            // ... (�ҵ�������������ϵ�Դ��) ...
        }


        Debug.Log($"�ɹ��� {newPart.name} ���ӵ� {targetRigidbody.name}");

        // ����״̬ (�߼�����)
        if (currentGhostInstance != null) { Destroy(currentGhostInstance); /* ... */ }
        // ... (���� selectedPartPrefab ��) ...
    }
    // ������ʾ���
    void HighlightPart(GameObject part)
    {
        if (part == null || highlightMaterial == null) return;
        if (currentlyHoveredPart == part) return; // �����ظ�����

        UnhighlightPart(); // ��ȡ��֮ǰ�ĸ���

        currentlyHoveredPart = part;
        originalPartMaterials.Clear();

        Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend != null)
            {
                originalPartMaterials[rend] = rend.sharedMaterials; // ����ԭʼ�������
                Material[] highlightedMats = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < highlightedMats.Length; i++)
                {
                    highlightedMats[i] = highlightMaterial; // Ӧ�ø�������
                }
                rend.materials = highlightedMats; // ʹ�� materials ����ʵ��
            }
        }
    }

    // ȡ��������ʾ���
    void UnhighlightPart()
    {
        if (currentlyHoveredPart == null) return;

        Renderer[] renderers = currentlyHoveredPart.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            // �ָ�ԭʼ����
            if (rend != null && originalPartMaterials.ContainsKey(rend))
            {
                rend.sharedMaterials = originalPartMaterials[rend]; // �ָ��������
            }
        }
        currentlyHoveredPart = null;
        originalPartMaterials.Clear();
    }

    // �ڳ����˳���ֹͣʱȷ��ȡ������
    void OnDisable() { UnhighlightPart(); }
    void OnApplicationQuit() { UnhighlightPart(); }

    // (��ѡ) ���� Gizmos ��������
    void OnDrawGizmos()
    {
        // ����������ⷶΧ (�������е�)
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo, maxBuildDistance))
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(hitInfo.point, snapCheckRadius);
        }

        // ��������λ�ú��ҵ������ӵ�
        if (currentGhostInstance != null)
        {
            if (isGhostPlacementValid && potentialSnapTargetPoint != null && potentialGhostSourcePoint != null)
            {
                Gizmos.color = Color.green; // ��Ч����
                Gizmos.DrawLine(potentialGhostSourcePoint.transform.position, potentialSnapTargetPoint.position);
                Gizmos.DrawWireSphere(potentialSnapTargetPoint.position, 0.1f); // ���Ŀ���
                Gizmos.DrawWireSphere(potentialGhostSourcePoint.transform.position, 0.08f); // ���Դ��
            }
            else if (potentialSnapTargetPoint != null)
            {
                Gizmos.color = Color.cyan; // �ҵ���Ŀ��㵫������Ч/δ���
                Gizmos.DrawWireSphere(potentialSnapTargetPoint.position, 0.1f);
            }

            // ������������нӴ���
            Gizmos.color = Color.magenta;
            foreach (var point in currentGhostContactPoints)
            {
                if (point != null) Gizmos.DrawWireSphere(point.transform.position, 0.05f);
            }
        }
    }

    // --- �ɵ�/δʹ�õĺ��� (����ɾ�������ο�) ---
    /*
    Vector3 CalculateBoundsBasedOffset(GameObject ghost, Transform attachPoint) { ... }
    void TryPlacePart() { ... } // �ѱ� Update �е��߼����
    */
}