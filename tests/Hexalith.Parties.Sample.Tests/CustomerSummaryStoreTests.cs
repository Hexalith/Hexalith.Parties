using Hexalith.Parties.Sample;

using Shouldly;

namespace Hexalith.Parties.Sample.Tests;

public sealed class CustomerSummaryStoreTests : IDisposable
{
    public CustomerSummaryStoreTests()
    {
        CustomerSummaryStore.Customers.Clear();
    }

    public void Dispose()
    {
        CustomerSummaryStore.Customers.Clear();
    }

    [Fact]
    public void Customers_ShouldBeEmptyByDefault()
    {
        CustomerSummaryStore.Customers.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Customers_ShouldAddAndRetrieveCustomer()
    {
        CustomerSummary customer = new()
        {
            Id = "p-1",
            DisplayName = "Jean Dupont",
        };

        CustomerSummaryStore.Customers["p-1"] = customer;

        CustomerSummaryStore.Customers.ContainsKey("p-1").ShouldBeTrue();
        CustomerSummaryStore.Customers["p-1"].DisplayName.ShouldBe("Jean Dupont");
        CustomerSummaryStore.Customers["p-1"].IsActive.ShouldBeTrue();
        CustomerSummaryStore.Customers["p-1"].Email.ShouldBeNull();
    }

    [Fact]
    public void Customers_ShouldUpdateEmailOnExistingCustomer()
    {
        CustomerSummary customer = new()
        {
            Id = "p-2",
            DisplayName = "Marie Martin",
        };

        CustomerSummaryStore.Customers["p-2"] = customer;
        customer.Email = "marie@example.com";

        CustomerSummaryStore.Customers["p-2"].Email.ShouldBe("marie@example.com");
    }

    [Fact]
    public void Customers_ShouldMarkCustomerInactive()
    {
        CustomerSummary customer = new()
        {
            Id = "p-3",
            DisplayName = "Pierre Durand",
        };

        CustomerSummaryStore.Customers["p-3"] = customer;
        customer.IsActive = false;

        CustomerSummaryStore.Customers["p-3"].IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Customers_ShouldBeConcurrentSafe()
    {
        Parallel.For(0, 100, i =>
        {
            CustomerSummaryStore.Customers[$"p-{i}"] = new CustomerSummary
            {
                Id = $"p-{i}",
                DisplayName = $"Party {i}",
            };
        });

        CustomerSummaryStore.Customers.Count.ShouldBe(100);
    }
}
