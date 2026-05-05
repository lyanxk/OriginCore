using UnityEngine;

namespace Camera
{
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
                Fov = cam.GetComponent<UnityEngine.Camera>() ? cam.GetComponent<UnityEngine.Camera>().fieldOfView : 60f
            };
        }
    }
}