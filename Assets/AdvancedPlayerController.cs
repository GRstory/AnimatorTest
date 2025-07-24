using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class AdvancedPlayerController : MonoBehaviour
{
    // --- 열거형 및 컴포넌트 변수 ---
    private enum PlayerState { Grounded, Aerial, Climbing, Vaulting }
    private PlayerState currentState;

    [Header("핵심 컴포넌트")]
    public Animator animator;
    public Camera playerCamera;
    private CharacterController controller;

    [Header("기본 움직임 설정")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 70.0f;

    [Header("점프 및 중력")]
    public float jumpForce = 7f;
    public float gravity = 25.0f;

    [Header("벽 타기 설정")]
    public float climbSpeed = 3f;
    public LayerMask climbableWallLayer;

    [Header("파쿠르(난간) 설정")]
    public float ledgeDetectionLength = 1.0f;
    public float ledgeVaultDuration = 0.8f;

    // --- 내부 동작 변수 ---
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private bool isRunning = false;
    private bool wasGrounded = true;

    // --- 초기화 ---
    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentState = PlayerState.Grounded;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // --- 메인 루프 ---
    void Update()
    {
        // 1. 방향 결정 단계: 현재 상태에 따라 moveDirection을 계산
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
                moveDirection = Vector3.zero; // 파쿠르 중에는 모든 움직임을 멈춤
                break;
        }

        // 2. 물리 적용 단계: 계산된 moveDirection으로 캐릭터를 실제로 이동
        if (currentState != PlayerState.Climbing && currentState != PlayerState.Vaulting)
        {
            moveDirection.y -= gravity * Time.deltaTime; // 벽 타기/파쿠르 중이 아닐 때만 중력 적용
        }
        controller.Move(moveDirection * Time.deltaTime);

        // 3. 상태 및 애니메이터 동기화 단계: 물리 이동 후의 상태를 바탕으로 다음 상태 결정 및 애니메이터 업데이트
        UpdateStateAndAnimator();

        // 4. 시점 조작
        if (currentState != PlayerState.Vaulting)
        {
            HandleMouseLook();
        }
    }

    // --- 방향 결정 함수들 ---

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

        if (Input.GetButtonDown("Jump")) // 벽에서 손 놓기
        {
            currentState = PlayerState.Aerial;
        }
    }

    // --- 상태 및 애니메이터 동기화 (핵심 로직) ---

    void UpdateStateAndAnimator()
    {
        bool isGrounded = controller.isGrounded;

        // 1. 기본 상태 전환 (땅 <-> 공중)
        if (currentState == PlayerState.Grounded && !isGrounded)
        {
            currentState = PlayerState.Aerial;
        }
        else if (currentState == PlayerState.Aerial && isGrounded)
        {
            currentState = PlayerState.Grounded;
        }

        // 2. 조건에 따른 행동 시작 (벽 타기, 파쿠르)
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

        // 3. 애니메이터 파라미터 업데이트
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

        // 4. 다음 프레임을 위해 현재 상태 저장
        wasGrounded = isGrounded;
    }

    // --- 조건 감지 함수들 ---

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

    // --- 보조 및 감지 함수들 ---

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

    // --- 파쿠르 코루틴 ---
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

    // --- 디버깅용 기즈모 ---
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red; // 전방 벽 감지 레이저 (빨간색)
        Gizmos.DrawRay(transform.position, transform.forward * ledgeDetectionLength);

        // 모서리 감지 레이저 (파란색)
        RaycastHit wallHit;
        if (Physics.Raycast(transform.position, transform.forward, out wallHit, ledgeDetectionLength, climbableWallLayer))
        {
            Vector3 ledgeCheckStartPoint = wallHit.point + transform.forward * 0.1f + Vector3.up * 0.5f;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(ledgeCheckStartPoint, Vector3.down * 1.0f);
        }
    }
}