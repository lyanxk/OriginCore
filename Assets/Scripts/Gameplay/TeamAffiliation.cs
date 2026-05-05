using UnityEngine;

namespace Gameplay
{
    public enum TeamType
    {
        Friendly = 0,
        Neutral = 1,
        Enemy = 2
    }

    [DisallowMultipleComponent]
    public class TeamAffiliation : MonoBehaviour
    {
        //团队设置
        [SerializeField] TeamType team = TeamType.Friendly;

        public TeamType Team => team;
        public bool IsPlayerControllable => team == TeamType.Friendly;

        public bool IsHostileTo(TeamAffiliation other)
        {
            if (other == null)
                return false;

            return IsHostile(team, other.team);
        }

        public void SetTeam(TeamType teamType)
        {
            team = teamType;
        }

        public static bool IsHostile(TeamType a, TeamType b)
        {
            if (a == b)
                return false;

            if (a == TeamType.Neutral || b == TeamType.Neutral)
                return false;

            return (a == TeamType.Friendly && b == TeamType.Enemy)
                   || (a == TeamType.Enemy && b == TeamType.Friendly);
        }
    }
}
