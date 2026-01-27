using UnityEngine;

public struct CameraState
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Fov;

    public static CameraState From(Transform cam)
    {
        return new CameraState
        {
            Position = cam.position,
            Rotation = cam.rotation,
            Fov = cam.GetComponent<Camera>() ? cam.GetComponent<Camera>().fieldOfView : 60f
        };
    }
}