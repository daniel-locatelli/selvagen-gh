using System.Collections.Generic;
using System.Linq;
using Selvagen.Core.Components;
using Xunit;

namespace Selvagen.Core.Tests.Components
{
    public class CacheDecisionTests
    {
        [Fact]
        public void NeedsFetch_True_When_NoCachedItems()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: false,
                cachedKey: new object[] { "a" },
                currentKey: new object[] { "a" },
                refresh: false,
                refreshWasTrue: false);

            Assert.True(result);
        }

        [Fact]
        public void NeedsFetch_False_When_KeysMatch_AndNoRefreshEdge()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: new object[] { "a", "meshes" },
                currentKey: new object[] { "a", "meshes" },
                refresh: false,
                refreshWasTrue: false);

            Assert.False(result);
        }

        [Fact]
        public void NeedsFetch_True_When_KeysDiffer()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: new object[] { "old-id" },
                currentKey: new object[] { "new-id" },
                refresh: false,
                refreshWasTrue: false);

            Assert.True(result);
        }

        [Fact]
        public void NeedsFetch_True_On_RefreshEdge_FalseToTrue()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: new object[] { "a" },
                currentKey: new object[] { "a" },
                refresh: true,
                refreshWasTrue: false);

            Assert.True(result);
        }

        [Fact]
        public void NeedsFetch_False_When_Refresh_HeldHigh()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: new object[] { "a" },
                currentKey: new object[] { "a" },
                refresh: true,
                refreshWasTrue: true);

            Assert.False(result);
        }

        [Fact]
        public void NeedsFetch_False_When_KeysMatch_AndRefreshDropped()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: new object[] { "a" },
                currentKey: new object[] { "a" },
                refresh: false,
                refreshWasTrue: true);

            Assert.False(result);
        }

        [Fact]
        public void NeedsFetch_TreatsNullCachedKey_AsMismatch()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: null,
                currentKey: new object[] { "a" },
                refresh: false,
                refreshWasTrue: false);

            Assert.True(result);
        }

        [Fact]
        public void NeedsFetch_KeyEquality_Uses_ValueComparison()
        {
            // string interning aside, equal-valued object[] should compare equal
            var k1 = new object[] { "a", 1 };
            var k2 = new object[] { "a", 1 };

            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: k1,
                currentKey: k2,
                refresh: false,
                refreshWasTrue: false);

            Assert.False(result);
        }
    }

    public class ReconcileTests
    {
        private record Item(string Id, string Name);

        [Fact]
        public void SelectId_Returns_PersistedId_When_Present()
        {
            var items = new[] { new Item("a", "Alpha"), new Item("b", "Beta") };

            var result = Reconcile.SelectId(items, "b", x => x.Id);

            Assert.Equal("b", result);
        }

        [Fact]
        public void SelectId_Returns_Null_When_Persisted_Missing()
        {
            var items = new[] { new Item("a", "Alpha"), new Item("b", "Beta") };

            var result = Reconcile.SelectId(items, "ghost", x => x.Id);

            Assert.Null(result);
        }

        [Fact]
        public void SelectId_Returns_Null_When_Persisted_Null()
        {
            var items = new[] { new Item("a", "Alpha") };

            var result = Reconcile.SelectId(items, null, x => x.Id);

            Assert.Null(result);
        }

        [Fact]
        public void SelectId_Returns_Null_When_Items_Empty()
        {
            var items = new Item[0];

            var result = Reconcile.SelectId(items, "a", x => x.Id);

            Assert.Null(result);
        }

        [Fact]
        public void SelectId_Returns_Null_When_Items_Null()
        {
            var result = Reconcile.SelectId<Item>(null, "a", x => x.Id);

            Assert.Null(result);
        }
    }
}
