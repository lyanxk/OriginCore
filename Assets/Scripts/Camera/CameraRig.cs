using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    public float smoothTimePosition = 0.18f;
    public float smoothTimeRotation = 0.12f;
    public float smoothTimeFov = 0.15f;

    Vector3 _posVel;
    float _fovVel;

    Camera _cam;

    CameraState _target;
    bool _hasTarget;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _target = new CameraState { Position = transform.position, Rotation = transform.rotation, Fov = _cam.fieldOfView };
        _hasTarget = true;
    }

    public void SetTarget(CameraState target)
    {
        _target = target;
        _hasTarget = true;
    }

    // 切换模式时可以调用：让过渡“更慢/更快”
    public void SetSmooth(float posTime, float rotTime, float fovTime)
    {
        smoothTimePosition = Mathf.Max(0.01f, posTime);
        smoothTimeRotation = Mathf.Max(0.01f, rotTime);
        smoothTimeFov = Mathf.Max(0.01f, fovTime);
    }

    void LateUpdate()
    {
        if (!_hasTarget) return;

        transform.position = Vector3.SmoothDamp(transform.position, _target.Position, ref _posVel, smoothTimePosition);

        // 旋转用指数插值（稳定）
        float t = 1f - Mathf.Exp(-Time.deltaTime / smoothTimeRotation);
        transform.rotation = Quaternion.Slerp(transform.rotation, _target.Rotation, t);

        _cam.fieldOfView = Mathf.SmoothDamp(_cam.fieldOfView, _target.Fov, ref _fovVel, smoothTimeFov);
    }
}