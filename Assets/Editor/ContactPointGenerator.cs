using UnityEngine;
using UnityEditor; // 需要引入 UnityEditor 命名空间

public class ContactPointGenerator : Editor
{
    // --- 配置项 ---
    private const string ContactPointScriptName = "PartContactPoint"; // 确保这个名字和你的脚本完全一致
    private const string DefaultContactPointTag = "PartContactPoint"; // 将要应用的 Tag
    private const string DefaultContactPointLayer = "PartContactPoints"; // 将要应用的 Layer
    private const string ContactPointNamePrefix = "ContactPoint_Auto_"; // 自动生成点的前缀

    // 在 GameObject 菜单下添加一个新选项
    [MenuItem("GameObject/自动化工具/为选中对象生成盒体接触点 (面中心)", false, 10)]
    private static void GenerateBoxContactPoints()
    {
        // 获取当前选中的 GameObject
        GameObject selectedObject = Selection.activeGameObject;

        // --- 检查选中对象是否有效 ---
        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("错误", "请先在 Hierarchy 或 Scene 视图中选择一个 GameObject。", "确定");
            return;
        }

        MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("错误", "选中的对象没有 MeshFilter 组件或没有指定 Mesh。\n无法计算表面中心。", "确定");
            return;
        }

        // 检查 Tag 和 Layer 是否存在，如果不存在则提示
        if (!TagExists(DefaultContactPointTag))
        {
            EditorUtility.DisplayDialog("警告", $"Tag '{DefaultContactPointTag}' 不存在！\n请先在 Tag Manager 中创建它。\n自动生成的接触点将不会被设置 Tag。", "知道了");
        }
        if (LayerMask.NameToLayer(DefaultContactPointLayer) == -1) // Layer 不存在时返回 -1
        {
            EditorUtility.DisplayDialog("警告", $"Layer '{DefaultContactPointLayer}' 不存在！\n请先在 Layer Manager 中创建它。\n自动生成的接触点将不会被设置 Layer。", "知道了");
        }


        // --- 开始生成 ---
        Undo.SetCurrentGroupName("生成盒体接触点"); // 设置撤销操作的名称
        int group = Undo.GetCurrentGroup();

        Mesh mesh = meshFilter.sharedMesh;
        Bounds bounds = mesh.bounds; // 获取本地坐标下的包围盒

        // 定义六个面的中心点和法线方向 (在本地坐标系中)
        Vector3[] faceCenters = new Vector3[] {
            bounds.center + Vector3.right * bounds.extents.x,   // Right (+X)
            bounds.center + Vector3.left * bounds.extents.x,    // Left (-X)
            bounds.center + Vector3.up * bounds.extents.y,      // Top (+Y)
            bounds.center + Vector3.down * bounds.extents.y,    // Bottom (-Y)
            bounds.center + Vector3.forward * bounds.extents.z, // Front (+Z)
            bounds.center + Vector3.back * bounds.extents.z     // Back (-Z)
        };

        Vector3[] faceNormals = new Vector3[] {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back
        };

        string[] faceNames = new string[] {
            "Right", "Left", "Top", "Bottom", "Front", "Back"
        };

        // 遍历六个面
        for (int i = 0; i < 6; i++)
        {
            string pointName = ContactPointNamePrefix + faceNames[i];
            Vector3 localPosition = faceCenters[i];
            Vector3 normal = faceNormals[i];

            // 计算旋转，使接触点的 Y 轴 (绿色箭头) 指向法线方向 (朝外)
            // Z 轴 (蓝色箭头) 会尽量保持朝向世界的前方或上方
            Quaternion localRotation = Quaternion.LookRotation(normal == Vector3.up || normal == Vector3.down ? Vector3.forward : Vector3.up, normal);


            // 创建新的空 GameObject 作为子对象
            GameObject contactPointObject = new GameObject(pointName);
            Undo.RegisterCreatedObjectUndo(contactPointObject, "创建接触点"); // 注册撤销

            contactPointObject.transform.SetParent(selectedObject.transform); // 设置父对象
            contactPointObject.transform.localPosition = localPosition;     // 设置局部位置
            contactPointObject.transform.localRotation = localRotation;     // 设置局部旋转

            // 尝试添加 PartContactPoint 脚本
            System.Type contactPointScriptType = System.Type.GetType(ContactPointScriptName + ",Assembly-CSharp"); // 尝试获取脚本类型 (可能需要根据你的项目结构调整)
            if (contactPointScriptType != null)
            {
                Component addedComponent = contactPointObject.AddComponent(contactPointScriptType);
                Undo.RegisterCreatedObjectUndo(addedComponent, "添加接触点脚本"); // 注册添加组件的撤销
            }
            else
            {
                Debug.LogWarning($"无法找到名为 '{ContactPointScriptName}' 的脚本。请确保脚本存在且已编译。");
            }


            // 设置 Tag (如果 Tag 存在)
            if (TagExists(DefaultContactPointTag))
            {
                contactPointObject.tag = DefaultContactPointTag;
                // 注意：直接修改 tag 不会被 Undo 系统很好地记录，但创建对象本身可以撤销
            }

            // 设置 Layer (如果 Layer 存在)
            int layerIndex = LayerMask.NameToLayer(DefaultContactPointLayer);
            if (layerIndex != -1)
            {
                contactPointObject.layer = layerIndex;
                // 注意：直接修改 layer 不会被 Undo 系统很好地记录
            }

            Debug.Log($"已在 {selectedObject.name} 上生成接触点: {pointName}");
        }

        Undo.CollapseUndoOperations(group); // 合并所有操作到一个撤销步骤
        EditorUtility.DisplayDialog("成功", $"已为 '{selectedObject.name}' 在 6 个面的中心生成了接触点。", "好的");
    }


    // 辅助函数：检查指定的 Tag 是否存在
    private static bool TagExists(string tagName)
    {
        try
        {
            // UnityEditorInternal 命名空间包含检查 Tag 的方法，但它是内部 API，可能变化
            // 一个间接但安全的方法是尝试查找使用该 Tag 的对象，但这不完美
            // 更简单的方法是直接使用 TagsAndLayers UI 来管理
            // 这里我们用一个稍微不那么直接但相对安全的方式检查
            GameObject checkObj = null;
            try
            {
                checkObj = new GameObject("TagCheckTemporaryObject");
                checkObj.tag = tagName; // 如果 Tag 不存在，这里会抛出异常
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
            finally
            {
                if (checkObj != null) Object.DestroyImmediate(checkObj);
            }
            // 更现代的 Unity 版本可能有更直接的 API，如 UnityEditor.TagManager.GetTags()
            // return UnityEditorInternal.InternalEditorUtility.tags.Contains(tagName); // 使用内部 API (可能不稳定)
        }
        catch { return false; } // 捕获任何异常
    }
}