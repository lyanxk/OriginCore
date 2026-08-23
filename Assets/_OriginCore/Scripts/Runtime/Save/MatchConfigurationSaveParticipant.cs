using System;
using OriginCore.Matches;

namespace OriginCore.Save
{
    public sealed class MatchConfigurationSaveParticipant : ISaveParticipant
    {
        public string ParticipantKey => "match-config";
        public int RestoreOrder => 50;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            MatchConfiguration configuration = context.Services.MatchSession.CurrentConfiguration;
            if (configuration == null)
            {
                configuration = MatchConfiguration.CreateSystemTestFallback();
                configuration.sceneKey = data.sceneKey;
            }

            data.matchConfiguration.CopyFrom(configuration);
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            MatchConfiguration configuration = data.matchConfiguration.ToConfiguration();
            if (!context.Services.MatchSession.CommitLoadedMatch(configuration, out string error))
            {
                throw new InvalidOperationException("Match configuration restore failed: " + error);
            }
        }
    }
}
