using UnityEngine;
using System.Collections;

// �� ��ũ��Ʈ�� �۵��Ϸ��� ���� CharacterController�� �ݵ�� �ʿ���
[RequireComponent(typeof(CharacterController))]
public class AdvancedPlayerController : MonoBehaviour
{
    // --- �÷��̾��� ���� ���¸� ��Ȯ�� �����ϱ� ���� ������ ---
    private enum PlayerState
    {
        Grounded,   // ���� �ִ� ��� ���� (����, �ȱ�, �޸���)
        Aerial,     // ���߿� �ִ� ��� ���� (����, �߶�)
        Climbing,   // ���� �پ��ִ� ����
        Vaulting    // ���� �� ��� ���� ����
    }
    private PlayerState currentState;


    [Header("�ٽ� ������Ʈ")]
    public Animator animator;           // �ִϸ��̼��� ������ �ִϸ�����
    public Camera playerCamera;         // 1��Ī ���� ī�޶�
    private CharacterController controller; // ���� ĳ���͸� ������ ��Ʈ�ѷ�


    [Header("�⺻ ������ ����")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float lookSpeed = 2.0f;      // ���콺 ����
    public float lookXLimit = 70.0f;    // ���� �þ߰� ����


    [Header("���� �� �߷�")]
    public float jumpForce = 7f;
    public float gravity = 25.0f;


    [Header("�� Ÿ�� ����")]
    public float climbSpeed = 3f;
    public LayerMask climbableWallLayer; // '��'���� �ν��� ������Ʈ�� ���̾�


    [Header("����(����) ����")]
    public float ledgeDetectionLength = 1.0f;   // ���� ������ ������ ����ĳ��Ʈ ����
    public float ledgeVaultDuration = 0.8f;   // ������ �Ѵ� �ִϸ��̼ǿ� �ɸ��� �ð�


    // --- ���� ���� ���� ---
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private bool isRunning = false;
    private bool wasGrounded; // ���� �����ӿ� ���� �־����� ���

    // --- �ʱ�ȭ ---
    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentState = PlayerState.Grounded; // ������ ���� ����

        // ���콺 Ŀ�� ��� �� �����
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        wasGrounded = controller.isGrounded; // ���� ���� ����
    }

    // --- ���� ���� ---
    void Update()
    {
        // 1. ���� ������ ���¸� ���� �ľ� (���� �߿�)
        bool isGrounded = controller.isGrounded;

        // 2. ���� ��ȯ ����: ���� ���¿� ������ ���¸� �������� ���� ���¸� ����
        //    �� ������ �Ҿ����� isGrounded ���� ���� ���� ������ ��
        UpdateCurrentState(isGrounded);

        // 3. ������ ���� ���¿� ���� ������ �ൿ ó��
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
                // Vaulting ���¿����� �ƹ��� �Էµ� ���� ����
                break;
        }

        // 4. ���콺 ���� ����
        if (currentState != PlayerState.Vaulting)
        {
            HandleMouseLook();
        }

