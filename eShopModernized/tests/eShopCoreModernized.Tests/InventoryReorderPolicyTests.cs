using eShopCoreModernized.Domain;
using eShopCoreModernized.Models;
using Xunit;

namespace eShopCoreModernized.Tests
{
    public class InventoryReorderPolicyTests
    {
        private readonly InventoryReorderPolicy _policy = new();

        private static CatalogItem Item(int stock, int restock, bool onReorder = false) =>
            new() { AvailableStock = stock, RestockThreshold = restock, OnReorder = onReorder };

        [Fact]
        public void RequiresReorder_WhenStockBelowThreshold_ReturnsTrue()
        {
            Assert.True(_policy.RequiresReorder(Item(stock: 8, restock: 15)));
        }

        [Fact]
        public void RequiresReorder_WhenStockEqualsThreshold_ReturnsTrue()
        {
            Assert.True(_policy.RequiresReorder(Item(stock: 15, restock: 15)));
        }

        [Fact]
        public void RequiresReorder_WhenStockAboveThreshold_ReturnsFalse()
        {
            Assert.False(_policy.RequiresReorder(Item(stock: 100, restock: 20)));
        }

        [Fact]
        public void RequiresReorder_WhenAlreadyOnReorder_ReturnsFalse()
        {
            Assert.False(_policy.RequiresReorder(Item(stock: 3, restock: 10, onReorder: true)));
        }

        [Fact]
        public void RequiresReorder_WhenStockZero_ReturnsTrue()
        {
            Assert.True(_policy.RequiresReorder(Item(stock: 0, restock: 10)));
        }

        [Fact]
        public void RequiresReorder_NullItem_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _policy.RequiresReorder(null!));
        }
    }
}
