using Cinemachine;
using UnityEngine;

/// <summary>
/// Keeps a Cinemachine camera following a clamped proxy target, so the player can
/// keep moving while the camera stops at the configured X/Z boundary.
/// </summary>
[DisallowMultipleComponent]
public class CameraFollowBounds : MonoBehaviour
{
    private enum ClampPlane
    {
        WorldXZ,
        WorldXY
    }

    [Header("References")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private Transform target;
    [Tooltip("Optional. Use a BoxCollider or any 3D collider to define the camera target bounds on the X/Z plane.")]
    [SerializeField] private Collider boundsCollider;

    [Header("Bounds")]
    [Tooltip("Hub and combat scenes usually move on World X/Z. Use World X/Y only for 2D-style scenes.")]
    [SerializeField] private ClampPlane clampPlane = ClampPlane.WorldXZ;
    [Tooltip("When no Bounds Collider is assigned, enable this to use Min/Max values below. Leave off to preserve normal follow.")]
    [SerializeField] private bool useManualBounds;
    [Tooltip("Used only when Bounds Collider is not assigned and Use Manual Bounds is enabled.")]
    [SerializeField] private Vector2 minXZ = new Vector2(-20f, -20f);
    [Tooltip("Used only when Bounds Collider is not assigned and Use Manual Bounds is enabled.")]
    [SerializeField] private Vector2 maxXZ = new Vector2(20f, 20f);
    [SerializeField] private bool clampX = true;
    [SerializeField] private bool clampZ = true;
    [SerializeField] private bool assignLookAt = true;

    private Transform followProxy;

    private void Reset()
    {
        virtualCamera = GetComponent<CinemachineVirtualCamera>();
        boundsCollider = GetComponent<Collider>();
    }

    public void Configure(CinemachineVirtualCamera camera, Transform followTarget)
    {
        if (camera != null)
        {
            virtualCamera = camera;
        }

        target = followTarget;
        EnsureFollowProxy();
        UpdateFollowProxy();
        AssignCameraTarget();
    }

    private void Awake()
    {
        if (virtualCamera == null)
        {
            virtualCamera = GetComponent<CinemachineVirtualCamera>();
        }

        EnsureFollowProxy();
    }

    private void OnEnable()
    {
        AssignCameraTarget();
    }

    private void OnDestroy()
    {
        if (followProxy != null)
        {
            if (Application.isPlaying)
            {
                Destroy(followProxy.gameObject);
            }
            else
            {
                DestroyImmediate(followProxy.gameObject);
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateFollowProxy();
        AssignCameraTarget();
    }

    private void EnsureFollowProxy()
    {
        if (followProxy != null)
        {
            return;
        }

        GameObject proxyObject = new GameObject($"{name}_ClampedFollowTarget");
        proxyObject.hideFlags = HideFlags.HideInHierarchy;
        followProxy = proxyObject.transform;
    }

    private void AssignCameraTarget()
    {
        if (virtualCamera == null)
        {
            return;
        }

        Transform desiredTarget = HasActiveBounds ? followProxy : target;
        if (desiredTarget == null)
        {
            return;
        }

        if (virtualCamera.Follow != desiredTarget)
        {
            virtualCamera.Follow = desiredTarget;
        }

        if (assignLookAt && virtualCamera.LookAt != desiredTarget)
        {
            virtualCamera.LookAt = desiredTarget;
        }
    }

    private void UpdateFollowProxy()
    {
        EnsureFollowProxy();

        Vector3 clampedPosition = GetClampedTargetPosition(target.position);
        followProxy.position = clampedPosition;
        followProxy.rotation = target.rotation;
    }

    private Vector3 GetClampedTargetPosition(Vector3 position)
    {
        if (!HasActiveBounds)
        {
            return position;
        }

        Vector2 min = minXZ;
        Vector2 max = maxXZ;

        if (boundsCollider != null)
        {
            Bounds bounds = boundsCollider.bounds;
            if (clampPlane == ClampPlane.WorldXY)
            {
                min = new Vector2(bounds.min.x, bounds.min.y);
                max = new Vector2(bounds.max.x, bounds.max.y);
            }
            else
            {
                min = new Vector2(bounds.min.x, bounds.min.z);
                max = new Vector2(bounds.max.x, bounds.max.z);
            }
        }

        if (min.x > max.x)
        {
            float temp = min.x;
            min.x = max.x;
            max.x = temp;
        }

        if (min.y > max.y)
        {
            float temp = min.y;
            min.y = max.y;
            max.y = temp;
        }

        Vector2 planePosition = GetPlanePosition(position);

        if (clampX)
        {
            planePosition.x = Mathf.Clamp(planePosition.x, min.x, max.x);
        }

        if (clampZ)
        {
            planePosition.y = Mathf.Clamp(planePosition.y, min.y, max.y);
        }

        return SetPlanePosition(position, planePosition);
    }

    private bool HasActiveBounds
    {
        get { return boundsCollider != null || useManualBounds; }
    }

    private Vector2 GetPlanePosition(Vector3 position)
    {
        if (clampPlane == ClampPlane.WorldXY)
        {
            return new Vector2(position.x, position.y);
        }

        return new Vector2(position.x, position.z);
    }

    private Vector3 SetPlanePosition(Vector3 position, Vector2 planePosition)
    {
        position.x = planePosition.x;

        if (clampPlane == ClampPlane.WorldXY)
        {
            position.y = planePosition.y;
        }
        else
        {
            position.z = planePosition.y;
        }

        return position;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center;
        Vector3 size;

        if (boundsCollider != null)
        {
            Bounds bounds = boundsCollider.bounds;
            center = bounds.center;
            size = clampPlane == ClampPlane.WorldXY
                ? new Vector3(bounds.size.x, bounds.size.y, 0.05f)
                : new Vector3(bounds.size.x, 0.05f, bounds.size.z);
        }
        else
        {
            float minX = Mathf.Min(minXZ.x, maxXZ.x);
            float maxX = Mathf.Max(minXZ.x, maxXZ.x);
            float minSecondAxis = Mathf.Min(minXZ.y, maxXZ.y);
            float maxSecondAxis = Mathf.Max(minXZ.y, maxXZ.y);

            if (clampPlane == ClampPlane.WorldXY)
            {
                center = new Vector3((minX + maxX) * 0.5f, (minSecondAxis + maxSecondAxis) * 0.5f, transform.position.z);
                size = new Vector3(maxX - minX, maxSecondAxis - minSecondAxis, 0.05f);
            }
            else
            {
                center = new Vector3((minX + maxX) * 0.5f, transform.position.y, (minSecondAxis + maxSecondAxis) * 0.5f);
                size = new Vector3(maxX - minX, 0.05f, maxSecondAxis - minSecondAxis);
            }
        }

        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.75f);
        Gizmos.DrawWireCube(center, size);
    }
}
