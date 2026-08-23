using System.Collections.Generic;
using OriginCore.Core;
using OriginCore.SceneFlow;

namespace OriginCore.Save
{
    public interface ISaveParticipant
    {
        string ParticipantKey { get; }
        int RestoreOrder { get; }
        void Capture(SaveOperationContext context, SaveGameData data);
        void Restore(SaveOperationContext context, SaveGameData data);
    }

    public interface IVisibilitySaveBridge
    {
        void CaptureExploredState(VisibilitySaveData data);
        void RestoreExploredState(VisibilitySaveData data);
    }

    public sealed class SaveOperationContext
    {
        public SaveOperationContext(
            GameServices services,
            SceneContext sceneContext,
            ICollection<string> warnings)
        {
            Services = services;
            SceneContext = sceneContext;
            Warnings = warnings;
        }

        public GameServices Services { get; }
        public SceneContext SceneContext { get; }
        public ICollection<string> Warnings { get; }

        public void Warn(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Warnings?.Add(message.Trim());
            }
        }
    }
}
