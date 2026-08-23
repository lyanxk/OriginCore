namespace OriginCore.Input
{
    public struct GameplayInputSuppressionFrame
    {
        private bool _isArmed;
        private int _frame;

        public bool IsArmed => _isArmed;
        public int Frame => _isArmed ? _frame : -1;

        public void Arm(int frame)
        {
            _frame = frame;
            _isArmed = true;
        }

        public bool IsSuppressed(int frame)
        {
            return _isArmed && _frame == frame;
        }

        public void ClearIfExpired(int frame)
        {
            if (_isArmed && frame > _frame)
            {
                Clear();
            }
        }

        public void Clear()
        {
            _isArmed = false;
            _frame = 0;
        }
    }
}
