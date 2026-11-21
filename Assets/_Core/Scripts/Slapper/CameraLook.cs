using UnityEngine;

public class CameraLook : MonoBehaviour
{
    [SerializeField] private SlapperInput input;
    
    [Header("Settings")]
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;
    
    private float _pitch = 0f;

    private void Awake()
    {
        if (input == null) input = FindFirstObjectByType<SlapperInput>();
    }

    private void Update()
    {
        if (input == null) return;

        // Read look input from SlapperInput
        Vector2 lookDelta = input.LookInput * sensitivity;

        // Horizontal (Yaw)
        transform.Rotate(Vector3.up, lookDelta.x, Space.World);

        // Vertical (Pitch)
        _pitch -= lookDelta.y;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        
        transform.rotation = Quaternion.Euler(_pitch, transform.eulerAngles.y, 0f);
    }
}