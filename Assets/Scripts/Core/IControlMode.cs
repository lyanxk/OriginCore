using Camera;
using Input;

namespace Core
{
    public interface IControlMode
    {
        string Name { get; }
        void Enter();
        void Exit();
        void Tick(float dt, InputIntent intent);
        CameraState GetCameraTarget();
    }
}