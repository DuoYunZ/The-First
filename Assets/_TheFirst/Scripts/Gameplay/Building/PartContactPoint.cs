using UnityEngine;

/// <summary>
/// ���һ����Ϸ������Ϊ����ϵ����ӵ� (�Ӵ���)��
/// ���� MechBuilder �������������߼���
/// </summary>
public class PartContactPoint : MonoBehaviour
{
    [Header("���ӵ�����")]
    [Tooltip("���ӵ�����ͣ�������ƥ����ݵ����ӡ�����: Structural, Power, Small, Large")]
    public string pointType = "Structural"; // Ĭ������Ϊ "Structural"

    [Tooltip("���ӳɹ����Ƿ���ô˽Ӵ�������")]
    public bool disableSelfOnConnect = true;

    [Tooltip("���ӳɹ����Ƿ�Ҳ���Խ���Ŀ�����ӵ㣿(���Ŀ���Ҳ�� PartContactPoint)")]
    public bool disableTargetOnConnect = true; // ͨ���ɶԽ���

    // --- δ������չ������ ---
    // [Header("�߼����� (��ѡ)")]
    // [Tooltip("��������ӵ�ľֲ������ϡ��������ڸ���ȷ����ת����")]
    // public Vector3 localUp = Vector3.up;
    // [Tooltip("��������ӵ�ľֲ�����ǰ���򡰳��⡱����")]
    // public Vector3 localForward = Vector3.forward;

    // ������ Start �� Awake �����һЩ��֤�߼� (��ѡ)
    // void Start()
    // {
    //     // �������������Ƿ񸽼�����ȷ����Ϸ����㼶��
    // }

    // (��ѡ) �ڱ༭���л��� Gizmo �Ա���ӻ�
    void OnDrawGizmos()
    {
        // ����һ��С������������ʾ���ӵ�ĳ���
        float gizmoSize = 0.1f; // Gizmo ��С
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;

        // ���ƾֲ�������: X (��ɫ), Y (��ɫ), Z (��ɫ)
        Gizmos.color = Color.red;   // X ��
        Gizmos.DrawRay(pos, rot * Vector3.right * gizmoSize);
        Gizmos.color = Color.green; // Y �� (ͨ����Ϊ�����ϡ�)
        Gizmos.DrawRay(pos, rot * Vector3.up * gizmoSize);
        Gizmos.color = Color.blue;  // Z �� (ͨ����Ϊ����ǰ��/�����⡱)
        Gizmos.DrawRay(pos, rot * Vector3.forward * gizmoSize);

        // �����ٻ�һ��С������λ��
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, gizmoSize * 0.2f);
    }
}