using UnityEngine;
using System.Collections;

// 이 스크립트가 작동하려면 씬에 CharacterController가 반드시 필요함
[RequireComponent(typeof(CharacterController))]
public class AdvancedPlayerController : MonoBehaviour
{
    // --- 플레이어의 현재 상태를 명확히 구분하기 위한 열거형 ---
    private enum PlayerState
    {
        Grounded,   // 땅에 있는 모든 상태 (정지, 걷기, 달리기)
        Aerial,     // 공중에 있는 모든 상태 (점프, 추락)
        Climbing,   // 벽에 붙어있는 상태
        Vaulting    // 파쿠르 등 제어가 잠기는 상태
    }
    private PlayerState currentState;


    [Header("핵심 컴포넌트")]
    public Animator animator;           // 애니메이션을 제어할 애니메이터
    public Camera playerCamera;         // 1인칭 시점 카메라
    private CharacterController controller; // 실제 캐릭터를 움직일 컨트롤러


    [Header("기본 움직임 설정")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float lookSpeed = 2.0f;      // 마우스 감도
    public float lookXLimit = 70.0f;    // 상하 시야각 제한


    [Header("점프 및 중력")]
    public float jumpForce = 7f;
    public float gravity = 25.0f;


    [Header("벽 타기 설정")]
    public float climbSpeed = 3f;
    public LayerMask climbableWallLayer; // '벽'으로 인식할 오브젝트의 레이어


    [Header("파쿠르(난간) 설정")]
    public float ledgeDetectionLength = 1.0f;   // 전방 난간을 감지할 레이캐스트 길이
    public float ledgeVaultDuration = 0.8f;   // 난간을 넘는 애니메이션에 걸리는 시간


    // --- 내부 동작 변수 ---
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private bool isRunning = false;
    private bool wasGrounded; // 이전 프레임에 땅에 있었는지 기억

    // --- 초기화 ---
    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentState = PlayerState.Grounded; // 시작은 지상 상태

        // 마우스 커서 잠금 및 숨기기
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        wasGrounded = controller.isGrounded; // 시작 상태 저장
    }

    // --- 메인 루프 ---
    void Update()
    {
        // 1. 현재 물리적 상태를 먼저 파악 (가장 중요)
        bool isGrounded = controller.isGrounded;

        // 2. 상태 전환 로직: 현재 상태와 물리적 상태를 바탕으로 다음 상태를 결정
        //    이 로직은 불안정한 isGrounded 값에 대한 필터 역할을 함
        UpdateCurrentState(isGrounded);

        // 3. 결정된 현재 상태에 따라 적절한 행동 처리
        switch (currentState)
        {
            case PlayerState.Grounded:
                HandleGroundedState();
                break;
            case PlayerState.Aerial:
                HandleAerialState();
                break;
            case PlayerState.Climbing:
                HandleClimbingState();
                break;
            case PlayerState.Vaulting:
                // Vaulting 상태에서는 아무런 입력도 받지 않음
                break;
        }

        // 4. 마우스 시점 조작
        if (currentState != PlayerState.Vaulting)
        {
            HandleMouseLook();
        }

        // 5. 애니메이터 동기화 (결정된 상태와 물리적 상태를 전달)
        UpdateAnimator(isGrounded);
    }

    // --- 새로운 상태 관리 함수 ---
    private void UpdateCurrentState(bool isGrounded)
    {
        // 이 함수가 상태의 '떨림'을 방지하는 핵심입니다.

        if (currentState == PlayerState.Vaulting) return; // 파쿠르 중에는 상태 고정
        if (currentState == PlayerState.Climbing)
        {
            // 벽을 타다가 공중으로 떨어지는 경우
            // (HasReachedTopOfWall, Jump Key 등 다른 곳에서 상태를 바꿈)
            return;
        }

        if (isGrounded)
        {
            // 공중 상태였다가 땅에 닿으면 Grounded 상태로 확실히 전환
            if (currentState == PlayerState.Aerial)
            {
                currentState = PlayerState.Grounded;
            }
        }
        else
        {
            // 땅에 있다가 발이 떨어지면 Aerial 상태로 전환
            // (이 경우는 점프 행동에서 이미 처리되지만, 만약을 위한 안전장치)
            if (currentState == PlayerState.Grounded)
            {
                currentState = PlayerState.Aerial;
            }
        }
    }

