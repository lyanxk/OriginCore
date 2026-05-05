using UnityEngine;

namespace Unit.Selection
{
    public interface IGroundCommandReceiver
    {
        bool TryIssueGroundCommand(Vector3 worldPoint, bool append);
    }
}
