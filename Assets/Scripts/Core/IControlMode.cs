public interface IControlMode
{
    string Name { get; }
    void Enter();
    void Exit();
    void Tick(float dt, InputIntent intent);
    CameraState GetCameraTarget();
}