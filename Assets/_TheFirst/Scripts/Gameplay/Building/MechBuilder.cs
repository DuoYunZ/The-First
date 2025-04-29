using System.Collections.Generic;
using UnityEngine;
using System.Linq; // 需要引入 Linq 来方便地查找最近点

public class MechBuilder : MonoBehaviour
{
    [Header("必要设置 (Setup)")]
    [Tooltip("指定用于点击检测的主摄像机")]
    public Camera mainCamera;
    [Tooltip("指定核心/底盘的刚体 (Rigidbody) 组件")]
    public Rigidbody chassisRigidbody;

    [Header("零件库 (Part Inventory)")]
    [Tooltip("将所有可用的零件预设拖拽到这里")]
    public List<GameObject> availablePartPrefabs;

    [Header("建造设置 (Build Settings)")]
    [Tooltip("鼠标点击射线检测的最大距离")]
    public float maxBuildDistance = 100f;
    [Tooltip("放置零件时应用的额外旋转偏移量 (相对连接点)")]
    public Vector3 placementRotationOffset = Vector3.zero;

    [Header("幽灵/预览设置 (Ghost/Preview Settings)")]
    [Tooltip("用于显示幽灵效果的半透明材质 (初始状态)")]
    public Material ghostMaterial;
    [Tooltip("幽灵处于可放置状态时的材质")]
    public Material ghostMaterialValid;
    [Tooltip("幽灵处于不可放置状态时的材质")]
    public Material ghostMaterialInvalid;

    [Header("吸附与有效性设置 (Snapping & Validity)")]
    [Tooltip("检测附近连接点 (底盘安装点 & 零件接触点) 的半径")]
    public float snapCheckRadius = 0.5f;
    [Tooltip("指定连接点所在的物理图层 (需要同时包含底盘安装点和零件接触点)")]
    public LayerMask connectionPointLayerMask; // *** 关键：确保在 Inspector 中选择了包含 AttachmentPoint 和 PartContactPoint 的层 ***

    [Header("旋转控制 (Rotation Control)")]
    [Tooltip("旋转步长 (每次按键旋转多少度)")]
    public float rotationStep = 45.0f;
    public KeyCode rotateCounterClockwiseKey = KeyCode.Q; // Yaw Left
    public KeyCode rotateClockwiseKey = KeyCode.E;     // Yaw Right
    public KeyCode rotatePitchForwardKey = KeyCode.R;    // Pitch Forward
    public KeyCode rotatePitchBackwardKey = KeyCode.F;   // Pitch Backward
    // public KeyCode rotateRollLeftKey = KeyCode.Z;    // (可选) Roll Left
    // public KeyCode rotateRollRightKey = KeyCode.C;   // (可选) Roll Right

    [Header("移除设置 (Removal Settings)")]
    [Tooltip("鼠标悬停在可移除零件上时的材质 (可选，或用颜色)")]
    public Material highlightMaterial; // 用于高亮的材质 (例如，纯红色)
    [Tooltip("用于识别已放置零件的 Tag")]
    public string placedPartTag = "PlacedPart";

    private GameObject currentlyHoveredPart = null; // 当前鼠标悬停的可移除零件
    private Dictionary<Renderer, Material[]> originalPartMaterials = new Dictionary<Renderer, Material[]>(); // 存储被高亮零件的原始材质

    // --- 私有变量 ---
    private GameObject selectedPartPrefab = null;
    private GameObject currentGhostInstance = null;
    private List<PartContactPoint> currentGhostContactPoints = new List<PartContactPoint>(); // 缓存幽灵的接触点
    private Material[] originalMaterials = null; // 仅用于旧的材质替换逻辑，如果新逻辑不需要可以移除

    private bool isGhostPlacementValid = false;
    private Transform potentialSnapTargetPoint = null; // 潜在的目标连接点 (可能是底盘安装点或零件接触点)
    private PartContactPoint potentialGhostSourcePoint = null; // 潜在的幽灵零件上的源接触点