        // 5. �ִϸ����� ����ȭ (������ ���¿� ������ ���¸� ����)
        UpdateAnimator(isGrounded);
    }

    // --- ���ο� ���� ���� �Լ� ---
    private void UpdateCurrentState(bool isGrounded)
    {
        // �� �Լ��� ������ '����'�� �����ϴ� �ٽ��Դϴ�.

        if (currentState == PlayerState.Vaulting) return; // ���� �߿��� ���� ����
        if (currentState == PlayerState.Climbing)
        {
            // ���� Ÿ�ٰ� �������� �������� ���
            // (HasReachedTopOfWall, Jump Key �� �ٸ� ������ ���¸� �ٲ�)
            return;
        }

        if (isGrounded)
        {
            // ���� ���¿��ٰ� ���� ������ Grounded ���·� Ȯ���� ��ȯ
            if (currentState == PlayerState.Aerial)
            {
                currentState = PlayerState.Grounded;
            }
        }
        else
        {
            // ���� �ִٰ� ���� �������� Aerial ���·� ��ȯ
            // (�� ���� ���� �ൿ���� �̹� ó��������, ������ ���� ������ġ)
            if (currentState == PlayerState.Grounded)
            {
                currentState = PlayerState.Aerial;
            }
        }
    }

    // --- ���º� ó�� �Լ� ---

    private void HandleGroundedState()
    {
        // ... (�Է�, �޸���, �̵� ���� ��� �ڵ�� ����) ...
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        isRunning = Input.GetKey(KeyCode.LeftShift);

        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 forwardMove = transform.forward * verticalInput;
        Vector3 rightMove = transform.right * horizontalInput;
        moveDirection = (forwardMove + rightMove).normalized * currentSpeed;

        // ���� ó��
        if (Input.GetButtonDown("Jump"))
        {
            animator.SetTrigger("Jump");
            moveDirection.y = jumpForce;
            controller.Move(moveDirection * Time.deltaTime); // ���� ���� ��� ����
            currentState = PlayerState.Aerial; // ���¸� �������� Ȯ��
            wasGrounded = false; // �����ϴ� ���� ������ ������ ������ ����
            return; // ���������Ƿ� �� �̻� Grounded ������ �������� ����
        }

        // ���� �� �پ��ֵ��� y�� �ӵ��� ������ ��¦ ���� (���� ����)
        moveDirection.y = -gravity * Time.deltaTime;

        // ���� �̵� ����
        controller.Move(moveDirection * Time.deltaTime);

        // �� Ÿ�� ��ȯ üũ
        if (verticalInput > 0 && CanStartClimbing())
        {
            currentState = PlayerState.Climbing;
        }
    }


    // --- �ִϸ����� ����ȭ �Լ� (������) ---

    private void UpdateAnimator(bool isGrounded)
    {
        // �ִϸ����� �Ķ���� ������Ʈ
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsClimbing", currentState == PlayerState.Climbing);

        // ���� ���� ���� �ӵ� ����
        if (currentState == PlayerState.Grounded)
        {
            float horizontalVelocity = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
            animator.SetFloat("Speed", isRunning ? 2.0f : horizontalVelocity); // �޸���� �� ���� ������
        }

        // ���� '����'�� �����Ͽ� Ʈ���� �ߵ�
        if (!wasGrounded && isGrounded)
        {
            animator.SetTrigger("Land");
            Debug.Log("LAND TRIGGER FIRED!"); // ���� ���� ���� �ÿ��� ȣ��� ����
        }

        // ���� �������� ���� ���� ���� ����
        wasGrounded = isGrounded;
    }

    private void HandleAerialState()
    {
        // ���߿��� �ణ�� ���� ��ȯ ���
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector3 airMove = (transform.forward * verticalInput + transform.right * horizontalInput).normalized * (walkSpeed * 0.5f);

        // �߷� ����
        moveDirection.y -= gravity * Time.deltaTime;

        // ���� �̵� ���� ��� �� ����
        Vector3 finalMove = new Vector3(airMove.x, moveDirection.y, airMove.z);
        controller.Move(finalMove * Time.deltaTime);

        // �߶� �� ����(���� ���) üũ
        if (moveDirection.y < 0) // �Ʒ��� ������ ���� ����
        {
            CheckForLedge();
        }
    }

    private void HandleClimbingState()
    {
        // ��/�Ʒ�/��/�� �Է� �ޱ�
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");

        // �� ǥ�鿡 ���� �̵� ���� ���
        Vector3 climbMove = (transform.up * verticalInput) + (transform.right * horizontalInput);
        moveDirection = climbMove.normalized * climbSpeed;

        // ���� ��� �پ��ֵ��� �̼��� �� �߰�
        moveDirection += transform.forward * 0.1f;

        // ĳ���� �̵� (�߷� ����)
        controller.Move(moveDirection * Time.deltaTime);

        // �ִϸ����Ϳ� ���� ���� ����
        animator.SetFloat("Climb_Vertical", verticalInput);
        animator.SetFloat("Climb_Horizontal", horizontalInput);

        // ������ �� ���� ��������
        // (����: ������ ������ ���� ���� Ű�� ������ ���������� ����)
        if (Input.GetButtonDown("Jump"))
        {
            currentState = PlayerState.Aerial;
            animator.SetBool("IsClimbing", false);
        }

        // ���� ���� �����ߴ��� üũ�Ͽ� �ö󰡱�
        if (verticalInput > 0 && HasReachedTopOfWall())
        {
            StartCoroutine(VaultOverLedge());
        }
    }

    // --- ���� ���� ���� �� �ִϸ����� ����ȭ ---

    private void UpdateStatesAndAnimator()
    {
        // 1. ���� ���� �ִ��� Ȯ��
        bool isGrounded = controller.isGrounded;

        // 2. ���� ��ȯ ����
        if (isGrounded)
        {
            // �����̳� ��Ÿ�� ���¿��ٰ� ���� ������ Grounded ���·� ����
            if (currentState == PlayerState.Aerial || currentState == PlayerState.Climbing)
            {
                currentState = PlayerState.Grounded;
            }
        }
        else if (currentState == PlayerState.Grounded)
        {
            // ���� �ִٰ� ���� �������� Aerial ���·� ���� (���� ���� �߶�)
            currentState = PlayerState.Aerial;
        }

        // 3. �ִϸ����� �Ķ���� ������Ʈ
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsClimbing", currentState == PlayerState.Climbing);

        // ���� ���� ���� �ӵ� ����
        if (currentState == PlayerState.Grounded)
        {
            float horizontalVelocity = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
            animator.SetFloat("Speed", horizontalVelocity);
        }

        // ���� '����'�� �����Ͽ� Ʈ���� �ߵ�
        if (!wasGrounded && isGrounded)
        {
            animator.SetTrigger("Land");
        }

        // ���� �������� ���� ���� ���� ����
        wasGrounded = isGrounded;
    }

    // --- ���� �� ���� �Լ� ---

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
        // ���� 1���� ���� ��� ������ ���� �ִ��� Ȯ��
        if (Physics.Raycast(transform.position, transform.forward, out hit, 1.0f, climbableWallLayer))
        {
            // ���� ������ �ʹ� ���ĸ��� ������ Ȯ�� (���� ����, ������ �߰�)
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
        // 1. �Ӹ� ���̿��� �������� ���̸� ���� �� ����
        if (Physics.Raycast(playerCamera.transform.position, transform.forward, out wallHit, ledgeDetectionLength, climbableWallLayer))
        {
            RaycastHit ledgeHit;
            // 2. ������ ���� ��¦ ������ �Ʒ��� ���̸� ���� �� ����(����)�� �ִ��� Ȯ��
            Vector3 ledgeCheckStartPoint = wallHit.point + transform.forward * 0.1f + Vector3.up * 0.5f;
            if (!Physics.Raycast(ledgeCheckStartPoint, Vector3.down, out ledgeHit, 1.0f))
            {
                StartCoroutine(VaultOverLedge());
            }
        }
    }

    private bool HasReachedTopOfWall()
    {
        // �Ӹ� �ٷ� �� ���濡 �� �̻� ���� ������ Ȯ��
        return !Physics.Raycast(playerCamera.transform.position + Vector3.up * 0.5f, transform.forward, 1.0f, climbableWallLayer);
    }

    // --- ���� �ڷ�ƾ ---

    private IEnumerator VaultOverLedge()
    {
        currentState = PlayerState.Vaulting;
        animator.SetTrigger("DoVault"); // ���� �ִϸ��̼� ����

        // �ִϸ��̼��� �ڿ������� ĳ���� ��ġ�� �Ű��ֵ��� ��� ���
        // (��Ʈ ����� ������� �ʴ� ���, �Ʒ� �ڵ�� ��ġ�� ���� �̵�)
        yield return new WaitForSeconds(0.1f); // �ִϸ��̼� ���� ���

        Vector3 startPos = transform.position;
        Vector3 endPos = GetVaultEndPoint();

        float elapsedTime = 0;
        while (elapsedTime < ledgeVaultDuration)
        {
            transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / ledgeVaultDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos; // ��Ȯ�� ��ġ ����
        currentState = PlayerState.Grounded; // ���¸� �ٽ� ��������
    }

    private Vector3 GetVaultEndPoint()
    {
        RaycastHit hit;
        // ĳ������ �ణ �տ��� �ٴ��� ���� ���̸� ���� ������ ���� ������ ã��
        if (Physics.Raycast(transform.position + transform.forward * 1.0f + Vector3.up * 2f, Vector3.down, out hit, 3.0f))
        {
            return hit.point;
        }
        return transform.position + transform.forward; // ������ ����� �⺻��
    }
}