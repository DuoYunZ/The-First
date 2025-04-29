using UnityEngine;
using UnityEditor; // ��Ҫ���� UnityEditor �����ռ�

public class ContactPointGenerator : Editor
{
    // --- ������ ---
    private const string ContactPointScriptName = "PartContactPoint"; // ȷ��������ֺ���Ľű���ȫһ��
    private const string DefaultContactPointTag = "PartContactPoint"; // ��ҪӦ�õ� Tag
    private const string DefaultContactPointLayer = "PartContactPoints"; // ��ҪӦ�õ� Layer
    private const string ContactPointNamePrefix = "ContactPoint_Auto_"; // �Զ����ɵ��ǰ׺

    // �� GameObject �˵������һ����ѡ��
    [MenuItem("GameObject/�Զ�������/Ϊѡ�ж������ɺ���Ӵ��� (������)", false, 10)]
    private static void GenerateBoxContactPoints()
    {
        // ��ȡ��ǰѡ�е� GameObject
        GameObject selectedObject = Selection.activeGameObject;

        // --- ���ѡ�ж����Ƿ���Ч ---
        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("����", "������ Hierarchy �� Scene ��ͼ��ѡ��һ�� GameObject��", "ȷ��");
            return;
        }

        MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("����", "ѡ�еĶ���û�� MeshFilter �����û��ָ�� Mesh��\n�޷�����������ġ�", "ȷ��");
            return;
        }

        // ��� Tag �� Layer �Ƿ���ڣ��������������ʾ
        if (!TagExists(DefaultContactPointTag))
        {
            EditorUtility.DisplayDialog("����", $"Tag '{DefaultContactPointTag}' �����ڣ�\n������ Tag Manager �д�������\n�Զ����ɵĽӴ��㽫���ᱻ���� Tag��", "֪����");
        }
        if (LayerMask.NameToLayer(DefaultContactPointLayer) == -1) // Layer ������ʱ���� -1
        {
            EditorUtility.DisplayDialog("����", $"Layer '{DefaultContactPointLayer}' �����ڣ�\n������ Layer Manager �д�������\n�Զ����ɵĽӴ��㽫���ᱻ���� Layer��", "֪����");
        }


        // --- ��ʼ���� ---
        Undo.SetCurrentGroupName("���ɺ���Ӵ���"); // ���ó�������������
        int group = Undo.GetCurrentGroup();

        Mesh mesh = meshFilter.sharedMesh;
        Bounds bounds = mesh.bounds; // ��ȡ���������µİ�Χ��

        // ��������������ĵ�ͷ��߷��� (�ڱ�������ϵ��)
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

        // ����������
        for (int i = 0; i < 6; i++)
        {
            string pointName = ContactPointNamePrefix + faceNames[i];
            Vector3 localPosition = faceCenters[i];
            Vector3 normal = faceNormals[i];

            // ������ת��ʹ�Ӵ���� Y �� (��ɫ��ͷ) ָ���߷��� (����)
            // Z �� (��ɫ��ͷ) �ᾡ�����ֳ��������ǰ�����Ϸ�
            Quaternion localRotation = Quaternion.LookRotation(normal == Vector3.up || normal == Vector3.down ? Vector3.forward : Vector3.up, normal);


            // �����µĿ� GameObject ��Ϊ�Ӷ���
            GameObject contactPointObject = new GameObject(pointName);
            Undo.RegisterCreatedObjectUndo(contactPointObject, "�����Ӵ���"); // ע�᳷��

            contactPointObject.transform.SetParent(selectedObject.transform); // ���ø�����
            contactPointObject.transform.localPosition = localPosition;     // ���þֲ�λ��
            contactPointObject.transform.localRotation = localRotation;     // ���þֲ���ת

            // ������� PartContactPoint �ű�
            System.Type contactPointScriptType = System.Type.GetType(ContactPointScriptName + ",Assembly-CSharp"); // ���Ի�ȡ�ű����� (������Ҫ���������Ŀ�ṹ����)
            if (contactPointScriptType != null)
            {
                Component addedComponent = contactPointObject.AddComponent(contactPointScriptType);
                Undo.RegisterCreatedObjectUndo(addedComponent, "��ӽӴ���ű�"); // ע���������ĳ���
            }
            else
            {
                Debug.LogWarning($"�޷��ҵ���Ϊ '{ContactPointScriptName}' �Ľű�����ȷ���ű��������ѱ��롣");
            }


            // ���� Tag (��� Tag ����)
            if (TagExists(DefaultContactPointTag))
            {
                contactPointObject.tag = DefaultContactPointTag;
                // ע�⣺ֱ���޸� tag ���ᱻ Undo ϵͳ�ܺõؼ�¼����������������Գ���
            }

            // ���� Layer (��� Layer ����)
            int layerIndex = LayerMask.NameToLayer(DefaultContactPointLayer);
            if (layerIndex != -1)
            {
                contactPointObject.layer = layerIndex;
                // ע�⣺ֱ���޸� layer ���ᱻ Undo ϵͳ�ܺõؼ�¼
            }

            Debug.Log($"���� {selectedObject.name} �����ɽӴ���: {pointName}");
        }

        Undo.CollapseUndoOperations(group); // �ϲ����в�����һ����������
        EditorUtility.DisplayDialog("�ɹ�", $"��Ϊ '{selectedObject.name}' �� 6 ��������������˽Ӵ��㡣", "�õ�");
    }


    // �������������ָ���� Tag �Ƿ����
    private static bool TagExists(string tagName)
    {
        try
        {
            // UnityEditorInternal �����ռ������� Tag �ķ������������ڲ� API�����ܱ仯
            // һ����ӵ���ȫ�ķ����ǳ��Բ���ʹ�ø� Tag �Ķ��󣬵��ⲻ����
            // ���򵥵ķ�����ֱ��ʹ�� TagsAndLayers UI ������
            // ����������һ����΢����ôֱ�ӵ���԰�ȫ�ķ�ʽ���
            GameObject checkObj = null;
            try
            {
                checkObj = new GameObject("TagCheckTemporaryObject");
                checkObj.tag = tagName; // ��� Tag �����ڣ�������׳��쳣
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
            // ���ִ��� Unity �汾�����и�ֱ�ӵ� API���� UnityEditor.TagManager.GetTags()
            // return UnityEditorInternal.InternalEditorUtility.tags.Contains(tagName); // ʹ���ڲ� API (���ܲ��ȶ�)
        }
        catch { return false; } // �����κ��쳣
    }
}