    // 累积的用户旋转输入
    private Quaternion currentRotationOffset = Quaternion.identity;

    void Start()
    {
        if (!ValidateSetup()) return; // 验证基础设置

        if (chassisRigidbody != null)
        {
            chassisRigidbody.isKinematic = true; // 进入建造模式，固定核心
            Debug.Log($"Chassis Rigidbody '{chassisRigidbody.name}' 已设置为 Kinematic 用于建造。");
        }

        selectedPartPrefab = null; // 初始没有选中任何零件
        Debug.Log("MechBuilder 初始化完成。请点击 UI 按钮选择零件。");
    }

    // 用于在 Start 中验证必要的设置
    bool ValidateSetup()
    {
        if (mainCamera == null) { Debug.LogError("MechBuilder 错误：主摄像机 (Main Camera) 未指定！", this); return false; }
        if (availablePartPrefabs == null || availablePartPrefabs.Count == 0 || availablePartPrefabs.Any(p => p == null)) { Debug.LogError("MechBuilder 错误：零件预设列表 (Available Part Prefabs) 未设置、为空或包含 null 元素！", this); return false; }
        if (chassisRigidbody == null) { Debug.LogWarning("MechBuilder 警告：核心刚体 (Chassis Rigidbody) 未在 Inspector 指定，尝试自动查找...", this); chassisRigidbody = FindObjectOfType<Rigidbody>(); /* 更精确查找 */ if (chassisRigidbody == null) { Debug.LogError("MechBuilder 错误：核心刚体 (Chassis Rigidbody) 无法自动找到！", this); return false; } }
        if (ghostMaterial == null || ghostMaterialValid == null || ghostMaterialInvalid == null) { Debug.LogWarning("MechBuilder 警告：部分或全部幽灵材质未指定，状态反馈可能不显示。", this); }
        if (connectionPointLayerMask.value == 0) { Debug.LogWarning("MechBuilder 警告：连接点图层掩码 (Connection Point Layer Mask) 未设置！将无法吸附到任何点。", this); }
        return true;
    }
    /// <summary>
    /// 检查指定的零件是否可以被移除（即没有其他零件连接到它上面）。
    /// </summary>
    /// <param name="partToCheck">要检查的零件 GameObject</param>
    /// <returns>如果可以移除则返回 true，否则返回 false。</returns>
    private bool CanRemovePart(GameObject partToCheck)
    {
        if (partToCheck == null) return false;

        Rigidbody rbToCheck = partToCheck.GetComponent<Rigidbody>();
        // 如果零件本身没有 Rigidbody，我们假设它不能被移除（或者是一个无效状态）
        if (rbToCheck == null) return false;

        // --- 依赖检查 ---
        // 查找场景中所有的 FixedJoint。注意：如果零件数量非常多，这里可能会有性能开销。
        // 优化方向：维护一个已放置零件和关节的列表，而不是每次都 FindObjectsOfType。
        FixedJoint[] allJoints = FindObjectsOfType<FixedJoint>();
        foreach (FixedJoint joint in allJoints)
        {
            // 检查条件：
            // 1. 关节有效
            // 2. 关节连接的刚体 (connectedBody) 是我们要检查的刚体 (rbToCheck)
            // 3. 这个关节不属于我们正在检查的物体本身 (joint.gameObject != partToCheck)
            if (joint != null && joint.connectedBody == rbToCheck && joint.gameObject != partToCheck)
            {
                // 找到了一个依赖项（另一个零件连在这个零件上）
                // Debug.Log($"检查: 零件 '{joint.gameObject.name}' 连接到了 '{partToCheck.name}'，不可移除。"); // 用于调试
                return false; // 不可移除
            }
        }

        // 如果循环结束都没有找到依赖项
        return true; // 可以移除
    }
    // 由 UI 按钮调用，选择要放置的零件
    public void SelectPartToPlace(int partIndex)
    {
        if (partIndex < 0 || partIndex >= availablePartPrefabs.Count)
        {
            Debug.LogError($"尝试选择无效的零件索引: {partIndex}");
            ClearSelection();
            return;
        }

        // 清理旧的幽灵 (如果存在)
        if (currentGhostInstance != null)
        {
            Destroy(currentGhostInstance);
        }

        selectedPartPrefab = availablePartPrefabs[partIndex];
        currentRotationOffset = Quaternion.identity; // 重置旋转

        if (selectedPartPrefab != null)
        {
            Debug.Log($"已选择零件: {selectedPartPrefab.name}");
            // 实例化新的幽灵 (位置和旋转稍后更新)
            currentGhostInstance = Instantiate(selectedPartPrefab, Vector3.zero, Quaternion.identity);
            currentGhostInstance.name = selectedPartPrefab.name + "_Ghost";
            SetupGhostObject(currentGhostInstance); // 设置幽灵状态
        }
        else
        {
            ClearSelection(); // 如果预设为空，则清除选择
        }
    }

