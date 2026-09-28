using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

public class View : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform m_Target;

    [Header("Zoom")]
    [SerializeField] private float m_ZoomMin = 4f;
    [SerializeField] private float m_ZoomMax = 16f;
    [SerializeField] private float m_ZoomSpeed = 25f;
    [SerializeField] private float m_ZoomSmooth = 8f;
    [SerializeField, Min(0f)] private float m_WheelZoomStep = 0.75f;

    [Header("Original Rotation")]
    [SerializeField] private float m_RotationSpeedX = 6f;
    [SerializeField] private float m_RotationSpeedY = 10f;
    [SerializeField] private float m_RotationSmooth = 6f;
    [SerializeField] private float m_MinPitch = 0f;
    [SerializeField] private float m_MaxPitch = 80f;

    [Header("Comfort Rotation (X: horizontal, Y: vertical)")]
    [Tooltip("Degrees per mouse pixel.")]
    [SerializeField] private Vector2 m_MouseSensitivity = new Vector2(0.15f, 0.12f);
    [Tooltip("Degrees per second at full stick deflection.")]
    [SerializeField] private Vector2 m_GamepadSensitivity = new Vector2(180f, 120f);

    [Header("Follow")]
    [SerializeField] private float m_FollowSmooth = 4f;
    [SerializeField, Min(0f)] private float m_ComfortFollowSmooth = 15f;
    [SerializeField] private float m_PivotHeight = 0.8f;

    [Header("Collision")]
    [SerializeField, Min(0.01f)] private float m_CameraRadius = 0.3f;
    [SerializeField] private LayerMask m_CollisionMask = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0f)] private float m_PlayerVisibleDistance = 1.5f;
    [SerializeField, Min(0f)] private float m_CollisionLookAhead = 0.8f;
    [SerializeField, Min(0f)] private float m_CollisionInDamping = 0.15f;
    [SerializeField, Min(0f)] private float m_CollisionOutDamping = 0.35f;
    [SerializeField, Min(0f)] private float m_CollisionHoldTime = 0.15f;

    [Header("Demo: F1 original / F2 improved")]
    [SerializeField] private bool m_UseOriginalCamera;

    [Header("References")]
    [SerializeField] private Transform m_CameraPivot;
    [SerializeField] private Transform m_CameraHandle;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference m_LookAction;
    [SerializeField] private InputActionReference m_ZoomAction;

    private Vector2 m_CameraRotation;
    private Vector2 m_CameraRotationSmoothed;
    private float m_Zoom = 5f;
    private float m_ZoomDelta;
    private float m_CollisionDistance;
    private float m_CollisionHoldRemaining;
    private float m_HeldCollisionDistance;
    private readonly RaycastHit[] m_HitBuffer = new RaycastHit[32];
    private Transform m_CameraTransform;
    private Camera m_Camera;
    private Renderer[] m_PlayerRenderers;
    private ShadowCastingMode[] m_ShadowModes;
    private bool m_PlayerHidden;
    private int m_PreviousVSync;
    private int m_PreviousFrameRate;
    private CursorLockMode m_PreviousCursorLock;
    private bool m_PreviousCursorVisible;

    private void OnEnable()
    {
        m_PreviousVSync = QualitySettings.vSyncCount;
        m_PreviousFrameRate = Application.targetFrameRate;
        m_PreviousCursorLock = Cursor.lockState;
        m_PreviousCursorVisible = Cursor.visible;
        SetFrameRate();
        m_LookAction?.action.Enable();
        if (m_ZoomAction != null)
        {
            m_ZoomAction.action.Enable();
            m_ZoomAction.action.performed += OnZoomPerformed;
        }
    }

    private void OnDisable()
    {
        SetPlayerVisibility(true);
        m_LookAction?.action.Disable();
        if (m_ZoomAction != null)
        {
            m_ZoomAction.action.performed -= OnZoomPerformed;
            m_ZoomAction.action.Disable();
        }
        QualitySettings.vSyncCount = m_PreviousVSync;
        Application.targetFrameRate = m_PreviousFrameRate;
        Cursor.lockState = m_PreviousCursorLock;
        Cursor.visible = m_PreviousCursorVisible;
        m_ZoomDelta = 0f;
    }

    private void SetFrameRate()
    {
#if UNITY_EDITOR
        QualitySettings.vSyncCount = m_UseOriginalCamera ? 0 : 1;
        Application.targetFrameRate = m_UseOriginalCamera ? 35 : -1;
#else
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
#endif
    }

    private void OnZoomPerformed(InputAction.CallbackContext ctx)
    {
        m_ZoomDelta += ctx.ReadValue<float>();
    }

    private void Start()
    {
        m_Camera = Camera.main;
        if (m_Target == null || m_CameraPivot == null || m_CameraHandle == null || m_Camera == null)
        {
            Debug.LogError("View requires a target, pivot, handle and Main Camera.", this);
            enabled = false;
            return;
        }

        m_PlayerRenderers = m_Target.GetComponentsInChildren<Renderer>();
        m_ShadowModes = new ShadowCastingMode[m_PlayerRenderers.Length];
        for (int i = 0; i < m_PlayerRenderers.Length; i++) m_ShadowModes[i] = m_PlayerRenderers[i].shadowCastingMode;
        m_CameraTransform = m_Camera.transform;
        Vector3 angles = transform.eulerAngles;
        m_CameraRotation = new Vector2(Mathf.Clamp(angles.x + 30f, m_MinPitch, m_MaxPitch), angles.y - 130f);
        m_Zoom = Mathf.Clamp(m_Zoom, m_ZoomMin, m_ZoomMax);
        m_CollisionDistance = m_Zoom;
        m_CollisionHoldRemaining = 0f;
        if (!m_UseOriginalCamera)
        {
            m_CameraRotationSmoothed = m_CameraRotation;
            transform.position = m_Target.position + Vector3.up * m_PivotHeight;
            transform.rotation = Quaternion.Euler(m_CameraRotation.x, m_CameraRotation.y, 0f);
            m_CameraPivot.localPosition = Vector3.back * m_Zoom;
            CaptureCursor(true);
            ApplyCameraPosition(0f);
        }
    }

    private void SetCameraMode(bool original)
    {
        if (m_UseOriginalCamera == original) return;
        m_UseOriginalCamera = original;
        Vector3 angles = transform.eulerAngles;
        m_CameraRotation = new Vector2(angles.x, angles.y);
        m_CameraRotationSmoothed = m_CameraRotation;
        m_CollisionDistance = Vector3.Distance(transform.position, m_CameraTransform.position);
        m_CollisionHoldRemaining = 0f;
        SetFrameRate();
        CaptureCursor(!original);
    }

    private void CaptureCursor(bool capture)
    {
        Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !capture;
    }

    private void LateUpdate()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.f1Key.wasPressedThisFrame) SetCameraMode(true);
            if (keyboard.f2Key.wasPressedThisFrame) SetCameraMode(false);
            if (keyboard.escapeKey.wasPressedThisFrame) CaptureCursor(false);
        }
        if (!m_UseOriginalCamera && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CaptureCursor(true);
        }

        float deltaTime = Time.deltaTime;
        HandleInput(deltaTime);
        FollowTarget(deltaTime);
        ApplyRotation(deltaTime);
        ApplyZoom(deltaTime);
        ApplyCameraPosition(deltaTime);
    }

    private void FollowTarget(float deltaTime)
    {
        Vector3 target = m_Target.position + Vector3.up * (m_UseOriginalCamera ? 0f : m_PivotHeight);
        float smooth = m_UseOriginalCamera ? m_FollowSmooth : m_ComfortFollowSmooth;
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smooth * deltaTime));
    }

    private void ApplyRotation(float deltaTime)
    {
        m_CameraRotationSmoothed = m_UseOriginalCamera
            ? Vector2.Lerp(m_CameraRotationSmoothed, m_CameraRotation, deltaTime * m_RotationSmooth)
            : m_CameraRotation;
        transform.rotation = Quaternion.Euler(m_CameraRotationSmoothed.x, m_CameraRotationSmoothed.y, 0f);
    }

    private void ApplyZoom(float deltaTime)
    {
        float amount = m_UseOriginalCamera ? deltaTime * m_ZoomSmooth : 1f - Mathf.Exp(-m_ZoomSmooth * deltaTime);
        m_CameraPivot.localPosition = Vector3.Lerp(m_CameraPivot.localPosition, Vector3.back * m_Zoom, amount);
    }

    private void ApplyCameraPosition(float deltaTime)
    {
        Vector3 offset = m_CameraHandle.position - transform.position;
        float distance = offset.magnitude;
        if (!m_UseOriginalCamera && distance > 0f)
        {
            float nearHeight = m_Camera.nearClipPlane * Mathf.Tan(m_Camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float radius = Mathf.Max(m_CameraRadius, Mathf.Sqrt(nearHeight * nearHeight * (1f + m_Camera.aspect * m_Camera.aspect) + m_Camera.nearClipPlane * m_Camera.nearClipPlane));
            Physics.SyncTransforms();
            Vector3 direction = offset / distance;
            float safeDistance = GetObstacleDistance(direction, distance, radius);
            float lookAhead = m_CollisionLookAhead;
            float desiredDistance = Mathf.Max(0f, GetObstacleDistance(direction, distance + lookAhead, radius) - lookAhead);
            if (lookAhead > 0f)
            {
                float angle = Mathf.Atan2(lookAhead, distance) * Mathf.Rad2Deg;
                desiredDistance = Mathf.Min(desiredDistance, GetObstacleDistance(Quaternion.AngleAxis(angle, Vector3.up) * direction, distance, radius));
                desiredDistance = Mathf.Min(desiredDistance, GetObstacleDistance(Quaternion.AngleAxis(-angle, Vector3.up) * direction, distance, radius));
            }
            bool pullingIn = desiredDistance < distance - 0.001f && desiredDistance < m_CollisionDistance;
            if (pullingIn)
            {
                m_CollisionHoldRemaining = m_CollisionHoldTime;
            }
            else if (m_CollisionHoldRemaining > 0f)
            {
                m_CollisionHoldRemaining = Mathf.Max(0f, m_CollisionHoldRemaining - deltaTime);
                desiredDistance = Mathf.Min(desiredDistance, m_HeldCollisionDistance);
            }
            float damping = desiredDistance < m_CollisionDistance ? m_CollisionInDamping : m_CollisionOutDamping;
            float amount = damping > 0f && deltaTime > 0f ? 1f - Mathf.Exp(-deltaTime / damping) : 1f;
            m_CollisionDistance = Mathf.Min(safeDistance, Mathf.Lerp(m_CollisionDistance, desiredDistance, amount));
            if (pullingIn) m_HeldCollisionDistance = m_CollisionDistance;
            m_CameraTransform.position = transform.position + offset.normalized * m_CollisionDistance;
        }
        else
        {
            m_CameraTransform.position = m_CameraHandle.position;
        }
        m_CameraTransform.rotation = m_CameraHandle.rotation;
        float visibleDistance = m_PlayerVisibleDistance + (m_PlayerHidden ? 0.2f : 0f);
        SetPlayerVisibility(m_UseOriginalCamera || m_CollisionDistance >= visibleDistance);
    }

    private float GetObstacleDistance(Vector3 direction, float distance, float radius)
    {
        int count = Physics.SphereCastNonAlloc(transform.position, radius, direction, m_HitBuffer, distance, m_CollisionMask, QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = m_HitBuffer;
        if (count == hits.Length)
        {
            // ponytail: rare full-buffer fallback keeps all hits; enlarge the buffer if saturation is frequent.
            hits = Physics.SphereCastAll(transform.position, radius, direction, distance, m_CollisionMask, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (!hit.transform.IsChildOf(m_Target)) distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.05f));
        }
        return distance;
    }

    private void SetPlayerVisibility(bool visible)
    {
        if (m_PlayerHidden == !visible) return;
        m_PlayerHidden = !visible;
        if (m_PlayerRenderers == null) return;
        for (int i = 0; i < m_PlayerRenderers.Length; i++)
        {
            Renderer renderer = m_PlayerRenderers[i];
            if (renderer != null && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
            {
                renderer.shadowCastingMode = visible ? m_ShadowModes[i] : ShadowCastingMode.ShadowsOnly;
            }
        }
    }

    private void HandleInput(float deltaTime)
    {
        InputAction lookAction = m_LookAction != null ? m_LookAction.action : null;
        Vector2 look = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        bool pointer = lookAction?.activeControl?.device is Pointer;
        if (!Application.isFocused || (!m_UseOriginalCamera && pointer && Cursor.lockState != CursorLockMode.Locked)) look = Vector2.zero;
        if (m_UseOriginalCamera)
        {
            look *= (lookAction?.activeControl?.device is Gamepad || lookAction?.activeControl?.device is Joystick) ? 0.5f : 0.05f;
            m_CameraRotation.x -= look.y * m_RotationSpeedX;
            m_CameraRotation.y += look.x * m_RotationSpeedY;
        }
        else
        {
            Vector2 sensitivity = pointer ? m_MouseSensitivity : m_GamepadSensitivity * deltaTime;
            m_CameraRotation.x -= look.y * sensitivity.y;
            m_CameraRotation.y = Mathf.Repeat(m_CameraRotation.y + look.x * sensitivity.x, 360f);
        }
        m_CameraRotation.x = Mathf.Clamp(m_CameraRotation.x, m_MinPitch, m_MaxPitch);

        InputAction zoomAction = m_ZoomAction != null ? m_ZoomAction.action : null;
        float scroll = m_ZoomDelta;
        m_ZoomDelta = 0f;
        if (Application.isFocused && zoomAction != null && (m_UseOriginalCamera || !(zoomAction.activeControl?.device is Mouse) || Cursor.lockState == CursorLockMode.Locked))
        {
            if (m_UseOriginalCamera) m_Zoom -= scroll * m_ZoomSpeed;
            else if (zoomAction.activeControl?.device is Mouse) m_Zoom -= scroll * 10f * m_WheelZoomStep;
            else m_Zoom -= zoomAction.ReadValue<float>() * m_ZoomSpeed * deltaTime;
            m_Zoom = Mathf.Clamp(m_Zoom, m_ZoomMin, m_ZoomMax);
        }
    }
}
