namespace Unit.Command
{
    public interface IUnitCommand
    {
        void Begin(UnitContext ctx);
        void Tick(UnitContext ctx, float dt);
        bool IsDone(UnitContext ctx);
        void End(UnitContext ctx);
    }

}