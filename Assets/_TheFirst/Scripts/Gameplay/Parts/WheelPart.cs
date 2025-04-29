using UnityEngine;

public class WheelPart : MonoBehaviour
{
    [Tooltip("����Χ���ĸ��ֲ���������ת (���� Vector3.right �� Vector3.up)")]
    public Vector3 hingeAxis = Vector3.right; // *** ��Ҫ�����������ģ�ͳ��������� ***

    [Tooltip("�Ƿ�Ĭ���������������")]
    public bool useMotorByDefault = true; // �������ͨ�������������

    [Tooltip("Ĭ�����������")]
    public float motorForce = 100f; // ʾ��ֵ

    [Tooltip("Ĭ�����Ŀ���ٶ�")]
    public float motorTargetVelocity = 1000f; // ʾ��ֵ

    // ������������������������е����ԣ���뾶��Ħ��ϵ����
}