using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [SerializeField]
    float duration;

    [SerializeField]
    float intensity;

    [SerializeField]
    Slicer slicer;

    // Start is called before the first frame update
    void Start()
    {
        slicer.onSlice += ShakeCamera;
    }

    private void OnDestroy()
    {
        slicer.onSlice -= ShakeCamera;
    }

    void ShakeCamera()
    {
        StartCoroutine(IStartCameraShake());
    }

    IEnumerator IStartCameraShake()
    {
        Vector3 originalPosition = transform.localPosition;

        float elapsedTime = 0;
        while (elapsedTime < duration)
        {
            float x = originalPosition.x + Random.Range(-1, 1) * intensity;
            float y = originalPosition.y + Random.Range(-1, 1) * intensity;

            transform.localPosition = new Vector3(x, y, originalPosition.z);

            elapsedTime += Time.deltaTime;

            yield return null;
        }

        transform.localPosition = originalPosition;
    }
}
