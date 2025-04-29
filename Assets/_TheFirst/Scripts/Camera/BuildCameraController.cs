using UnityEngine;

public class BuildCameraController : MonoBehaviour
{
    [Header("Ŀ�������")]
    [Tooltip("�����Χ����ת��Ŀ�� (��� ChassisCore)")]
    public Transform target; // �� ChassisCore �ϵ�����
    [Tooltip("����Ҽ�����ʱ��������ת�ӽ�")]
    public bool requireMouseButton = true;
    public int mouseButtonIndex = 1; // 0=���, 1=�Ҽ�, 2=�м�

    [Header("����������")]
    [Tooltip("��ʼ�Լ���ǰ���������Ŀ��ľ���")]
    public float distance = 5.0f;
    [Tooltip("�����ٶ�")]
    public float zoomSpeed = 4f;
    [Tooltip("��С����")]
    public float minDistance = 1f;
    [Tooltip("������")]
    public float maxDistance = 15f;

    [Header("��ת�ٶ�������")]
    [Tooltip("ˮƽ��ת�ٶ� (X��)")]
    public float xSpeed = 120.0f;
    [Tooltip("��ֱ��ת�ٶ� (Y��)")]
    public float ySpeed = 120.0f;
    [Tooltip("��ֱ�Ƕȵ���С���� (���¿�)")]
    public float yMinLimit = -20f;
    [Tooltip("��ֱ�Ƕȵ�������� (���Ͽ�)")]
    public float yMaxLimit = 80f;
    [Tooltip("��ת���� (��ֵԽ��ֹͣԽ��)")]
    public float rotationDamping = 3.0f; // ��΢��������ת��ƽ��

    // ˽�б���
    private float x = 0.0f;
    private float y = 0.0f;
    private Vector3 targetPosition;

    void Start()
    {
        // ��ʼ���Ƕ�
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        // ȷ����Ŀ��
        if (target == null)
        {
            Debug.LogError("BuildCameraController: Target δ����!", this);
            enabled = false;
            return;
        }
        targetPosition = target.position; // ��ʼĿ��λ��
    }

    // ʹ�� LateUpdate ����ȷ��Ŀ�������Ѿ���������е��ƶ�����ת
    void LateUpdate()
    {
        if (target == null) return; // ���Ŀ�궪ʧ��ִ��

        targetPosition = target.position; // ����Ŀ��λ��

        // ����Ƿ���ָ������갴�� (��� requireMouseButton Ϊ true)
        bool mouseButtonPressed = !requireMouseButton || Input.GetMouseButton(mouseButtonIndex);

        // ������ת����
        if (mouseButtonPressed)
        {
            x += Input.GetAxis("Mouse X") * xSpeed * 0.02f; // ����0.02f��Ϊ����ɰ�Input Manager��Ϊ����
            y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;

            // ���ƴ�ֱ�Ƕ�
            y = ClampAngle(y, yMinLimit, yMaxLimit);
        }

        // ������ת
        Quaternion targetRotation = Quaternion.Euler(y, x, 0);

        // ������������
        distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, minDistance, maxDistance);

        // ���������λ��
        // ��Ŀ������������ distance ����
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 targetCamPosition = targetRotation * negDistance + targetPosition;

        // Ӧ��λ�ú���ת (ʹ�� Lerp ʵ��ƽ������)
        // �������Ҫƽ��������ֱ�Ӹ�ֵ: transform.rotation = targetRotation; transform.position = targetCamPosition;
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationDamping * 10f); // ����10��Ϊ��������Ч��������
        transform.position = Vector3.Lerp(transform.position, targetCamPosition, Time.deltaTime * rotationDamping * 10f);

    }

    // ���ߺ��������Ƕ������� min �� max ֮��
    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F)
            angle += 360F;
        if (angle > 360F)
            angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}