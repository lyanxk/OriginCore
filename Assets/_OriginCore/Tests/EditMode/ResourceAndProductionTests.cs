using NUnit.Framework;
using OriginCore.Buildings;
using OriginCore.Economy;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Tests.EditMode
{
    public sealed class ResourceAndProductionTests
    {
        [Test]
        public void MultiResourceSpendIsAtomicAndPublishesOneChange()
        {
            ResourceWallet wallet = new ResourceWallet(100, 20, 0, 2);
            ResourceSnapshot original = wallet.Snapshot;
            int eventCount = 0;
            wallet.ResourceChanged += change => eventCount++;

            Assert.That(wallet.TrySpend(new ResourceCost(90, 25, 1)), Is.False);
            Assert.That(wallet.Snapshot, Is.EqualTo(original));
            Assert.That(eventCount, Is.Zero);

            Assert.That(wallet.TrySpend(new ResourceCost(50, 10, 2)), Is.True);
            Assert.That(wallet.Snapshot.CommanderResource, Is.EqualTo(50));
            Assert.That(wallet.Snapshot.Crystal, Is.EqualTo(10));
            Assert.That(wallet.Snapshot.InfluenceUsed, Is.EqualTo(2));
            Assert.That(wallet.Snapshot.InfluenceCap, Is.EqualTo(2));
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void InfluenceCapAndReleaseAreExactAndIdempotent()
        {
            ResourceWallet wallet = new ResourceWallet(0, 0, 0, 2);
            Assert.That(wallet.ReserveInfluence(2), Is.True);
            ResourceSnapshot full = wallet.Snapshot;
            Assert.That(full.InfluenceUsed, Is.EqualTo(2));
            Assert.That(full.InfluenceAvailable, Is.Zero);

            Assert.That(wallet.ReserveInfluence(1), Is.False);
            Assert.That(wallet.Snapshot, Is.EqualTo(full));
            Assert.That(wallet.ReleaseInfluence(2), Is.True);
            Assert.That(wallet.Snapshot.InfluenceUsed, Is.Zero);
            Assert.That(wallet.ReleaseInfluence(2), Is.False);
            Assert.That(wallet.Snapshot.InfluenceUsed, Is.Zero);
        }

        [Test]
        public void RejectedProductionDoesNotPartiallyDeductAnyResource()
        {
            GameObject servicesObject = new GameObject("P10 Test ResourceService");
            GameObject producerObject = new GameObject("P10 Test Producer");
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            ProductionRecipe recipe = ScriptableObject.CreateInstance<ProductionRecipe>();
            try
            {
                ResourceService resources = servicesObject.AddComponent<ResourceService>();
                resources.ConfigureInitialValues(10, 5, 0, 1);
                definition.Configure(
                    "unit.test.worker",
                    "Test Worker",
                    UnitRole.Worker,
                    FactionId.Friendly,
                    1f,
                    1f,
                    1f,
                    0.1f,
                    1f,
                    10f,
                    0f,
                    0f,
                    1f,
                    1f,
                    1f);
                recipe.Configure(
                    "production.test.worker",
                    "Test Worker",
                    definition,
                    new ResourceCost(20, 2, 1),
                    1f);

                ProductionQueue queue = producerObject.AddComponent<ProductionQueue>();
                queue.Configure(
                    new[] { recipe },
                    producerObject.GetComponent<OriginCore.Units.UnitSpawner>(),
                    producerObject.GetComponent<RallyPointController>(),
                    null,
                    ProductionQueue.DefaultCapacity,
                    0.5f,
                    resources);
                ResourceSnapshot before = resources.Snapshot;

                Assert.That(queue.TryEnqueue(
                    recipe,
                    out ProductionRejectionReason rejection), Is.False);
                Assert.That(rejection, Is.EqualTo(
                    ProductionRejectionReason.InsufficientResources));
                Assert.That(resources.Snapshot, Is.EqualTo(before));
                Assert.That(queue.TotalCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(producerObject);
                Object.DestroyImmediate(servicesObject);
                Object.DestroyImmediate(recipe);
                Object.DestroyImmediate(definition);
            }
        }
    }
}
