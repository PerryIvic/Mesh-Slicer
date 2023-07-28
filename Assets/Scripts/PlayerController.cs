using System.Collections;
using System.Collections.Generic;
using UnityEditor.UI;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    GameObject playerObject;

    [SerializeField]
    float mouseSensitivity = 2;

    Slicer slicer;

    // Camera Variables
    Vector3 cameraRotation;
    float playerDistance;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        slicer = GetComponentInChildren<Slicer>();
        slicer.gameObject.SetActive(false);

        playerDistance = (transform.position - playerObject.transform.position).magnitude;
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
            UpdateCamera();
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

        transform.position = playerObject.transform.position - (transform.forward * playerDistance);
    }
}
