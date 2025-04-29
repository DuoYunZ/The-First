using UnityEngine;
using System.Collections.Generic;
using System.Linq; // ���� FindObjectsOfType (������ GetComponentsInChildren)

// ������ MechRoot ��
public class WeaponController : MonoBehaviour
{
    [Header("�Զ���������")]
    [Tooltip("�Ƿ������Զ�����")]
    public bool autoFire = true; // �������Զ���������

    private List<WeaponPart> weaponParts = new List<WeaponPart>(); // �洢�����ϵ�������������
    private Camera mainCamera; // ս����������ڻ�ȡ��귽��

    void Start()
    {
        mainCamera = Camera.main; // ��ȡ�����
        if (mainCamera == null)
        {
            Debug.LogError("WeaponController: δ�ҵ��������!", this);
            enabled = false;
            return;
        }

        FindAndRegisterWeapons();
    }

    // (��ѡ) �ṩһ�������������׽ṹ�仯ʱ�������Ƴ�/������������²�������
    public void FindAndRegisterWeapons()
    {
        Debug.Log("WeaponController: ������������...");
        // �� MechRoot �������Ӷ����в��� WeaponPart (��������Ӷ���)
        weaponParts = GetComponentsInChildren<WeaponPart>(true).ToList(); // true ��ʾ�����Ǽ���ģ��������
        Debug.Log($"WeaponController: �ҵ��� {weaponParts.Count} ������������");
    }


    void Update()
    {
        if (!autoFire || weaponParts.Count == 0)
        {
            return; // ������Զ������û����������ִ��
        }

        // --- ������귽�� ---
        Vector3 mouseWorldPos = Vector3.zero;
        bool mousePosValid = false;
        Ray mouseRay = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, transform.position); // ˮƽ��
        if (groundPlane.Raycast(mouseRay, out float distance))
        {
            mouseWorldPos = mouseRay.GetPoint(distance);
            mousePosValid = true;
        }
        // -------------------

        if (mousePosValid)
        {
            // ����ӻ�������ָ������ˮƽ����
            Vector3 targetDirection = mouseWorldPos - transform.position; // �� MechRoot ��λ��
            targetDirection.y = 0;
            targetDirection.Normalize();

            if (targetDirection.sqrMagnitude > 0.01f) // ȷ��������Ч
            {
                // --- �����������������Կ��� ---
                foreach (WeaponPart weapon in weaponParts)
                {
                    if (weapon != null && weapon.enabled) // ȷ�������ű������õ�
                    {
                        // WeaponPart �ڲ��Լ�������ȴ��ʱ��
                        // ֱ�ӵ��� Fire����������ȴ
                        weapon.Fire(targetDirection);
                    }
                }
            }
        }
    }
}