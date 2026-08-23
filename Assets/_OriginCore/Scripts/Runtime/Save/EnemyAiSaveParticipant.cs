using System;
using OriginCore.AI;
using OriginCore.Economy;

namespace OriginCore.Save
{
    internal sealed class EnemyAiSaveParticipant : ISaveParticipant
    {
        public string ParticipantKey => "enemy-ai";
        public int RestoreOrder => 165;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.enemyAi.Clear();
            EnemyAiDirector[] directors =
                EntitySaveParticipant.CollectSceneComponents<EnemyAiDirector>(
                    context.SceneContext.Scene,
                    true);
            for (int i = 0; i < directors.Length; i++)
            {
                EnemyAiDirector director = directors[i];
                if (director == null || director.Account == null) continue;
                EnemyAiRuntimeSnapshot runtime = director.CaptureSnapshot();
                ResourceSnapshot account = director.Account.Snapshot;
                data.enemyAi.Add(new EnemyAiSaveData
                {
                    directorId = director.DirectorId,
                    phase = (int)runtime.Phase,
                    assaultSerial = runtime.AssaultSerial,
                    thinkRemaining = runtime.ThinkRemaining,
                    regroupRemaining = runtime.RegroupRemaining,
                    productionCursor = runtime.ProductionCursor,
                    strategicTargetRuntimeId = runtime.StrategicTargetRuntimeId,
                    commanderResource = account.CommanderResource,
                    crystal = account.Crystal,
                    influenceUsed = account.InfluenceUsed,
                    influenceCap = account.InfluenceCap
                });
            }
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            EnemyAiDirector[] directors =
                EntitySaveParticipant.CollectSceneComponents<EnemyAiDirector>(
                    context.SceneContext.Scene,
                    true);
            for (int i = 0; i < directors.Length; i++)
            {
                EnemyAiDirector director = directors[i];
                EnemyAiSaveData saved = Find(data, director != null
                    ? director.DirectorId
                    : string.Empty);
                if (director == null || saved == null || director.Account == null)
                    continue;
                director.Account.Restore(new ResourceSnapshot(
                    saved.commanderResource,
                    saved.crystal,
                    saved.influenceUsed,
                    saved.influenceCap));
                EnemyAiPhase phase = Enum.IsDefined(typeof(EnemyAiPhase), saved.phase)
                    ? (EnemyAiPhase)saved.phase
                    : EnemyAiPhase.Dormant;
                director.RestoreSnapshot(new EnemyAiRuntimeSnapshot
                {
                    Phase = phase,
                    AssaultSerial = saved.assaultSerial,
                    ThinkRemaining = saved.thinkRemaining,
                    RegroupRemaining = saved.regroupRemaining,
                    ProductionCursor = saved.productionCursor,
                    StrategicTargetRuntimeId = saved.strategicTargetRuntimeId
                });
            }
        }

        private static EnemyAiSaveData Find(SaveGameData data, string directorId)
        {
            for (int i = 0; i < data.enemyAi.Count; i++)
            {
                EnemyAiSaveData saved = data.enemyAi[i];
                if (saved != null && string.Equals(
                        saved.directorId,
                        directorId,
                        StringComparison.Ordinal)) return saved;
            }
            return null;
        }
    }
}
