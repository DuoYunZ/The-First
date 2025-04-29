using UnityEngine;

// �ű������ڸ��� 'MechRoot' ��
public class MechController : MonoBehaviour
{
    [Header("�ƶ����� (Movement)")]
    [Tooltip("�ƶ��ٶ� (��/��)")]
    public float moveSpeed = 7f; // ���Ը�����Ҫ����

    [Header("��ת���� (Rotation)")]
    [Tooltip("���׳�����������ת�ٶ�")]
    public float rotationSpeed = 15f; // ֵԽ��ת��Խ��

    private Camera mainCamera; // ս���������������
    private Rigidbody chassisRigidbody; // �Ӷ��� ChassisCore �� Rigidbody (��ѡ, �������ڻ�ȡλ��?)
    private Transform chassisCoreTransform; // �Ӷ��� ChassisCore �� Transform

    private Vector2 moveInput; // ʹ�� Vector2 �洢 WASD ����

    void Start()
    {
        // ��ȡս��������������� (ȷ��������������� Tag ������ȷ)
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("MechController: δ�ҵ�������� (Main Camera)! ��ȷ������������ֻ��һ�� Tag Ϊ 'MainCamera' ���������", this);
            enabled = false;
            return;
        }

        // ��ȡ�Ӷ��� ChassisCore ������ (�����Ҫ)
        // ����ͨ�� Find ���� GameManager ���������ô��ݹ���
        chassisCoreTransform = transform.Find("ChassisCore"); // ���� ChassisCore ��ֱ���Ӷ��������ֲ���
        if (chassisCoreTransform != null)
        {
            chassisRigidbody = chassisCoreTransform.GetComponent<Rigidbody>();
        }
        if (chassisCoreTransform == null || chassisRigidbody == null)
        {
            Debug.LogWarning("MechController: δ���ҵ��Ӷ��� ChassisCore ���� Rigidbody��", this);
            // ���ܲ�Ӱ���ƶ������������ܿ�����Ҫ
        }

    }

    void Update()
    {
        // --- ��ȡ���� ---
        // GetAxisRaw �ṩ -1, 0, 1 ����ɢֵ���ʺϷ������
        moveInput.x = Input.GetAxisRaw("Horizontal"); // A/D -> X������
        moveInput.y = Input.GetAxisRaw("Vertical");   // W/S -> Y������ (��Ļ����)

        // --- ������ת (�û��׳������) ---
        RotateTowardsMouse();

        // --- �����ƶ� (ֱ���޸� Transform) ---
        Move();
    }

    void Move()
    {
        // --- �����������Ե��ƶ����� ---
        // 1. ��ȡ�������ǰ����������Ͷ�䵽ˮƽ�� (���� Y ��)
        Vector3 camForward = mainCamera.transform.forward;
        camForward.y = 0;
        camForward.Normalize();

        // 2. ��ȡ��������ҷ���������Ͷ�䵽ˮƽ��
        Vector3 camRight = mainCamera.transform.right;
        camRight.y = 0;
        camRight.Normalize();

        // 3. ��������������������������ƶ�����
        // moveInput.y �����������ǰ��(��Ļ����)�ƶ���moveInput.x ���������������(��Ļ����)�ƶ�
        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // 4. ���㱾֡���ƶ���
        Vector3 movement = moveDirection * moveSpeed * Time.deltaTime;

        // 5. Ӧ���ƶ� (ֱ���޸� Transform)
        transform.Translate(movement, Space.World); // ������ռ����ƶ�
    }

    void RotateTowardsMouse()
    {
        // 1. ����һ�����ߴ������������������굱ǰλ��
        Ray mouseRay = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 2. ����һ�������ˮƽ�� (����׵�ǰ Y ��ͬ��)
        Plane groundPlane = new Plane(Vector3.up, transform.position); // ʹ�û��׵�ǰ�߶���Ϊƽ��

        // 3. ����������ƽ��Ľ���
        if (groundPlane.Raycast(mouseRay, out float distance))
        {
            // ��ȡ���������ռ���ˮƽ���ϵĵ�
            Vector3 mouseWorldPos = mouseRay.GetPoint(distance);

            // 4. ����ӻ���ָ������ķ�������
            Vector3 lookDirection = mouseWorldPos - transform.position;
            lookDirection.y = 0; // ȷ��ֻ��ˮƽ����ת����̧ͷ��ͷ

            // 5. �������������Ч (����ԭ����ת)
            if (lookDirection.sqrMagnitude > 0.01f) // ��鳤��ƽ�����⿪����
            {
                // ����Ŀ����ת
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

                // 6. ʹ�� Slerp ƽ����ת��Ŀ����ת
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
}