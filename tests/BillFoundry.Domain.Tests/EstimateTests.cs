using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Tests;

public sealed class MoneyRoundingTests
{
    [Theory]
    [InlineData(0.004, 0.00)]
    [InlineData(0.005, 0.01)]
    [InlineData(1.125, 1.13)]
    [InlineData(1.135, 1.14)]
    [InlineData(-0.005, -0.01)]
    public void Amount_rounds_midpoints_away_from_zero(decimal value, decimal expected)
    {
        Assert.Equal(expected, MoneyRounding.Amount(value));
    }

    [Fact]
    public void Scale_helpers_reject_extra_fractional_digits()
    {
        Assert.True(MoneyRounding.HasAmountScale(1.25m));
        Assert.False(MoneyRounding.HasAmountScale(1.251m));
        Assert.True(MoneyRounding.HasQuantityScale(1.1234m));
        Assert.False(MoneyRounding.HasQuantityScale(1.12345m));
        Assert.True(MoneyRounding.HasRateScale(8.2500m));
        Assert.False(MoneyRounding.HasRateScale(8.25001m));
    }
}

public sealed class EstimateCalculatorTests
{
    [Fact]
    public void Line_amount_rounds_after_multiplication()
    {
        Assert.Equal(1.51m, EstimateCalculator.LineAmount(1.5m, 1.005m));
        Assert.Equal(1.00m, EstimateCalculator.LineAmount(3m, 0.3333m));
        Assert.Equal(0.00m, EstimateCalculator.LineAmount(0.0001m, 0.0001m));
    }

    [Fact]
    public void Discount_is_allocated_proportionally_to_taxable_lines()
    {
        EstimateTotals totals = EstimateCalculator.Calculate(
            [
                new EstimateLineAmount(1m, 10.00m, IsTaxable: true),
                new EstimateLineAmount(1m, 10.00m, IsTaxable: false)
            ],
            discount: 5.00m,
            taxRatePercent: 10m);

        Assert.Equal(20.00m, totals.Subtotal);
        Assert.Equal(5.00m, totals.Discount);
        Assert.Equal(7.50m, totals.TaxableSubtotal);
        Assert.Equal(0.75m, totals.Tax);
        Assert.Equal(15.75m, totals.Total);
    }

    [Fact]
    public void Full_discount_clears_tax_and_total()
    {
        EstimateTotals totals = EstimateCalculator.Calculate(
            [new EstimateLineAmount(1m, 40.00m, true)],
            discount: 40.00m,
            taxRatePercent: 8.25m);

        Assert.Equal(0.00m, totals.TaxableSubtotal);
        Assert.Equal(0.00m, totals.Tax);
        Assert.Equal(0.00m, totals.Total);
    }

    [Fact]
    public void Non_taxable_lines_do_not_generate_tax()
    {
        EstimateTotals totals = EstimateCalculator.Calculate(
            [new EstimateLineAmount(2m, 50.00m, false)],
            discount: 10.00m,
            taxRatePercent: 10m);

        Assert.Equal(0.00m, totals.TaxableSubtotal);
        Assert.Equal(0.00m, totals.Tax);
        Assert.Equal(90.00m, totals.Total);
    }

    [Fact]
    public void Tax_midpoint_rounds_away_from_zero()
    {
        EstimateTotals totals = EstimateCalculator.Calculate(
            [new EstimateLineAmount(1m, 10.00m, true)],
            discount: 0m,
            taxRatePercent: 8.25m);

        Assert.Equal(0.83m, totals.Tax);
        Assert.Equal(10.83m, totals.Total);
    }

    [Fact]
    public void Empty_document_has_zero_totals()
    {
        EstimateTotals totals = EstimateCalculator.Calculate([], 0m, 0m);
        Assert.Equal(0m, totals.Subtotal);
        Assert.Equal(0m, totals.Total);
    }

    [Fact]
    public void Discount_cannot_exceed_subtotal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EstimateCalculator.Calculate([new EstimateLineAmount(1m, 10m, true)], 10.01m, 0m));
    }
}

