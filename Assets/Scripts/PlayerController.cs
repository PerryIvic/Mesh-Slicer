using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.AI;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour
{
    [Header("Player")]

    [SerializeField]
    GameObject playerObject;

    [SerializeField]
    GameObject katanaObject;

    [SerializeField]
    Transform bladeTargetTransform;

    CharacterController playerController;
    Slicer slicer;

    Vector3 defaultLocalSliceRotation;
    Vector3 defaultBladeTargetPosition;

    [Header("Camera Positions")]

    [SerializeField]
    Transform defaultCameraTarget;

    [SerializeField]
    Transform defaultCameraTransform;

    [SerializeField]
    Transform sliceModeCameraTransform;

    Transform cameraTransform;
    Vector3 cameraRotation;
    float playerDistance;

    [Header("Movement Settings")]

    [SerializeField]
    float mouseSensitivity = 2;

    [SerializeField]
    float movementSpeed = 50;

    float turnSmoothTime = 0.1f;
    float turnSmoothVelocity;

    [Header("Slice Mode")]

    [SerializeField]
    float sliceModeFov = 40;

    float defaultFov;

    [SerializeField]
    float sliceModeTransitionDuration = 1f;
    float sliceModeTransitionTimer;

    Vector3 lerpStartPosition;
    Vector3 lerpEndPosition;

    float lerpStartFov;
    float lerpEndFov;

    // Player Animator
    Animator playerAnimator;

    int previousAnimTrigger;

    readonly int animIdleTrigger = Animator.StringToHash("Idle");
    readonly int animRunTrigger = Animator.StringToHash("Run");
    readonly int animSliceModeTrigger = Animator.StringToHash("SliceMode");
    readonly int animPosXTrigger = Animator.StringToHash("Pos X");
    readonly int animPosYTrigger = Animator.StringToHash("Pos Y");

    // Slow Motion
    float defaultTimeScale;
    float defaultFixedDeltaTime;
    float slowTimeScale = 0.4f;

    // Start is called before the first frame update
    void Start()
    {
        playerController = playerObject.GetComponent<CharacterController>();
        playerAnimator = playerObject.GetComponent<Animator>();

        cameraTransform = Camera.main.transform;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        slicer = GetComponentInChildren<Slicer>();
        slicer.onSlice += FlipBladeTargetPosition;
        slicer.SetVisibility(false);
        katanaObject.SetActive(false);

        playerDistance = (transform.position - defaultCameraTarget.transform.position).magnitude;

        defaultLocalSliceRotation = new Vector3(0, 0, 90);
        defaultBladeTargetPosition = bladeTargetTransform.localPosition;
        defaultTimeScale = Time.timeScale;
        defaultFixedDeltaTime = Time.fixedDeltaTime;
        defaultFov = Camera.main.fieldOfView;

        lerpStartPosition = defaultCameraTransform.position;
        lerpEndPosition = defaultCameraTransform.position;
        lerpStartFov = defaultFov;
        lerpEndFov = defaultFov;
    }

    private void OnDestroy()
    {
        slicer.onSlice -= FlipBladeTargetPosition;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButtonDown(1))
        {
            SetSliceModeState(true);
        }

        if(Input.GetMouseButtonUp(1))
        {
            SetSliceModeState(false);
        }

        if (!Input.GetMouseButton(1))
        {
            if (IsTransitioningToSliceMode())
            {
                UpdateSliceModeTransition();
            }
            else
            {
                UpdateCamera();
                UpdateCharacterMovement();
            }
        }
        else
        {
            // Match character rotation with camera rotation
            float angle = Mathf.SmoothDampAngle(playerObject.transform.eulerAngles.y, transform.eulerAngles.y, ref turnSmoothVelocity, turnSmoothTime);
            playerObject.transform.rotation = Quaternion.Euler(0, angle, 0);

            if (IsTransitioningToSliceMode())
            {
                UpdateSliceModeTransition();
            }

            UpdateSliceModePosition();
        }
    }

    void UpdateCamera()
    {
        Vector3 mouseDelta = Vector3.zero;
        mouseDelta.x = Input.GetAxis("Mouse X");
        mouseDelta.y = Input.GetAxis("Mouse Y");
        mouseDelta *= mouseSensitivity;

        cameraRotation.x += mouseDelta.x; 
        cameraRotation.y += mouseDelta.y;
        cameraRotation.y = Mathf.Clamp(cameraRotation.y, -90, 90);

        transform.eulerAngles = new Vector3(-cameraRotation.y, transform.eulerAngles.y + mouseDelta.x, 0);

        transform.position = defaultCameraTarget.transform.position - (transform.forward * playerDistance);
    }

    void UpdateCharacterMovement()
    {
        Vector2 inputAxis = new Vector2();
        inputAxis.x = Input.GetAxis("Horizontal");
        inputAxis.y = Input.GetAxis("Vertical");

        Vector3 direction = new Vector3(inputAxis.x, 0, inputAxis.y).normalized;
        if(direction.magnitude > 0.1f)
        {
            SetAnimationOnce(animRunTrigger);

            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + transform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(playerObject.transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);

            playerObject.transform.rotation = Quaternion.Euler(0, angle, 0);

            Vector3 moveDir = Quaternion.Euler(0, targetAngle, 0) * Vector3.forward;
            moveDir.Normalize();

            playerController.Move(moveDir * movementSpeed * Time.deltaTime);
        }
        else
        {
            SetAnimationOnce(animIdleTrigger);
        }
    }

    void UpdateSliceModePosition()
    {
        // Gives us the localPosition of bladeTargetTransform in the slicer planes parent space. Which tells us the localPosition of the bladeTargertTransform when the slicer plane changes rotation.
        Vector3 pos = transform.InverseTransformPoint(bladeTargetTransform.position);

        playerAnimator.SetFloat(animPosXTrigger, pos.x);
        playerAnimator.SetFloat(animPosYTrigger, pos.y);
    }

    void SetAnimationOnce(int triggerID)
    {
        if(previousAnimTrigger != triggerID)
        {
            playerAnimator.SetTrigger(triggerID);
            previousAnimTrigger = triggerID;
        }
    }

    void FlipBladeTargetPosition()
    {
        Vector3 oldPos = bladeTargetTransform.localPosition;
        bladeTargetTransform.localPosition = new Vector3(oldPos.x * -1, -oldPos.y * -1, oldPos.z);
    }

    void SetSliceModeState(bool shouldBeActive)
    {
        if(shouldBeActive)
        {
            SetAnimationOnce(animSliceModeTrigger);

            playerAnimator.SetFloat(animPosXTrigger, 0);
            playerAnimator.SetFloat(animPosYTrigger, 1);

            katanaObject.SetActive(true);
            slicer.SetVisibility(true);

            slicer.transform.localEulerAngles = defaultLocalSliceRotation;
            sliceModeTransitionTimer = 0;

            lerpStartPosition = defaultCameraTransform.position;
            lerpEndPosition = sliceModeCameraTransform.position;

            lerpStartFov = defaultFov;
            lerpEndFov = sliceModeFov;

            bladeTargetTransform.localPosition = defaultBladeTargetPosition;

            Time.timeScale = slowTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * slowTimeScale;
        }
        else
        {
            SetAnimationOnce(animIdleTrigger);

            katanaObject.SetActive(false);
            slicer.SetVisibility(false);

            sliceModeTransitionTimer = 0;

            lerpStartPosition = sliceModeCameraTransform.position;
            lerpEndPosition = defaultCameraTransform.position;

            lerpStartFov = sliceModeFov;
            lerpEndFov = defaultFov;

            Time.timeScale = defaultTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
        }
    }

    bool IsTransitioningToSliceMode()
    {
        return (sliceModeTransitionTimer < sliceModeTransitionDuration);
    }

    void UpdateSliceModeTransition()
    {
        sliceModeTransitionTimer += Time.deltaTime;

        float lerpPercentage = Mathf.Min(sliceModeTransitionTimer / sliceModeTransitionDuration, 1);

        Camera.main.fieldOfView = Mathf.Lerp(lerpStartFov, lerpEndFov, lerpPercentage);
        cameraTransform.position = Vector3.Lerp(lerpStartPosition, lerpEndPosition, lerpPercentage);
    }
}
