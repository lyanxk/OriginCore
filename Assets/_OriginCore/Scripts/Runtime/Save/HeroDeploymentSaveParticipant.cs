using System;
using OriginCore.Gameplay;
using OriginCore.Matches;

namespace OriginCore.Save
{
    internal sealed class HeroDeploymentSaveParticipant : ISaveParticipant
    {
        public string ParticipantKey => "hero-deployment";
        public int RestoreOrder => 155;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.heroDeployment = new HeroDeploymentSaveData();
            HeroDeploymentController deployment =
                context.Services.MatchSession.InitialHeroDeployment;
            if (deployment == null || !deployment.IsDeploying)
            {
                return;
            }

            EntityIdentity host = deployment.GetComponent<EntityIdentity>();
            data.heroDeployment.pending = true;
            data.heroDeployment.remainingSeconds = deployment.RemainingSeconds;
            data.heroDeployment.hostRuntimeId = host != null
                ? host.EnsureRuntimeId()
                : string.Empty;
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            HeroDeploymentSaveData saved = data.heroDeployment;
            if (saved == null || !saved.pending)
            {
                return;
            }

            if (!context.Services.MatchSession.RestoreInitialHeroDeployment(
                    saved.remainingSeconds,
                    saved.hostRuntimeId,
                    out string error))
            {
                throw new InvalidOperationException(
                    "Hero deployment could not be restored: " + error);
            }
        }
    }
}
