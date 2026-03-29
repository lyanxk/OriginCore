using System.Collections.Generic;
using UnityEngine;

public static class RtsOrderDispatcher
{
    public static bool TryIssueMove(
        IReadOnlyList<Selectable> selected,
        Vector3 destination,
        bool append,
        List<CommandExecutor> executorBuffer,
        List<Vector3> destinationBuffer)
    {
        if (selected == null || executorBuffer == null || destinationBuffer == null)
            return false;

        bool issued = false;
        executorBuffer.Clear();
        for (int i = 0; i < selected.Count; i++)
        {
            Selectable selectable = selected[i];
            if (!CanControlSelectable(selectable))
                continue;

            if (selectable.CommandExecutor == null || selectable.Motor == null)
            {
                if (selectable.GroundCommandReceiver != null)
                    issued |= selectable.GroundCommandReceiver.TryIssueGroundCommand(destination, append);

                continue;
            }

            executorBuffer.Add(selectable.CommandExecutor);
        }

        int count = executorBuffer.Count;
        if (count == 0)
            return issued;

        for (int i = 0; i < count; i++)
            executorBuffer[i].Enqueue(new MoveCommand(destination), append);

        return true;
    }

    public static bool TryIssueAttack(IReadOnlyList<Selectable> selected, Vector3 orderPoint, bool append)
    {
        if (selected == null)
            return false;

        bool issued = false;
        for (int i = 0; i < selected.Count; i++)
        {
            Selectable selectable = selected[i];
            if (!CanControlSelectable(selectable))
                continue;

            if (selectable.CommandExecutor == null || selectable.Combat == null)
                continue;

            selectable.CommandExecutor.Enqueue(new AttackCommand(orderPoint), append);
            issued = true;
        }

        return issued;
    }

    public static bool TryIssueStop(IReadOnlyList<Selectable> selected)
    {
        if (selected == null)
            return false;

        bool issued = false;
        for (int i = 0; i < selected.Count; i++)
        {
            Selectable selectable = selected[i];
            if (!CanControlSelectable(selectable))
                continue;

            if (selectable.CommandExecutor == null)
                continue;

            selectable.CommandExecutor.Enqueue(new StopCommand(), append: false);
            issued = true;
        }

        return issued;
    }

    public static bool CanControlSelectable(Selectable selectable)
    {
        if (selectable == null)
            return false;

        TeamAffiliation team = selectable.TeamAffiliation;
        return team == null || team.IsPlayerControllable;
    }
}
