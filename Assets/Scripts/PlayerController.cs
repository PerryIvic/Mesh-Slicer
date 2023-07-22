using System.Collections;
using System.Collections.Generic;
using UnityEditor.UI;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    float mouseSensitivity = 2;

    Vector3 cameraRotation;

    Slicer slicer;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        slicer = GetComponentInChildren<Slicer>();
        slicer.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButtonDown(1))
        {
            slicer.gameObject.SetActive(true);
        }
        if(Input.GetMouseButtonUp(1))
        {
            slicer.gameObject.SetActive(false);
        }

        if(!Input.GetMouseButton(1))
        {
            RotateCamera();
        }
    }

    void RotateCamera()
    {
        Vector3 delta = Vector3.zero;
        delta.x = Input.GetAxis("Mouse X");
        delta.y = Input.GetAxis("Mouse Y");
        delta *= mouseSensitivity;

        cameraRotation.x += delta.x;
        cameraRotation.y += delta.y;

        cameraRotation.y = Mathf.Clamp(cameraRotation.y, -90f, 90);

        Quaternion quatX = Quaternion.AngleAxis(cameraRotation.x, Vector3.up);
        Quaternion quatY = Quaternion.AngleAxis(cameraRotation.y, Vector3.left);

        transform.localRotation = quatX * quatY;
    }
}
