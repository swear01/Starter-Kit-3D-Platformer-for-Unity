using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    private const int k_MaxJumps = 2;

    private static readonly int k_AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int k_AnimGrounded = Animator.StringToHash("Grounded");

    [Header("References")]
    [SerializeField] private Transform m_Model;
    [SerializeField] private Animator m_Animator;
    [SerializeField] private ParticleSystem m_RunTrail;
    [SerializeField] private AudioSource m_FootstepsAudio;
    [SerializeField] private AudioClip m_JumpAudio;
    [SerializeField] private AudioClip m_LandAudio;

    [Header("Properties")]
    [SerializeField] private float m_MovementSpeed = 3.5f;
    [SerializeField] private float m_JumpStrength = 7f;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference m_MoveAction;
    [SerializeField] private InputActionReference m_JumpAction;

    private CharacterController m_Controller;
    private Transform m_View;
    private Vector3 m_CurrentVelocity;
    private Vector3 m_MovementVelocity;
    private float m_RotationDirection;
    private float m_Gravity;
    private bool m_PreviouslyFloored;
    private int m_JumpsRemaining = 0;
    private int m_Coins;
    private Vector3 m_InitialScale = Vector3.one;

    public event Action<int> OnCoinCollected;

    private void Awake()
    {
        m_Controller = GetComponent<CharacterController>();
        if (Camera.main != null)
        {
            m_View = Camera.main.transform;
        }

        if (m_Model != null)
        {
            m_InitialScale = m_Model.localScale;
        }
    }

    private void OnEnable()
    {
        m_MoveAction?.action.Enable();
        m_JumpAction?.action.Enable();
    }

    private void OnDisable()
    {
        m_MoveAction?.action.Disable();
        m_JumpAction?.action.Disable();
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        HandleControls(delta);
        HandleGravity(delta);
        HandleEffects(delta);

        m_CurrentVelocity = Vector3.Lerp(m_CurrentVelocity, m_MovementVelocity, delta * 10f);
        m_CurrentVelocity.y = -m_Gravity;
        m_Controller.Move(m_CurrentVelocity * delta);

        Vector2 flatVelocity = new Vector2(m_CurrentVelocity.z, m_CurrentVelocity.x);
        if (flatVelocity.magnitude > 0f)
        {
            m_RotationDirection = Mathf.Atan2(m_CurrentVelocity.x, m_CurrentVelocity.z);
        }

        transform.rotation = Quaternion.Euler(0f,
            Mathf.LerpAngle(transform.eulerAngles.y, m_RotationDirection * Mathf.Rad2Deg, delta * 10f), 0f);

        if (transform.position.y < -10f)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        if (m_Model != null)
        {
            m_Model.localScale = Vector3.Lerp(m_Model.localScale, m_InitialScale, delta * 10f);
        }

        bool isGrounded = m_Controller.isGrounded;
        if (isGrounded && m_Gravity > 2f && !m_PreviouslyFloored)
        {
            if (m_Model != null)
            {
                m_Model.localScale = Vector3.Scale(m_InitialScale, new Vector3(1.25f, 0.75f, 1.25f));
            }

            if (m_LandAudio != null)
            {
                Audio.Instance.Play(m_LandAudio);
            }
        }

        m_PreviouslyFloored = isGrounded;
    }

    private void HandleControls(float delta)
    {
        Vector2 rawInput = m_MoveAction != null
            ? m_MoveAction.action.ReadValue<Vector2>()
            : Vector2.zero;

        Vector3 input = new Vector3(rawInput.x, 0f, rawInput.y);

        if (m_View != null)
        {
            input = Quaternion.Euler(0f, m_View.eulerAngles.y, 0f) * input;
        }

        if (input.magnitude > 1f)
        {
            input.Normalize();
        }

        m_MovementVelocity = m_MovementSpeed * input;

        if (m_JumpAction != null && m_JumpAction.action.WasPressedThisFrame())
        {
            if (m_JumpsRemaining > 0)
            {
                Jump();
            }
        }
    }

    private void HandleGravity(float delta)
    {
        m_Gravity += 25f * delta;

        if (m_Gravity > 0f && m_Controller.isGrounded)
        {
            m_JumpsRemaining = k_MaxJumps;
            m_Gravity = 25f * delta;
        }
    }

    private void Jump()
    {
        if (m_JumpAudio != null)
        {
            Audio.Instance.Play(m_JumpAudio);
        }

        m_Gravity = -m_JumpStrength;

        if (m_Model != null)
        {
            m_Model.localScale = Vector3.Scale(m_InitialScale, new Vector3(0.5f, 1.5f, 0.5f));
        }

        m_JumpsRemaining--;
    }

    private void HandleEffects(float delta)
    {
        bool isGrounded = m_Controller.isGrounded;
        Vector2 horizontalVelocity = new Vector2(m_MovementVelocity.x, m_MovementVelocity.z);
        float speedFactor = horizontalVelocity.magnitude / m_MovementSpeed;

        if (m_Animator != null)
        {
            m_Animator.SetBool(k_AnimGrounded, isGrounded);
            m_Animator.SetFloat(k_AnimSpeed, speedFactor);
        }

        if (m_FootstepsAudio != null && isGrounded && speedFactor > 0.3f)
        {
            m_FootstepsAudio.UnPause();
            m_FootstepsAudio.pitch = speedFactor;
        }
        else if (m_FootstepsAudio != null)
        {
            m_FootstepsAudio.Pause();
        }

        if (m_RunTrail != null)
        {
            ParticleSystem.EmissionModule emissionModule = m_RunTrail.emission;
            emissionModule.enabled = isGrounded && speedFactor > 0.75f;
        }
    }

    public void CollectCoin()
    {
        m_Coins++;
        OnCoinCollected?.Invoke(m_Coins);
    }
}