public sealed class EstimateStatusRulesTests
{
    [Theory]
    [InlineData(EstimateStatus.Draft, EstimateStatus.Sent, true)]
    [InlineData(EstimateStatus.Draft, EstimateStatus.Declined, true)]
    [InlineData(EstimateStatus.Draft, EstimateStatus.Accepted, false)]
    [InlineData(EstimateStatus.Sent, EstimateStatus.Draft, true)]
    [InlineData(EstimateStatus.Sent, EstimateStatus.Accepted, true)]
    [InlineData(EstimateStatus.Sent, EstimateStatus.Expired, true)]
    [InlineData(EstimateStatus.Accepted, EstimateStatus.Converted, true)]
    [InlineData(EstimateStatus.Accepted, EstimateStatus.Draft, false)]
    [InlineData(EstimateStatus.Declined, EstimateStatus.Draft, true)]
    [InlineData(EstimateStatus.Expired, EstimateStatus.Draft, true)]
    [InlineData(EstimateStatus.Converted, EstimateStatus.Draft, false)]
    public void Transition_matrix_is_explicit(EstimateStatus from, EstimateStatus to, bool allowed)
    {
        Assert.Equal(allowed, EstimateStatusRules.CanTransition(from, to));
    }

    [Fact]
    public void Only_draft_estimates_can_be_edited()
    {
        Assert.True(EstimateStatusRules.CanEdit(EstimateStatus.Draft));
        Assert.False(EstimateStatusRules.CanEdit(EstimateStatus.Sent));
        Assert.False(EstimateStatusRules.CanEdit(EstimateStatus.Accepted));
        Assert.False(EstimateStatusRules.CanEdit(EstimateStatus.Converted));
    }

    [Fact]
    public void User_facing_targets_omit_conversion()
    {
        Assert.DoesNotContain(EstimateStatus.Converted, EstimateStatusRules.UserFacingTargets(EstimateStatus.Accepted));
        Assert.Contains(EstimateStatus.Converted, EstimateStatusRules.AllowedTargets(EstimateStatus.Accepted));
    }
}

public sealed class EstimateNumberTests
{
    [Fact]
    public void Format_pads_to_four_digits_until_the_sequence_grows()
    {
        Assert.Equal("EST-0001", EstimateNumber.Format(DocumentPrefix.EstimateDefault, 1).Value);
        Assert.Equal("EST-10000", EstimateNumber.Format(DocumentPrefix.EstimateDefault, 10000).Value);
    }
}

public sealed class EstimateTests
{
    [Fact]
    public void Create_starts_as_draft_with_zero_totals()
    {
        Estimate estimate = Draft();

        Assert.Equal(EstimateStatus.Draft, estimate.Status);
        Assert.True(estimate.CanEdit);
        Assert.Equal("EST-0001", estimate.Number);
        Assert.Equal(0m, estimate.Total);
        Assert.Equal("USD", estimate.Currency.Value);
    }

    [Fact]
    public void Adding_lines_recalculates_persisted_totals()
    {
        Estimate estimate = Draft();
        estimate.AddLine(null, "Design", 2m, CatalogUnitType.Hour, 100m, true);
        estimate.UpdateHeader(estimate.ClientId, estimate.IssueDate, null, null, null, 20m, 10m);

        Assert.Equal(200.00m, estimate.Subtotal);
        Assert.Equal(20.00m, estimate.Discount);
        Assert.Equal(180.00m, estimate.TaxableSubtotal);
        Assert.Equal(18.00m, estimate.Tax);
        Assert.Equal(198.00m, estimate.Total);
        Assert.Equal(200.00m, estimate.Lines[0].LineAmount);
    }

    [Fact]
    public void Line_snapshots_keep_the_values_supplied_at_add_time()
    {
        Guid catalogId = Guid.NewGuid();
        Estimate estimate = Draft();
        estimate.AddLine(catalogId, "Copied description", 1.5m, CatalogUnitType.Hour, 125.5m, true);

        EstimateLine line = estimate.Lines[0];
        Assert.Equal(catalogId, line.CatalogItemId);
        Assert.Equal("Copied description", line.Description);
        Assert.Equal(1.5m, line.Quantity);
        Assert.Equal(125.5m, line.UnitPrice);
        Assert.True(line.IsTaxable);
    }

    [Fact]
    public void Removing_a_line_clamps_discount_to_the_new_subtotal()
    {
        Estimate estimate = Draft();
        estimate.AddLine(null, "A", 1m, CatalogUnitType.Item, 40m, false);
        estimate.AddLine(null, "B", 1m, CatalogUnitType.Item, 10m, false);
        estimate.UpdateHeader(estimate.ClientId, estimate.IssueDate, null, null, null, 30m, 0m);

        estimate.RemoveLine(estimate.Lines[0].Id);

        Assert.Equal(10.00m, estimate.Subtotal);
        Assert.Equal(10.00m, estimate.Discount);
        Assert.Equal(0.00m, estimate.Total);
        Assert.Equal(0, estimate.Lines[0].SortOrder);
    }

