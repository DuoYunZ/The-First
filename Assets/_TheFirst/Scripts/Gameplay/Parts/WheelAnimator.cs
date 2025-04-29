using UnityEngine;

public class WheelAnimator : MonoBehaviour
{
    [Tooltip("��Ҫ��ת�������Ӿ����ֵ� Transform (�����ָ�������Ի�ȡ��һ���Ӷ���)")]
    public Transform wheelVisualTransform;
    [Tooltip("���ӹ����İ뾶 (���ڼ���ת��)")]
    public float wheelRadius = 0.5f; // *** ��Ҫ�����������ģ�ʹ�С��ȷ���� ***
    [Tooltip("��ת�� (����ģ������ľֲ���)")]
    public Vector3 rotationAxis = Vector3.right; // *** ��Ҫ�����������ģ�ͳ������� ***

    private Rigidbody chassisRigidbody; // ����������

    void Start()
    {
        // �����Զ����Ҹ���������� Rigidbody
        chassisRigidbody = GetComponentInParent<Rigidbody>();
        if (chassisRigidbody == null)
        {
            Debug.LogError("WheelAnimator δ���ڸ����ҵ� Rigidbody!", this);
            enabled = false;
            return;
        }

        // ���û���ֶ�ָ���Ӿ� Transform�����Ի�ȡ��һ���Ӷ�����Ϊ�Ӿ�����
        if (wheelVisualTransform == null && transform.childCount > 0)
        {
            wheelVisualTransform = transform.GetChild(0);
            Debug.LogWarning($"WheelAnimator δָ�� wheelVisualTransform�����Զ�ʹ�õ�һ���Ӷ���: {wheelVisualTransform.name}", this);
        }
        else if (wheelVisualTransform == null)
        {
            // ���Ҳû���Ӷ��󣬾������� Transform (���ģ�;��ڸ��ڵ�)
            wheelVisualTransform = transform;
            Debug.LogWarning($"WheelAnimator δָ�� wheelVisualTransform �����Ӷ��󣬽���ת���� Transform", this);
        }

        if (wheelRadius <= 0)
        {
            Debug.LogError("WheelAnimator �� Wheel Radius ������� 0!", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (chassisRigidbody == null || wheelVisualTransform == null) return;

        // 1. ��ȡ����������ǰ�������ϵ��ٶ�
        // Vector3 localVelocity = transform.InverseTransformDirection(chassisRigidbody.velocity);
        // float forwardSpeed = localVelocity.z; // ��ȡ�ֲ� Z ���ٶ� (���� Z ��������ǰ������)
        // --- ���߸�ͨ�õķ����������ػ���ǰ��������ٶ�ͶӰ ---
        Vector3 worldVelocity = chassisRigidbody.velocity;
        Vector3 forwardDir = chassisRigidbody.transform.forward; // ʹ�õ��̵�ǰ������
        float forwardSpeed = Vector3.Dot(worldVelocity, forwardDir);


        // 2. ���������ܳ�
        float circumference = 2f * Mathf.PI * wheelRadius;

        // 3. ����ÿ����Ҫ��ת����Ȧ (�ٶ� / �ܳ�)
        float rotationsPerSecond = (circumference > 0) ? (forwardSpeed / circumference) : 0;

        // 4. ����ÿ֡��Ҫ��ת�ĽǶ� (Ȧ�� * 360�� * ʱ��)
        float angleDelta = rotationsPerSecond * 360f * Time.deltaTime;

        // 5. ��ת���ӵ��Ӿ� Transform
        // ʹ�� Space.Self ��ʾ�ƾֲ�����ת
        wheelVisualTransform.Rotate(rotationAxis, angleDelta, Space.Self);
    }
}