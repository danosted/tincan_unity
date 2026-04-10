using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles first-person player movement using CharacterController.
/// Supports walking on slopes, jumping, and gravity.
/// Foundation for expanded movement system (sprinting, swimming, flying, etc).
/// </summary>
public class PlayerMovement : ControllableActorBase
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 7f;
    [SerializeField] private float _acceleration = 30f;
    [SerializeField] private float _deceleration = 20f;
    [SerializeField] private float _sprintMultiplier = 1.8f;

    [Header("Jumping")]
    [SerializeField] private float _jumpForce = 15f;
    [SerializeField] private float _jumpCooldown = 0.25f;
    [SerializeField] private float _gravity = 20f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask _groundLayer;

    private CharacterController _characterController;
    private Vector3 _velocity = Vector3.zero;
    private bool _isGrounded = false;
    private float _jumpCooldownTimer = 0f;
    private Vector3 _moveDirection = Vector3.zero;
    private float _currentSpeed = 0f;
    private float _groundCheckDistance = 0f;
    private Rigidbody _currentAirshipRigidbody = null; // Reference to airship's Rigidbody when boarded
    private bool _isAboardAirship = false; // Flag if player is currently on an airship

    private ControllableActorBase _controllableActor;

    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
        {
            Debug.LogError("CharacterController component not found on " + gameObject.name);
        }

        // Calculate ground check distance dynamically based on character height
        // Check distance is 60% of character height below feet
        _groundCheckDistance = _characterController.height * 0.6f;
    }

    /// <summary>
    /// Sets the airship's Rigidbody for relative movement when boarded.
    /// </summary>
    /// <param name="airshipRigidbody">The Rigidbody of the airship.</param>
    /// <param name="isBoarded">True if the player is currently boarded, false otherwise.</param>
    public void SetAirshipParent(Rigidbody airshipRigidbody, bool isBoarded)
    {
        _currentAirshipRigidbody = airshipRigidbody;
        _isAboardAirship = isBoarded;
    }

    private void Update()
    {
        if (IsControlsEnabled is false) return;
        UpdateGroundCheck();
        HandleJump();
        ApplyGravity();
        HandleMovementInput();
        Move();

        _jumpCooldownTimer -= Time.deltaTime;
    }

    /// <summary>
    /// Processes WASD input and calculates movement direction.
    /// </summary>
    private void HandleMovementInput()
    {
        // Get input from new Input System
        float horizontalInput = 0f;
        float verticalInput = 0f;

        if (Keyboard.current.aKey.isPressed) horizontalInput -= 1f;
        if (Keyboard.current.dKey.isPressed) horizontalInput += 1f;
        if (Keyboard.current.wKey.isPressed) verticalInput += 1f;
        if (Keyboard.current.sKey.isPressed) verticalInput -= 1f;

        // Calculate movement direction relative to where player is looking
        _moveDirection = (transform.forward * verticalInput + transform.right * horizontalInput).normalized;

        // Determine target speed based on sprint
        float targetSpeed = _moveSpeed;
        if (Keyboard.current.leftShiftKey.isPressed && _isGrounded)
        {
            targetSpeed *= _sprintMultiplier;
        }

        // If no input, stop immediately. Otherwise accelerate to target speed
        if (_moveDirection.magnitude == 0)
        {
            _currentSpeed = 0f;
        }
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, _acceleration * Time.deltaTime);
        }

        // Apply velocity
        _velocity.x = _moveDirection.x * _currentSpeed;
        _velocity.z = _moveDirection.z * _currentSpeed;
    }

    /// <summary>
    /// Handles jump input and impulse.
    /// </summary>
    private void HandleJump()
    {
        bool spacePressed = Keyboard.current.spaceKey.wasPressedThisFrame;
        bool canJump = _isGrounded && _jumpCooldownTimer <= 0f && !_isAboardAirship; // Disable jumping if aboard airship

        // Debug output
        if (spacePressed)
        {
            Debug.Log($"Space pressed! Grounded: {_isGrounded}, CooldownReady: {_jumpCooldownTimer <= 0f}, CanJump: {canJump}");
        }

        if (spacePressed && canJump)
        {
            _velocity.y = _jumpForce;
            _jumpCooldownTimer = _jumpCooldown;
            Debug.Log($"Jump executed! Velocity.y = {_jumpForce}");
        }
    }

    /// <summary>
    /// Applies gravity to the player.
    /// </summary>
    private void ApplyGravity()
    {
        // If aboard airship and grounded, apply a slight downward force to keep player "glued" to the floor
        if (_isAboardAirship && _isGrounded)
        {
            _velocity.y = -2f; // Small constant downward force
        }
        // Otherwise, apply normal gravity
        else if (!_isGrounded)
        {
            _velocity.y -= _gravity * Time.deltaTime;
        }
    }

    /// <summary>
    /// Moves the character controller based on current velocity.
    /// </summary>
    private void Move()
    {
        Vector3 finalMove = _velocity * Time.deltaTime;

        // If boarded on an airship, add the airship's velocity to player's movement
        if (_isAboardAirship && _currentAirshipRigidbody != null)
        {
            finalMove += _currentAirshipRigidbody.linearVelocity * Time.deltaTime;
        }

        _characterController.Move(finalMove);
    }

    /// <summary>
    /// Checks if player is grounded using raycast.
    /// Raycast distance is dynamically calculated based on character height.
    /// </summary>
    private void UpdateGroundCheck()
    {
        RaycastHit hit;
        // Start raycast from center of capsule, go down
        Vector3 rayStart = transform.position;

        // Try ground layer first, then fall back to all layers if ground layer isn't set
        bool groundLayerIsValid = _groundLayer != 0;

        if (groundLayerIsValid)
        {
            _isGrounded = Physics.Raycast(rayStart, Vector3.down, out hit, _groundCheckDistance, _groundLayer);
        }
        else
        {
            // Fallback: use any collider
            _isGrounded = Physics.Raycast(rayStart, Vector3.down, out hit, _groundCheckDistance);
        }

        // Debug.Log($"Ground check: rayStart={rayStart}, distance={_groundCheckDistance}, grounded={_isGrounded}");

        // Reset fall velocity when grounded
        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = 0f;
        }
    }

    /// <summary>
    /// Gets current speed for animations or UI display.
    /// </summary>
    public float GetCurrentSpeed() => _currentSpeed;

    /// <summary>
    /// Gets whether player is currently grounded.
    /// </summary>
    public bool IsGrounded() => _isGrounded;
}