    [Fact]
    public void Reorder_updates_sort_order()
    {
        Estimate estimate = Draft();
        EstimateLine first = estimate.AddLine(null, "First", 1m, CatalogUnitType.Item, 1m, false);
        EstimateLine second = estimate.AddLine(null, "Second", 1m, CatalogUnitType.Item, 2m, false);

        estimate.ReorderLines([second.Id, first.Id]);

        Assert.Equal(second.Id, estimate.Lines[0].Id);
        Assert.Equal(0, estimate.Lines[0].SortOrder);
        Assert.Equal(1, estimate.Lines[1].SortOrder);
    }

    [Fact]
    public void Stage_line_reorder_uses_an_offset_that_does_not_overlap_final_order()
    {
        Estimate estimate = Draft();
        EstimateLine first = estimate.AddLine(null, "First", 1m, CatalogUnitType.Item, 1m, false);
        EstimateLine second = estimate.AddLine(null, "Second", 1m, CatalogUnitType.Item, 2m, false);

        estimate.StageLineReorder([second.Id, first.Id]);

        Assert.Equal(Estimate.MaxLines, estimate.Lines[0].SortOrder);
        Assert.Equal(Estimate.MaxLines + 1, estimate.Lines[1].SortOrder);
    }

    [Fact]
    public void Send_requires_at_least_one_line()
    {
        Estimate estimate = Draft();
        Assert.Throws<InvalidOperationException>(() => estimate.TransitionTo(EstimateStatus.Sent));

        estimate.AddLine(null, "Work", 1m, CatalogUnitType.Hour, 50m, false);
        estimate.TransitionTo(EstimateStatus.Sent);
        Assert.Equal(EstimateStatus.Sent, estimate.Status);
        Assert.False(estimate.CanEdit);
    }

    [Fact]
    public void Accepted_and_converted_estimates_cannot_be_edited()
    {
        Estimate estimate = Draft();
        estimate.AddLine(null, "Work", 1m, CatalogUnitType.Hour, 50m, false);
        estimate.TransitionTo(EstimateStatus.Sent);
        estimate.TransitionTo(EstimateStatus.Accepted);

        Assert.Throws<InvalidOperationException>(() =>
            estimate.UpdateHeader(estimate.ClientId, estimate.IssueDate, null, "no", null, 0m, 0m));
        Assert.Throws<InvalidOperationException>(() =>
            estimate.AddLine(null, "More", 1m, CatalogUnitType.Hour, 10m, false));

        estimate.TransitionTo(EstimateStatus.Converted);
        Assert.Throws<InvalidOperationException>(() => estimate.TransitionTo(EstimateStatus.Draft));
    }

    [Fact]
    public void Duplicate_copies_line_snapshots_onto_a_new_draft()
    {
        Estimate estimate = Draft();
        estimate.AddLine(Guid.NewGuid(), "Snapshot", 3m, CatalogUnitType.Day, 700m, true);
        estimate.UpdateHeader(estimate.ClientId, estimate.IssueDate, null, "Note", "Net 15", 50m, 5m);

        Estimate copy = estimate.Duplicate(
            2,
            EstimateNumber.Format(DocumentPrefix.EstimateDefault, 2),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 15));

        Assert.NotEqual(estimate.Id, copy.Id);
        Assert.Equal("EST-0002", copy.Number);
        Assert.Equal(EstimateStatus.Draft, copy.Status);
        Assert.Equal(estimate.ClientId, copy.ClientId);
        Assert.Equal("Note", copy.Notes);
        Assert.Equal(50m, copy.Discount);
        Assert.Equal(5m, copy.TaxRatePercent);
        Assert.Equal(estimate.Lines[0].Description, copy.Lines[0].Description);
        Assert.Equal(estimate.Lines[0].UnitPrice, copy.Lines[0].UnitPrice);
        Assert.NotEqual(estimate.Lines[0].Id, copy.Lines[0].Id);
        Assert.Equal(estimate.Total, copy.Total);
    }

    [Fact]
    public void Header_discount_greater_than_subtotal_is_rejected()
    {
        Estimate estimate = Draft();
        estimate.AddLine(null, "Work", 1m, CatalogUnitType.Hour, 10m, false);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            estimate.UpdateHeader(estimate.ClientId, estimate.IssueDate, null, null, null, 10.01m, 0m));
    }

    private static Estimate Draft() =>
        Estimate.Create(
            1,
            EstimateNumber.Format(DocumentPrefix.EstimateDefault, 1),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 22),
            null,
            null,
            null,
            0m,
            0m,
            CurrencyCode.Usd);
}