    // --- 상태별 처리 함수 ---

    private void HandleGroundedState()
    {
        // ... (입력, 달리기, 이동 방향 계산 코드는 동일) ...
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        isRunning = Input.GetKey(KeyCode.LeftShift);

        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 forwardMove = transform.forward * verticalInput;
        Vector3 rightMove = transform.right * horizontalInput;
        moveDirection = (forwardMove + rightMove).normalized * currentSpeed;

        // 점프 처리
        if (Input.GetButtonDown("Jump"))
        {
            animator.SetTrigger("Jump");
            moveDirection.y = jumpForce;
            controller.Move(moveDirection * Time.deltaTime); // 점프 힘을 즉시 적용
            currentState = PlayerState.Aerial; // 상태를 공중으로 확정
            wasGrounded = false; // 점프하는 순간 땅에서 떨어진 것으로 간주
            return; // 점프했으므로 더 이상 Grounded 로직을 실행하지 않음
        }

        // 땅에 딱 붙어있도록 y축 속도를 음수로 살짝 유지 (떨림 방지)
        moveDirection.y = -gravity * Time.deltaTime;

        // 최종 이동 적용
        controller.Move(moveDirection * Time.deltaTime);

        // 벽 타기 전환 체크
        if (verticalInput > 0 && CanStartClimbing())
        {
            currentState = PlayerState.Climbing;
        }
    }


    // --- 애니메이터 동기화 함수 (수정됨) ---

    private void UpdateAnimator(bool isGrounded)
    {
        // 애니메이터 파라미터 업데이트
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsClimbing", currentState == PlayerState.Climbing);

        // 땅에 있을 때만 속도 전달
        if (currentState == PlayerState.Grounded)
        {
            float horizontalVelocity = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
            animator.SetFloat("Speed", isRunning ? 2.0f : horizontalVelocity); // 달리기는 더 높은 값으로
        }

        // 착지 '순간'을 감지하여 트리거 발동
        if (!wasGrounded && isGrounded)
        {
            animator.SetTrigger("Land");
            Debug.Log("LAND TRIGGER FIRED!"); // 이제 정말 착지 시에만 호출될 것임
        }

        // 다음 프레임을 위해 현재 상태 저장
        wasGrounded = isGrounded;
    }

    private void HandleAerialState()
    {
        // 공중에서 약간의 방향 전환 허용
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector3 airMove = (transform.forward * verticalInput + transform.right * horizontalInput).normalized * (walkSpeed * 0.5f);

        // 중력 적용
        moveDirection.y -= gravity * Time.deltaTime;

        // 최종 이동 벡터 계산 및 적용
        Vector3 finalMove = new Vector3(airMove.x, moveDirection.y, airMove.z);
        controller.Move(finalMove * Time.deltaTime);

        // 추락 중 파쿠르(난간 잡기) 체크
        if (moveDirection.y < 0) // 아래로 떨어질 때만 감지
        {
            CheckForLedge();
        }
    }

    private void HandleClimbingState()
    {
        // 위/아래/좌/우 입력 받기
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");

        // 벽 표면에 맞춰 이동 방향 계산
        Vector3 climbMove = (transform.up * verticalInput) + (transform.right * horizontalInput);
        moveDirection = climbMove.normalized * climbSpeed;

        // 벽에 계속 붙어있도록 미세한 힘 추가
        moveDirection += transform.forward * 0.1f;

        // 캐릭터 이동 (중력 없음)
        controller.Move(moveDirection * Time.deltaTime);

        // 애니메이터에 현재 방향 전달
        animator.SetFloat("Climb_Vertical", verticalInput);
        animator.SetFloat("Climb_Horizontal", horizontalInput);

        // 벽에서 손 놓고 떨어지기
        // (참고: 디자인 결정에 따라 점프 키를 누르면 떨어지도록 했음)
        if (Input.GetButtonDown("Jump"))
        {
            currentState = PlayerState.Aerial;
            animator.SetBool("IsClimbing", false);
        }

        // 벽의 끝에 도달했는지 체크하여 올라가기
        if (verticalInput > 0 && HasReachedTopOfWall())
        {
            StartCoroutine(VaultOverLedge());
        }
    }

