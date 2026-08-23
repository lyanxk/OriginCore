using System;
using OriginCore.Input;

namespace OriginCore.Core
{
    public enum GameMode
    {
        RTS = 0,
        ACT = 1,
        FPS = 2
    }

    public readonly struct GameModeChange
    {
        public GameModeChange(GameMode previous, GameMode current, int frame)
        {
            Previous = previous;
            Current = current;
            Frame = frame;
        }

        public GameMode Previous { get; }
        public GameMode Current { get; }
        public int Frame { get; }
    }

    public static class GameModeExtensions
    {
        public static bool IsDirectControl(this GameMode mode)
        {
            return mode == GameMode.ACT || mode == GameMode.FPS;
        }

        public static GameplayInputMap ToGameplayInputMap(this GameMode mode)
        {
            switch (mode)
            {
                case GameMode.RTS:
                    return GameplayInputMap.RTS;
                case GameMode.ACT:
                    return GameplayInputMap.ACT;
                case GameMode.FPS:
                    return GameplayInputMap.FPS;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public static GameMode ToGameMode(this GameplayInputMap inputMap)
        {
            switch (inputMap)
            {
                case GameplayInputMap.RTS:
                    return GameMode.RTS;
                case GameplayInputMap.ACT:
                    return GameMode.ACT;
                case GameplayInputMap.FPS:
                    return GameMode.FPS;
                default:
                    throw new ArgumentOutOfRangeException(nameof(inputMap), inputMap, null);
            }
        }
    }
}
