using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.Experimental.AI;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    GameObject playerObject;

    CharacterController playerController;

    Transform cameraTransform;

    [SerializeField]
    Transform bladeTargetTransform;

    Vector3 defaultBladeTargetPosition;

    [SerializeField]
    GameObject katanaObject;

    [SerializeField]
    Transform defaultCameraTarget;

    [SerializeField]
    Transform defaultCameraTransform;

    [SerializeField]
    Transform sliceModeCameraTransform;

    [SerializeField]
    float mouseSensitivity = 2;

    Slicer slicer;
    Vector3 defaultLocalSliceRotation;

    // Camera Variables
    Vector3 cameraRotation;
    float playerDistance;

    float defaultFov;

    [SerializeField]
    float sliceModeFov = 40;

    Vector3 lerpStartPosition;
    Vector3 lerpEndPosition;

    // Character Movement Variables
    float turnSmoothTime = 0.1f;
    float turnSmoothVelocity;

    [SerializeField]
    float movementSpeed = 50;

    [SerializeField]
    float sliceModeTransitionDuration = 1f;

    float sliceModeTransitionTimer;

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
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        slicer = GetComponentInChildren<Slicer>();
        slicer.onSlice += FlipBladeTargetPosition;
        slicer.SetVisibility(false);

        defaultLocalSliceRotation = new Vector3(0, 0, 90);

        playerDistance = (transform.position - defaultCameraTarget.transform.position).magnitude;

        playerController = playerObject.GetComponent<CharacterController>();
        playerAnimator = playerObject.GetComponent<Animator>();

        katanaObject.SetActive(false);

        cameraTransform = Camera.main.transform;

        lerpStartPosition = defaultCameraTransform.position;
        lerpEndPosition = defaultCameraTransform.position;

        defaultBladeTargetPosition = bladeTargetTransform.localPosition;

        defaultTimeScale = Time.timeScale;
        defaultFixedDeltaTime = Time.fixedDeltaTime;

        defaultFov = Camera.main.fieldOfView;
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
            playerAnimator.SetFloat(animPosXTrigger, 0);
            playerAnimator.SetFloat(animPosYTrigger, 1);

            SetAnimationOnce(animSliceModeTrigger);

            katanaObject.SetActive(true);

            slicer.transform.localEulerAngles = defaultLocalSliceRotation;
            slicer.SetVisibility(true);

            sliceModeTransitionTimer = 0;

            lerpStartPosition = defaultCameraTransform.position;
            lerpEndPosition = sliceModeCameraTransform.position;

            bladeTargetTransform.localPosition = defaultBladeTargetPosition;

            Time.timeScale = slowTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * slowTimeScale;
        }

        if(Input.GetMouseButtonUp(1))
        {
            SetAnimationOnce(animIdleTrigger);

            katanaObject.SetActive(false);
            slicer.SetVisibility(false);

            sliceModeTransitionTimer = 0;

            lerpStartPosition = sliceModeCameraTransform.position;
            lerpEndPosition = defaultCameraTransform.position;

            Time.timeScale = defaultTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
        }

        if (!Input.GetMouseButton(1))
        {
            sliceModeTransitionTimer += Time.deltaTime;
            if (sliceModeTransitionTimer < sliceModeTransitionDuration)
            {
                float lerpPercentage = Mathf.Min(sliceModeTransitionTimer / sliceModeTransitionDuration, 1);

                Camera.main.fieldOfView = Mathf.Lerp(sliceModeFov, defaultFov, lerpPercentage);
                cameraTransform.position = Vector3.Lerp(lerpStartPosition, lerpEndPosition, lerpPercentage);
            }
            else
            {
                UpdateCamera();
                UpdateCharacterMovement();
            }
        }
        else
        {
            // Match characters rotation with camera
            float angle = Mathf.SmoothDampAngle(playerObject.transform.eulerAngles.y, transform.eulerAngles.y, ref turnSmoothVelocity, turnSmoothTime);
            playerObject.transform.rotation = Quaternion.Euler(0, angle, 0);

            sliceModeTransitionTimer += Time.deltaTime;
            if (sliceModeTransitionTimer < sliceModeTransitionDuration)
            {
                float lerpPercentage = Mathf.Min(sliceModeTransitionTimer / sliceModeTransitionDuration, 1);

                Camera.main.fieldOfView = Mathf.Lerp(defaultFov, sliceModeFov, lerpPercentage);
                cameraTransform.position = Vector3.Lerp(lerpStartPosition, lerpEndPosition, lerpPercentage);
            }

            UpdateSliceModePosition();
        }
    }

    void UpdateCamera()
    {
        Vector3 delta = Vector3.zero;
        delta.x = Input.GetAxis("Mouse X");
        delta.y = Input.GetAxis("Mouse Y");
        delta *= mouseSensitivity;

        cameraRotation.x += delta.x; 
        cameraRotation.y += delta.y;
        cameraRotation.y = Mathf.Clamp(cameraRotation.y, -90, 90);

        transform.eulerAngles = new Vector3(-cameraRotation.y, transform.eulerAngles.y + delta.x, 0);

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
}
