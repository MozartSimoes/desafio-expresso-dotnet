using Expresso.Api.Domain;

namespace Expresso.Tests.Domain;

public class ProductTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Price_must_be_greater_than_zero(decimal price)
    {
        Assert.Throws<BusinessRuleException>(() =>
            new Product("Latte", ProductCategory.Coffee, ProductSize.M, price));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_is_required(string name)
    {
        Assert.Throws<BusinessRuleException>(() =>
            new Product(name, ProductCategory.Coffee, ProductSize.M, 10m));
    }

    [Fact]
    public void Stock_cannot_be_negative()
    {
        Assert.Throws<BusinessRuleException>(() =>
            new Product("Coxinha", ProductCategory.Snack, null, 7.5m, stock: -1));
    }

    [Fact]
    public void Name_is_trimmed()
    {
        var product = new Product("  Latte  ", ProductCategory.Coffee, ProductSize.M, 10m);

        Assert.Equal("Latte", product.Name);
    }
}
