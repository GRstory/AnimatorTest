using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class AdvancedPlayerController : MonoBehaviour
{
    // --- ������ �� ������Ʈ ���� ---
    private enum PlayerState { Grounded, Aerial, Climbing, Vaulting }
    private PlayerState currentState;

    [Header("�ٽ� ������Ʈ")]
    public Animator animator;
    public Camera playerCamera;
    private CharacterController controller;

    [Header("�⺻ ������ ����")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 70.0f;

    [Header("���� �� �߷�")]
    public float jumpForce = 7f;
    public float gravity = 25.0f;

    [Header("�� Ÿ�� ����")]
    public float climbSpeed = 3f;
    public LayerMask climbableWallLayer;

    [Header("����(����) ����")]
    public float ledgeDetectionLength = 1.0f;
    public float ledgeVaultDuration = 0.8f;

    // --- ���� ���� ���� ---
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private bool isRunning = false;
    private bool wasGrounded = true;

    // --- �ʱ�ȭ ---
    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentState = PlayerState.Grounded;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // --- ���� ���� ---
    void Update()
    {
        // 1. ���� ���� �ܰ�: ���� ���¿� ���� moveDirection�� ���
        switch (currentState)
        {
            case PlayerState.Grounded:
                HandleGroundedMovement();
                break;
            case PlayerState.Aerial:
                HandleAerialMovement();
                break;
            case PlayerState.Climbing:
                HandleClimbingMovement();
                break;
            case PlayerState.Vaulting:
                moveDirection = Vector3.zero; // ���� �߿��� ��� �������� ����
                break;
        }

        // 2. ���� ���� �ܰ�: ���� moveDirection���� ĳ���͸� ������ �̵�
        if (currentState != PlayerState.Climbing && currentState != PlayerState.Vaulting)
        {
            moveDirection.y -= gravity * Time.deltaTime; // �� Ÿ��/���� ���� �ƴ� ���� �߷� ����
        }
        controller.Move(moveDirection * Time.deltaTime);

        // 3. ���� �� �ִϸ����� ����ȭ �ܰ�: ���� �̵� ���� ���¸� �������� ���� ���� ���� �� �ִϸ����� ������Ʈ
        UpdateStateAndAnimator();

        // 4. ���� ����
        if (currentState != PlayerState.Vaulting)
        {
            HandleMouseLook();
        }
    }

    // --- ���� ���� �Լ��� ---

    void HandleGroundedMovement()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        isRunning = Input.GetKey(KeyCode.LeftShift);

        float yVelocity = moveDirection.y;
        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 forwardMove = transform.forward * verticalInput;
        Vector3 rightMove = transform.right * horizontalInput;

        moveDirection = (forwardMove + rightMove).normalized * currentSpeed;
        moveDirection.y = yVelocity;

        if (Input.GetButtonDown("Jump"))
        {
            moveDirection.y = jumpForce;
        }
    }

    void HandleAerialMovement()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector3 airMove = (transform.forward * verticalInput + transform.right * horizontalInput).normalized * (walkSpeed * 0.5f);

        moveDirection.x = airMove.x;
        moveDirection.z = airMove.z;
    }

    void HandleClimbingMovement()
    {
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");

        Vector3 climbMove = (transform.up * verticalInput) + (transform.right * horizontalInput);
        moveDirection = climbMove.normalized * climbSpeed;

        if (Input.GetButtonDown("Jump")) // ������ �� ����
        {
            currentState = PlayerState.Aerial;
        }
    }

    // --- ���� �� �ִϸ����� ����ȭ (�ٽ� ����) ---

    void UpdateStateAndAnimator()
    {
        bool isGrounded = controller.isGrounded;

        // 1. �⺻ ���� ��ȯ (�� <-> ����)
        if (currentState == PlayerState.Grounded && !isGrounded)
        {
            currentState = PlayerState.Aerial;
        }
        else if (currentState == PlayerState.Aerial && isGrounded)
        {
            currentState = PlayerState.Grounded;
        }

        // 2. ���ǿ� ���� �ൿ ���� (�� Ÿ��, ����)
        switch (currentState)
        {
            case PlayerState.Grounded:
                TryStartClimbing();
                break;
            case PlayerState.Aerial:
                if (moveDirection.y < 0) TryVaultFromLedge();
                break;
            case PlayerState.Climbing:
                if (Input.GetAxis("Vertical") > 0) TryVaultFromWallTop();
                break;
        }

        // 3. �ִϸ����� �Ķ���� ������Ʈ
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsClimbing", currentState == PlayerState.Climbing);

        if (currentState == PlayerState.Grounded)
        {
            float horizontalVelocity = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
            animator.SetFloat("Speed", horizontalVelocity);
        }

        if (currentState == PlayerState.Climbing)
        {
            animator.SetFloat("Climb_Vertical", Input.GetAxis("Vertical"));
            animator.SetFloat("Climb_Horizontal", Input.GetAxis("Horizontal"));
        }

        if (!wasGrounded && isGrounded) animator.SetTrigger("Land");
        if (currentState == PlayerState.Grounded && Input.GetButtonDown("Jump")) animator.SetTrigger("Jump");

        // 4. ���� �������� ���� ���� ���� ����
        wasGrounded = isGrounded;
    }

    // --- ���� ���� �Լ��� ---

    void TryStartClimbing()
    {
        if (Input.GetAxis("Vertical") > 0 && CanStartClimbing())
        {
            currentState = PlayerState.Climbing;
        }
    }

    void TryVaultFromLedge()
    {
        if (CheckForLedge())
        {
            StartCoroutine(VaultOverLedge());
        }
    }

    void TryVaultFromWallTop()
    {
        if (HasReachedTopOfWall())
        {
            StartCoroutine(VaultOverLedge());
        }
    }

    // --- ���� �� ���� �Լ��� ---

    private void HandleMouseLook()
    {
        rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
    }

    private bool CanStartClimbing()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, 1.0f, climbableWallLayer))
        {
            if (Vector3.Angle(hit.normal, Vector3.up) > 80f) return true;
        }
        return false;
    }

    private bool CheckForLedge()
    {
        RaycastHit wallHit;
        if (Physics.Raycast(playerCamera.transform.position, transform.forward, out wallHit, ledgeDetectionLength, climbableWallLayer))
        {
            RaycastHit ledgeHit;
            Vector3 ledgeCheckStartPoint = wallHit.point + transform.forward * 0.1f + Vector3.up * 0.5f;
            if (!Physics.Raycast(ledgeCheckStartPoint, Vector3.down, out ledgeHit, 1.0f))
            {
                return true;
            }
        }
        return false;
    }

    private bool HasReachedTopOfWall()
    {
        return !Physics.Raycast(playerCamera.transform.position + Vector3.up * 0.5f, transform.forward, 1.0f, climbableWallLayer);
    }

    // --- ���� �ڷ�ƾ ---
    private IEnumerator VaultOverLedge()
    {
        currentState = PlayerState.Vaulting;
        animator.SetTrigger("DoVault");
        yield return new WaitForSeconds(0.1f);

        Vector3 startPos = transform.position;
        Vector3 endPos = GetVaultEndPoint();
        float elapsedTime = 0;

        while (elapsedTime < ledgeVaultDuration)
        {
            transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / ledgeVaultDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        currentState = PlayerState.Grounded;
    }

    private Vector3 GetVaultEndPoint()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + transform.forward * 1.0f + Vector3.up * 2f, Vector3.down, out hit, 3.0f))
        {
            return hit.point;
        }
        return transform.position + transform.forward;
    }

    // --- ������ ����� ---
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red; // ���� �� ���� ������ (������)
        Gizmos.DrawRay(transform.position, transform.forward * ledgeDetectionLength);

        // �𼭸� ���� ������ (�Ķ���)
        RaycastHit wallHit;
        if (Physics.Raycast(transform.position, transform.forward, out wallHit, ledgeDetectionLength, climbableWallLayer))
        {
            Vector3 ledgeCheckStartPoint = wallHit.point + transform.forward * 0.1f + Vector3.up * 0.5f;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(ledgeCheckStartPoint, Vector3.down * 1.0f);
        }
    }
}