    // --- 최종 상태 점검 및 애니메이터 동기화 ---

    private void UpdateStatesAndAnimator()
    {
        // 1. 현재 땅에 있는지 확인
        bool isGrounded = controller.isGrounded;

        // 2. 상태 전환 로직
        if (isGrounded)
        {
            // 공중이나 벽타기 상태였다가 땅에 닿으면 Grounded 상태로 변경
            if (currentState == PlayerState.Aerial || currentState == PlayerState.Climbing)
            {
                currentState = PlayerState.Grounded;
            }
        }
        else if (currentState == PlayerState.Grounded)
        {
            // 땅에 있다가 발이 떨어지면 Aerial 상태로 변경 (점프 외의 추락)
            currentState = PlayerState.Aerial;
        }

        // 3. 애니메이터 파라미터 업데이트
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsClimbing", currentState == PlayerState.Climbing);

        // 땅에 있을 때만 속도 전달
        if (currentState == PlayerState.Grounded)
        {
            float horizontalVelocity = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
            animator.SetFloat("Speed", horizontalVelocity);
        }

        // 착지 '순간'을 감지하여 트리거 발동
        if (!wasGrounded && isGrounded)
        {
            animator.SetTrigger("Land");
        }

        // 다음 프레임을 위해 현재 상태 저장
        wasGrounded = isGrounded;
    }

    // --- 보조 및 감지 함수 ---

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
        // 전방 1미터 내에 등반 가능한 벽이 있는지 확인
        if (Physics.Raycast(transform.position, transform.forward, out hit, 1.0f, climbableWallLayer))
        {
            // 벽의 각도가 너무 가파르지 않은지 확인 (선택 사항, 안정성 추가)
            if (Vector3.Angle(hit.normal, Vector3.up) > 80f)
            {
                return true;
            }
        }
        return false;
    }

    private void CheckForLedge()
    {
        RaycastHit wallHit;
        // 1. 머리 높이에서 전방으로 레이를 쏴서 벽 감지
        if (Physics.Raycast(playerCamera.transform.position, transform.forward, out wallHit, ledgeDetectionLength, climbableWallLayer))
        {
            RaycastHit ledgeHit;
            // 2. 감지된 벽의 살짝 위에서 아래로 레이를 쏴서 빈 공간(난간)이 있는지 확인
            Vector3 ledgeCheckStartPoint = wallHit.point + transform.forward * 0.1f + Vector3.up * 0.5f;
            if (!Physics.Raycast(ledgeCheckStartPoint, Vector3.down, out ledgeHit, 1.0f))
            {
                StartCoroutine(VaultOverLedge());
            }
        }
    }

    private bool HasReachedTopOfWall()
    {
        // 머리 바로 위 전방에 더 이상 벽이 없는지 확인
        return !Physics.Raycast(playerCamera.transform.position + Vector3.up * 0.5f, transform.forward, 1.0f, climbableWallLayer);
    }

    // --- 파쿠르 코루틴 ---

    private IEnumerator VaultOverLedge()
    {
        currentState = PlayerState.Vaulting;
        animator.SetTrigger("DoVault"); // 파쿠르 애니메이션 실행

        // 애니메이션이 자연스럽게 캐릭터 위치를 옮겨주도록 잠시 대기
        // (루트 모션을 사용하지 않는 경우, 아래 코드로 위치를 강제 이동)
        yield return new WaitForSeconds(0.1f); // 애니메이션 시작 대기

        Vector3 startPos = transform.position;
        Vector3 endPos = GetVaultEndPoint();

        float elapsedTime = 0;
        while (elapsedTime < ledgeVaultDuration)
        {
            transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / ledgeVaultDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos; // 정확한 위치 보정
        currentState = PlayerState.Grounded; // 상태를 다시 지상으로
    }

    private Vector3 GetVaultEndPoint()
    {
        RaycastHit hit;
        // 캐릭터의 약간 앞에서 바닥을 향해 레이를 쏴서 안전한 착지 지점을 찾음
        if (Physics.Raycast(transform.position + transform.forward * 1.0f + Vector3.up * 2f, Vector3.down, out hit, 3.0f))
        {
            return hit.point;
        }
        return transform.position + transform.forward; // 만약을 대비한 기본값
    }
}