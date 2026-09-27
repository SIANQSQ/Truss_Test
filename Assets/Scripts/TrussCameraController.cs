using System.Diagnostics;
using UnityEngine;

public class TrussCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private Transform target;

    [Header("Orbit")]
    [SerializeField]
    private float rotationSpeed = 3.0f;

    [SerializeField]
    private float minPitch = -80.0f;

    [SerializeField]
    private float maxPitch = 80.0f;

    [Header("Zoom")]
    [SerializeField]
    private float zoomSpeed = 5.0f;

    [SerializeField]
    private float minDistance = 2.0f;

    [SerializeField]
    private float maxDistance = 100.0f;

    [Header("Pan")]
    [SerializeField]
    private float panSpeed = 0.01f;

    [Header("Keyboard Movement")]
    [SerializeField]
    private float moveSpeed = 8.0f;

    [SerializeField]
    private float fastMoveMultiplier = 3.0f;

    [Header("Focus")]
    [SerializeField]
    private float focusDistanceMultiplier = 2.2f;

    private Vector3 focusPoint;
    private float distance;
    private float yaw;
    private float pitch;

    private void Start()
    {
        if (target == null)
        {
            UnityEngine.Debug.LogWarning(
                "TrussCameraController: Target is not assigned.");
        }

        InitializeCamera();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        HandleOrbit();
        HandlePan();
        HandleZoom();
        HandleKeyboardMovement();

        if (Input.GetKeyDown(KeyCode.F))
        {
            FocusTarget();
        }

        UpdateCameraTransform();
    }

    private void InitializeCamera()
    {
        if (target == null)
        {
            focusPoint = transform.position +
                         transform.forward * 10.0f;

            distance = 10.0f;
        }
        else
        {
            Bounds bounds = CalculateTargetBounds();

            focusPoint = bounds.center;

            float suggestedDistance =
                bounds.extents.magnitude *
                focusDistanceMultiplier;

            distance = Mathf.Clamp(
                suggestedDistance,
                minDistance,
                maxDistance);
        }

        Vector3 offset =
            transform.position - focusPoint;

        if (offset.sqrMagnitude > 0.001f)
        {
            distance = Mathf.Clamp(
                offset.magnitude,
                minDistance,
                maxDistance);

            Quaternion lookRotation =
                Quaternion.LookRotation(
                    offset.normalized,
                    Vector3.up);

            Vector3 angles =
                lookRotation.eulerAngles;

            yaw = angles.y;
            pitch = NormalizeAngle(angles.x);
        }
        else
        {
            yaw = 0.0f;
            pitch = 20.0f;
        }

        UpdateCameraTransform();
    }

    private void HandleOrbit()
    {
        if (!Input.GetMouseButton(1))
            return;

        float mouseX =
            Input.GetAxis("Mouse X");

        float mouseY =
            Input.GetAxis("Mouse Y");

        yaw += mouseX * rotationSpeed;
        pitch -= mouseY * rotationSpeed;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch);
    }

    private void HandlePan()
    {
        if (!Input.GetMouseButton(2))
            return;

        float mouseX =
            Input.GetAxis("Mouse X");

        float mouseY =
            Input.GetAxis("Mouse Y");

        Vector3 panMovement =
            (-transform.right * mouseX -
             transform.up * mouseY) *
            panSpeed *
            distance;

        focusPoint += panMovement;
    }

    private void HandleZoom()
    {
        float scroll =
            Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.001f)
            return;

        distance -= scroll * zoomSpeed;

        distance = Mathf.Clamp(
            distance,
            minDistance,
            maxDistance);
    }

    private void HandleKeyboardMovement()
    {
        float horizontal =
            Input.GetAxisRaw("Horizontal");

        float vertical =
            Input.GetAxisRaw("Vertical");

        Vector3 cameraForward =
            Vector3.ProjectOnPlane(
                transform.forward,
                Vector3.up).normalized;

        Vector3 cameraRight =
            Vector3.ProjectOnPlane(
                transform.right,
                Vector3.up).normalized;

        Vector3 movement =
            cameraRight * horizontal +
            cameraForward * vertical;

        if (movement.sqrMagnitude < 0.001f)
            return;

        float currentSpeed =
            moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed *= fastMoveMultiplier;
        }

        focusPoint +=
            movement.normalized *
            currentSpeed *
            Time.deltaTime;
    }

    private void UpdateCameraTransform()
    {
        Quaternion rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0.0f);

        Vector3 offset =
            rotation *
            Vector3.back *
            distance;

        transform.position =
            focusPoint + offset;

        transform.LookAt(
            focusPoint,
            Vector3.up);
    }

    /// <summary>
    /// Public entry point used by the runtime HUD and Escape menu.
    /// </summary>
    public void FocusModel()
    {
        FocusTarget();
        UpdateCameraTransform();
    }

    private void FocusTarget()
    {
        if (target == null)
            return;

        Bounds bounds =
            CalculateTargetBounds();

        focusPoint =
            bounds.center;

        float suggestedDistance =
            bounds.extents.magnitude *
            focusDistanceMultiplier;

        distance = Mathf.Clamp(
            suggestedDistance,
            minDistance,
            maxDistance);
    }

    private Bounds CalculateTargetBounds()
    {
        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>(
                true);

        if (renderers == null ||
            renderers.Length == 0)
        {
            return new Bounds(
                target.position,
                Vector3.one);
        }

        Bounds bounds =
            renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                bounds.Encapsulate(
                    renderers[i].bounds);
            }
        }

        return bounds;
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180.0f)
            angle -= 360.0f;

        while (angle < -180.0f)
            angle += 360.0f;

        return angle;
    }
}
