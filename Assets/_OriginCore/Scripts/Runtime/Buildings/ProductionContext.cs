using System.Collections.Generic;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;

namespace OriginCore.Buildings
{
    public interface IProductionResourceAccount
    {
        ResourceSnapshot Snapshot { get; }
        bool TrySpend(ResourceCost cost);
        bool Refund(ResourceCost cost);
        bool ReserveInfluence(int amount);
        bool ReleaseInfluence(int amount);
    }

    public interface IProductionUnlockPolicy
    {
        bool CanProduce(ProductionRecipe recipe);
    }

    public sealed class ProductionContext
    {
        public ProductionContext(
            FactionId faction,
            IProductionResourceAccount account,
            IProductionUnlockPolicy unlockPolicy)
        {
            Faction = faction;
            Account = account;
            UnlockPolicy = unlockPolicy;
        }

        public FactionId Faction { get; }
        public IProductionResourceAccount Account { get; }
        public IProductionUnlockPolicy UnlockPolicy { get; }
        public bool IsValid => Account != null && UnlockPolicy != null;

        public static bool TryCreatePlayer(out ProductionContext context)
        {
            context = null;
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.ResourceService == null)
            {
                return false;
            }
            context = new ProductionContext(
                FactionId.Friendly,
                root.Services.ResourceService,
                new PlayerProductionUnlockPolicy());
            return true;
        }
    }

    public sealed class PlayerProductionUnlockPolicy : IProductionUnlockPolicy
    {
        public bool CanProduce(ProductionRecipe recipe)
        {
            return recipe != null &&
                   (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                    root.Services.TechTree == null || root.Services.TechTree.CanProduce(recipe));
        }
    }

    public sealed class RecipeAllowListPolicy : IProductionUnlockPolicy
    {
        private readonly HashSet<ProductionRecipe> _recipes =
            new HashSet<ProductionRecipe>();

        public RecipeAllowListPolicy(IEnumerable<ProductionRecipe> recipes)
        {
            if (recipes == null) return;
            foreach (ProductionRecipe recipe in recipes)
            {
                if (recipe != null) _recipes.Add(recipe);
            }
        }

        public bool CanProduce(ProductionRecipe recipe)
        {
            return recipe != null && _recipes.Contains(recipe);
        }
    }
}
