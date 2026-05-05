namespace Unit.Combat
{
    public interface ICombatSkill
    {
        string SkillId { get; }
        bool TryUseSkill(UnitCombat combat, CombatSkillRequest request);
    }
}