    // 清除当前选择和幽灵
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
        currentRotationOffset = Quaternion.identity; // 也重置旋转偏移
        Debug.Log("零件选择已清除。"); // 可以保留或按需修改日志
    }


    // 设置幽灵物体的物理和视觉状态
    private void SetupGhostObject(GameObject ghost) // 参数 ghost 就是 currentGhostInstance
    {
        if (ghost == null) return;

        // 1. 获取所有接触点并缓存
        currentGhostContactPoints.Clear();
        ghost.GetComponentsInChildren<PartContactPoint>(true, currentGhostContactPoints); // 获取包括非激活状态的
        if (currentGhostContactPoints.Count == 0)
        {
            Debug.LogWarning($"选择的零件 '{ghost.name}' 上没有找到任何 PartContactPoint 组件！将无法使用接触点对齐。", ghost);
        }

        // 2. 禁用物理
        Collider[] colliders = ghost.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders) { col.enabled = false; }
        Rigidbody rb = ghost.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; }

        // 3. 设置初始幽灵材质 (无效状态)
        UpdateGhostAppearance(); // 使用通用函数更新外观

        // 4. (可选) 设置层级，避免干扰射线检测
        // ghost.layer = LayerMask.NameToLayer("Ignore Raycast");
        // foreach (Transform child in ghost.transform) { child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); }
    }

    void Update()
    {
        /*HandleRotationInput(); // 处理旋转输入

        if (currentGhostInstance != null)
        {
            UpdateGhostPosition(); // 更新幽灵位置、吸附状态和外观
        }

        HandlePlacementInput(); // 处理放置输入*/
        if (selectedPartPrefab != null) // --- 状态1: 正在放置零件 ---
        {
            UnhighlightPart(); // 确保取消移除高亮
            HandleRotationInput();
            UpdateGhostPosition();
            HandlePlacementInput(); // 放置输入
        }
        else // --- 状态2: 没有选择零件，可以进行移除操作 ---
        {
            HandleRemovalInput(); // 处理移除相关输入和高亮
        }
    }

    // 处理用户旋转输入
    void HandleRotationInput()
    {
        if (currentGhostInstance == null) return; // 没有幽灵则不处理旋转

        bool rotationInputDetected = false;
        // Yaw (Y-axis)
        if (Input.GetKeyDown(rotateCounterClockwiseKey)) { currentRotationOffset *= Quaternion.Euler(0, -rotationStep, 0); rotationInputDetected = true; }
        else if (Input.GetKeyDown(rotateClockwiseKey)) { currentRotationOffset *= Quaternion.Euler(0, rotationStep, 0); rotationInputDetected = true; }
        // Pitch (X-axis)
        else if (Input.GetKeyDown(rotatePitchForwardKey)) { currentRotationOffset *= Quaternion.Euler(rotationStep, 0, 0); rotationInputDetected = true; }
        else if (Input.GetKeyDown(rotatePitchBackwardKey)) { currentRotationOffset *= Quaternion.Euler(-rotationStep, 0, 0); rotationInputDetected = true; }
        // (可选) Roll (Z-axis)
        // else if (Input.GetKeyDown(rotateRollLeftKey))  { currentRotationOffset *= Quaternion.Euler(0, 0, rotationStep); rotationInputDetected = true; }
        // else if (Input.GetKeyDown(rotateRollRightKey)) { currentRotationOffset *= Quaternion.Euler(0, 0, -rotationStep); rotationInputDetected = true; }

        // 如果有旋转输入，强制更新幽灵位置以反映新的旋转 (即使鼠标没动)
        // UpdateGhostPosition 会自动处理旋转，这里标记只是为了清晰，实际调用 UpdateGhostPosition 就够了
        // if (rotationInputDetected) { /* UpdateGhostPosition(); // 会在 Update 中调用 */ }
    }
    // 新增函数：处理移除逻辑
    void HandleRemovalInput()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitInfo;

        GameObject partHit = null; // 当前射线击中的零件
        bool isHitPartRemovable = false; // 击中的零件是否可移除

        // 射线检测，只关心带有 PlacedPart Tag 的物体
        if (Physics.Raycast(ray, out hitInfo, maxBuildDistance) && hitInfo.collider.CompareTag(placedPartTag))
        {
            // 排除底盘核心 (如果它也带 PlacedPart Tag)
            if (hitInfo.collider.gameObject != chassisRigidbody.gameObject)
            {
                partHit = hitInfo.collider.gameObject;
                // *** 在高亮前先检查是否可移除 ***
                isHitPartRemovable = CanRemovePart(partHit);
            }
        }

        // --- 处理高亮逻辑 ---
        if (partHit != null) // 确实击中了一个可交互的零件
        {
            if (isHitPartRemovable) // 如果这个零件可以被移除
            {
                HighlightPart(partHit); // 高亮它 (变红)

                // 只有在零件可移除（已高亮）的情况下，才处理点击删除
                if (Input.GetMouseButtonDown(0) && currentlyHoveredPart == partHit)
                {
                    TryRemovePart(partHit);
                }
            }
            else // 击中了零件，但它不可移除
            {
                // 确保它没有显示高亮（红色）
                // 如果鼠标之前悬停在别的可移除零件上，然后移到这个不可移除零件上，需要取消之前的高亮
                if (currentlyHoveredPart != null && currentlyHoveredPart != partHit)
                {
                    UnhighlightPart();
                }
                else if (currentlyHoveredPart == partHit)
                {
                    // 如果鼠标一直悬停在这个不可移除零件上（比如它之前是可移除的，后来状态变了）
                    UnhighlightPart(); // 也取消高亮
                }

                // (可选) 在这里可以给不可移除的零件一个不同的视觉反馈，比如灰色高亮，
                // 但现在我们只实现不变色，所以这里不需要做什么特别的。
                // Debug.Log($"悬停在不可移除的零件: {partHit.name}"); // 用于调试
            }
        }
        else // 射线没有击中任何可交互的零件
        {
            // 取消任何可能存在的高亮
            UnhighlightPart();
        }
    }
    void TryRemovePart(GameObject partToRemove)
    {
        if (partToRemove == null) return;

        Rigidbody rbToRemove = partToRemove.GetComponent<Rigidbody>();
        if (rbToRemove == null)
        {
            Debug.LogError($"无法移除 '{partToRemove.name}'，因为它没有 Rigidbody 组件。", partToRemove);
            return;
        }

        // --- 依赖检查 ("最外层优先") ---
        // 查找场景中所有 FixedJoint
        FixedJoint[] allJoints = FindObjectsOfType<FixedJoint>(); // 注意性能，如果场景很大考虑优化
        foreach (FixedJoint joint in allJoints)
        {
            // 如果找到一个关节连接到了 *将要被移除的* 物体的 Rigidbody 上
            // 并且这个关节 *不属于* 将要被移除的物体本身
            if (joint.connectedBody == rbToRemove && joint.gameObject != partToRemove)
            {
                Debug.LogWarning($"无法移除 '{partToRemove.name}'，因为零件 '{joint.gameObject.name}' 还连接在它上面。", partToRemove);
                // (可选) 可以在这里加一个视觉或声音提示给玩家
                return; // 阻止移除
            }
        }

        // --- 执行移除 ---
        Debug.Log($"尝试移除零件: {partToRemove.name}");

        // 1. 获取 PartInfo 以找到连接点
        PartInfo partInfo = partToRemove.GetComponent<PartInfo>();
        if (partInfo != null && partInfo.connectedToPoint != null)
        {
            // 2. 重新激活连接点
            partInfo.connectedToPoint.gameObject.SetActive(true);
            Debug.Log($"重新激活连接点: {partInfo.connectedToPoint.name}");
        }
        else
        {
            Debug.LogWarning($"无法找到 '{partToRemove.name}' 连接到的目标点信息 (PartInfo丢失或未设置)，可能无法在该位置重新放置。", partToRemove);
        }

        // 3. (可选) 如果正在高亮这个零件，取消高亮
        if (currentlyHoveredPart == partToRemove)
        {
            UnhighlightPart(); // 清理高亮状态
        }

        // 4. 销毁关节 (零件自身的关节)
        FixedJoint selfJoint = partToRemove.GetComponent<FixedJoint>();
        if (selfJoint != null)
        {
            Destroy(selfJoint);
        }

        // 5. 销毁零件 GameObject
        // TODO: 添加 Undo 支持会更好
        // Undo.DestroyObjectImmediate(partToRemove); // 使用 Undo
        Destroy(partToRemove); // 普通销毁
        Debug.Log($"零件 '{partToRemove.name}' 已移除。");
    }
    // 更新幽灵的位置和状态
    // 更新幽灵的位置和状态 (新的对齐逻辑)
    // 更新幽灵的位置和状态 (新的对齐逻辑)
    private void UpdateGhostPosition()
    {
        if (currentGhostInstance == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitInfo;
        // 优化：只检测我们关心的层，避免击中幽灵自身 (如果幽灵在特定层)
        bool rayHit = Physics.Raycast(ray, out hitInfo, maxBuildDistance, connectionPointLayerMask.value | LayerMask.GetMask("Default") /* 或其他可放置表面层 */ );

        isGhostPlacementValid = false;
        potentialSnapTargetPoint = null;
        potentialGhostSourcePoint = null; // 重置潜在的源点
        Transform detectedTargetPoint = null;

        // --- 步骤 1: 射线检测和寻找最近的目标连接点 ---
        if (rayHit)
        {
            Vector3 mouseTargetPoint = hitInfo.point;
            Collider[] nearbyPoints = Physics.OverlapSphere(mouseTargetPoint, snapCheckRadius, connectionPointLayerMask);
            float closestDistSqr = float.MaxValue;

            foreach (Collider pointCollider in nearbyPoints)
            {
                // 排除幽灵自身的接触点Collider (重要!)
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

        // --- 步骤 2: 如果找到目标点，执行新的对齐逻辑 ---
        if (detectedTargetPoint != null)
        {
            // --- 2a. 计算初始旋转 (包含用户输入) ---
            // 基础旋转来自目标点 + 固有偏移 + 用户累积旋转
            Quaternion initialRotation = detectedTargetPoint.rotation * Quaternion.Euler(placementRotationOffset) * currentRotationOffset;
            // 先将幽灵旋转到这个初始状态，以便正确计算后续步骤中的世界坐标
            currentGhostInstance.transform.rotation = initialRotation;

            // --- 2b. 找到与目标法线最“反向”的幽灵接触点 ---
            Vector3 targetNormal = detectedTargetPoint.transform.up; // 目标表面法线
            PartContactPoint bestOpposingGhostPoint = null;
            float minDot = float.MaxValue; // 我们要找点积最小 (最负) 的点

            if (currentGhostContactPoints.Count > 0)
            {
                foreach (var ghostPoint in currentGhostContactPoints)
                {
                    if (ghostPoint == null) continue; // 安全检查
                    Vector3 ghostPointWorldUp = ghostPoint.transform.up; // 获取该点在当前旋转下的世界向上向量
                    float dot = Vector3.Dot(ghostPointWorldUp, targetNormal);
                    if (dot < minDot)
                    {
                        minDot = dot;
                        bestOpposingGhostPoint = ghostPoint;
                    }
                }
            }

            // --- 2c. 如果找到了最佳反向点，计算精确对齐 ---
            if (bestOpposingGhostPoint != null)
            {
                // --- 计算旋转差值 ---
                Vector3 currentGhostPointUp = bestOpposingGhostPoint.transform.up; // 当前最佳点的向上向量
                Vector3 targetOppositeNormal = -targetNormal; // 目标法线的反方向
                // 计算需要额外旋转多少，才能让 currentGhostPointUp 对齐到 targetOppositeNormal
                Quaternion deltaRotation = Quaternion.FromToRotation(currentGhostPointUp, targetOppositeNormal);

                // --- 应用最终旋转 ---
                // 将这个差值旋转应用到初始旋转上
                Quaternion finalRotation = deltaRotation * initialRotation;
                currentGhostInstance.transform.rotation = finalRotation;

                // --- 计算最终位置 ---
                // 旋转完成后，计算需要移动多少，才能让 bestOpposingGhostPoint 的位置与 detectedTargetPoint 重合
                Vector3 sourcePointWorldPos = bestOpposingGhostPoint.transform.position; // 获取最终旋转后源点的位置
                Vector3 targetPointWorldPos = detectedTargetPoint.position;
                // 计算当前幽灵（已旋转）的位置需要再加上多少位移
                Vector3 deltaMovement = targetPointWorldPos - sourcePointWorldPos;
                // 设置最终位置
                currentGhostInstance.transform.position = currentGhostInstance.transform.position + deltaMovement;

                // --- 更新状态 ---
                isGhostPlacementValid = true;
                potentialSnapTargetPoint = detectedTargetPoint;
                potentialGhostSourcePoint = bestOpposingGhostPoint; // 记录我们实际使用的源点
            }
            else // 幽灵没有接触点，无法执行此对齐
            {
                Debug.LogWarning($"零件 '{currentGhostInstance.name}' 没有找到 PartContactPoint，无法执行精确表面对齐。", currentGhostInstance);
                // (可选) 回退到旧的轴心对齐或标记为无效
                currentGhostInstance.transform.position = detectedTargetPoint.position; // 轴心对齐
                isGhostPlacementValid = false; // 或者标记为无效，强制用户使用带接触点的零件
                potentialSnapTargetPoint = detectedTargetPoint;
            }
        }
        // --- 步骤 3: 如果没找到目标点，幽灵自由移动 (逻辑不变) ---
        else if (rayHit) // 射线有击中点，但附近没连接点
        {
            currentGhostInstance.transform.position = hitInfo.point;
            currentGhostInstance.transform.rotation = Quaternion.LookRotation(hitInfo.point - mainCamera.transform.position) * currentRotationOffset;
            isGhostPlacementValid = false;
        }
        else // 射线未击中任何物体
        {
            Vector3 posAtMaxDist = ray.origin + ray.direction * maxBuildDistance;
            currentGhostInstance.transform.position = posAtMaxDist;
            currentGhostInstance.transform.rotation = Quaternion.LookRotation(posAtMaxDist - mainCamera.transform.position) * currentRotationOffset;
            isGhostPlacementValid = false;
        }

        // --- 步骤 4: 更新幽灵外观 (逻辑不变) ---
        UpdateGhostAppearance();
    }


    // 根据放置有效性更新幽灵材质
    private void UpdateGhostAppearance()
    {
        if (currentGhostInstance == null) return;

        Material materialToApply = isGhostPlacementValid ? ghostMaterialValid : ghostMaterialInvalid;

        // 如果没有设置有效/无效材质，则退回使用基础幽灵材质
        if (materialToApply == null) materialToApply = ghostMaterial;
        if (materialToApply == null) return; // 如果连基础材质都没有，就无法更新

        Renderer[] renderers = currentGhostInstance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            // 为每个 Renderer 的所有材质槽设置新材质
            Material[] mats = new Material[rend.sharedMaterials.Length]; // 使用 sharedMaterials 获取槽位数量
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = materialToApply;
            }
            rend.materials = mats; // 使用 materials 会为每个 Renderer 创建材质实例
        }
    }


    // 处理放置输入
    void HandlePlacementInput()
    {
        if (Input.GetMouseButtonDown(0)) // 检测左键点击
        {
            // 条件：有幽灵，位置有效，且记录了有效的吸附目标点
            if (currentGhostInstance != null && isGhostPlacementValid && potentialSnapTargetPoint != null)
            {
                PlacePart(potentialSnapTargetPoint, potentialGhostSourcePoint); // 传递目标点和源点信息
            }
            else if (currentGhostInstance != null) // 有幽灵但位置无效
            {
                Debug.Log("点击位置无效或未吸附到连接点，无法放置零件。");
                // (可以加个提示音效等)
            }
            // else: 没有幽灵，点击无效，不处理
        }
        // (可选) 添加右键点击取消选择的功能
        if (Input.GetMouseButtonDown(1) && currentGhostInstance != null)
        {
            ClearSelection();
        }
    }

    // 在指定的目标连接点放置零件
    // 参数: targetPoint - 世界中的目标连接点 (底盘安装点或零件接触点)
    // 参数: sourcePointOnGhost - (可能为null) 放置时幽灵上使用的源接触点
    void PlacePart(Transform targetPoint, PartContactPoint sourcePointOnGhost)
    {
        if (selectedPartPrefab == null || targetPoint == null) { /* ... 错误处理 ... */ return; }

        Rigidbody targetRigidbody = targetPoint.GetComponentInParent<Rigidbody>();
        if (targetRigidbody == null) { /* ... 错误处理 ... */ return; }

        // 实例化零件
        GameObject newPart = Instantiate(selectedPartPrefab, currentGhostInstance.transform.position, currentGhostInstance.transform.rotation);
        newPart.name = selectedPartPrefab.name;

        Rigidbody partRb = newPart.GetComponent<Rigidbody>();
        if (partRb == null) { /* ... 错误处理 ... */ Destroy(newPart); return; }
        partRb.isKinematic = false;

        // 添加 Tag 和 PartInfo (保持不变)
        PartInfo partInfo = newPart.GetComponent<PartInfo>();
        if (partInfo != null) partInfo.connectedToPoint = targetPoint;
        else Debug.LogWarning($"零件 '{newPart.name}' 缺少 PartInfo 组件...", newPart);
        newPart.tag = placedPartTag;

        // 设置父子关系 (保持不变)
        if (targetRigidbody == chassisRigidbody) newPart.transform.SetParent(chassisRigidbody.transform, true);
        // else { /* 可选：连接到其他零件 */ }


        // --- 关键修改：始终添加 FixedJoint ---
        Debug.Log($"添加 FixedJoint 到: {newPart.name}");
        FixedJoint joint = newPart.AddComponent<FixedJoint>();
        joint.connectedBody = targetRigidbody;
        // (可选) 设置断裂力，可以根据需要取消注释并调整数值
        // joint.breakForce = 1000;
        // joint.breakTorque = 1000;
        // --- 关节添加逻辑结束 ---

        // --- *** 新增：尝试忽略新零件与其连接对象之间的碰撞 *** ---
        Collider newPartCollider = newPart.GetComponentInChildren<Collider>(); // 获取新零件上的 Collider (或用 GetComponent)
        Collider connectedBodyCollider = targetRigidbody.GetComponentInChildren<Collider>(); // 获取连接对象上的 Collider

        if (newPartCollider != null && connectedBodyCollider != null)
        {
            Physics.IgnoreCollision(newPartCollider, connectedBodyCollider, true); // 设置为 true 表示忽略碰撞
            Debug.Log($"设置忽略碰撞: {newPartCollider.name} <-> {connectedBodyCollider.name}");
        }
        else
        {
            Debug.LogWarning($"未能获取到碰撞体以设置忽略: NewPart({newPartCollider != null}), ConnectedBody({connectedBodyCollider != null})", newPart);
        }


        // 禁用使用过的连接点 (逻辑不变)
        // ... (禁用 targetPoint 和 sourcePointOnGhost) ...
        PartContactPoint targetContactScript = targetPoint.GetComponent<PartContactPoint>();
        // ... (禁用逻辑) ...
        if (sourcePointOnGhost != null && sourcePointOnGhost.disableSelfOnConnect)
        {
            // ... (找到并禁用新零件上的源点) ...
        }


        Debug.Log($"成功将 {newPart.name} 连接到 {targetRigidbody.name}");

        // 清理状态 (逻辑不变)
        if (currentGhostInstance != null) { Destroy(currentGhostInstance); /* ... */ }
        // ... (重置 selectedPartPrefab 等) ...
    }
    // 高亮显示零件
    void HighlightPart(GameObject part)
    {
        if (part == null || highlightMaterial == null) return;
        if (currentlyHoveredPart == part) return; // 避免重复处理

        UnhighlightPart(); // 先取消之前的高亮

        currentlyHoveredPart = part;
        originalPartMaterials.Clear();

        Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend != null)
            {
                originalPartMaterials[rend] = rend.sharedMaterials; // 备份原始共享材质
                Material[] highlightedMats = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < highlightedMats.Length; i++)
                {
                    highlightedMats[i] = highlightMaterial; // 应用高亮材质
                }
                rend.materials = highlightedMats; // 使用 materials 创建实例
            }
        }
    }

    // 取消高亮显示零件
    void UnhighlightPart()
    {
        if (currentlyHoveredPart == null) return;

        Renderer[] renderers = currentlyHoveredPart.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            // 恢复原始材质
            if (rend != null && originalPartMaterials.ContainsKey(rend))
            {
                rend.sharedMaterials = originalPartMaterials[rend]; // 恢复共享材质
            }
        }
        currentlyHoveredPart = null;
        originalPartMaterials.Clear();
    }

    // 在程序退出或停止时确保取消高亮
    void OnDisable() { UnhighlightPart(); }
    void OnApplicationQuit() { UnhighlightPart(); }

    // (可选) 绘制 Gizmos 辅助调试
    void OnDrawGizmos()
    {
        // 绘制吸附检测范围 (在鼠标击中点)
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo, maxBuildDistance))
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(hitInfo.point, snapCheckRadius);
        }

        // 绘制幽灵位置和找到的连接点
        if (currentGhostInstance != null)
        {
            if (isGhostPlacementValid && potentialSnapTargetPoint != null && potentialGhostSourcePoint != null)
            {
                Gizmos.color = Color.green; // 有效连接
                Gizmos.DrawLine(potentialGhostSourcePoint.transform.position, potentialSnapTargetPoint.position);
                Gizmos.DrawWireSphere(potentialSnapTargetPoint.position, 0.1f); // 标记目标点
                Gizmos.DrawWireSphere(potentialGhostSourcePoint.transform.position, 0.08f); // 标记源点
            }
            else if (potentialSnapTargetPoint != null)
            {
                Gizmos.color = Color.cyan; // 找到了目标点但对齐无效/未完成
                Gizmos.DrawWireSphere(potentialSnapTargetPoint.position, 0.1f);
            }

            // 绘制幽灵的所有接触点
            Gizmos.color = Color.magenta;
            foreach (var point in currentGhostContactPoints)
            {
                if (point != null) Gizmos.DrawWireSphere(point.transform.position, 0.05f);
            }
        }
    }

    // --- 旧的/未使用的函数 (可以删除或保留参考) ---
    /*
    Vector3 CalculateBoundsBasedOffset(GameObject ghost, Transform attachPoint) { ... }
    void TryPlacePart() { ... } // 已被 Update 中的逻辑替代
    */
}