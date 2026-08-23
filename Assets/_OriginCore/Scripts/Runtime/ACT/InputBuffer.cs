using UnityEngine;

namespace OriginCore.ACT
{
    public sealed class InputBuffer
    {
        private bool _jumpBuffered;
        private float _jumpRemaining;

        public bool HasBufferedJump => _jumpBuffered;
        public float JumpRemaining => _jumpRemaining;

        public void Tick(float deltaTime)
        {
            if (!_jumpBuffered || deltaTime <= 0f)
            {
                return;
            }

            _jumpRemaining = Mathf.Max(0f, _jumpRemaining - deltaTime);
            if (_jumpRemaining <= 0f)
            {
                ClearJump();
            }
        }

        public void BufferJump(float duration)
        {
            _jumpBuffered = true;
            _jumpRemaining = Mathf.Max(0f, duration);
        }

        public bool TryConsumeJump()
        {
            if (!_jumpBuffered)
            {
                return false;
            }

            ClearJump();
            return true;
        }

        public void Clear()
        {
            ClearJump();
        }

        private void ClearJump()
        {
            _jumpBuffered = false;
            _jumpRemaining = 0f;
        }
    }
}
