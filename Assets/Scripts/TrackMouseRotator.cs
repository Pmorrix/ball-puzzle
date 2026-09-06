using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TrackMouseRotator : MonoBehaviour
{
    [SerializeField, Min(0f)] private float degreesPerPixel = 0.15f;
    [SerializeField, Range(0f, 89f)] private float maxTilt = 35f;

    private Quaternion startingRotation;
    private float xAngle;
    private float zAngle;

    private void Awake()
    {
        startingRotation = transform.localRotation;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed)
        {
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();
        xAngle = Mathf.Clamp(xAngle - delta.y * degreesPerPixel, -maxTilt, maxTilt);
        zAngle = Mathf.Clamp(zAngle - delta.x * degreesPerPixel, -maxTilt, maxTilt);

        transform.localRotation = startingRotation * Quaternion.Euler(xAngle, 0f, zAngle);
    }
}
