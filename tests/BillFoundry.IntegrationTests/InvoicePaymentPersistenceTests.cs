using BillFoundry.Application.Clients;
using BillFoundry.Application.Invoices;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class InvoicePaymentPersistenceTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public InvoicePaymentPersistenceTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"Pay-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Partial_and_full_payments_update_totals_and_status()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        InvoiceDetailsDto sent = await SentInvoiceAsync(invoices, clientId, 100m);

        InvoiceResult partial = await invoices.RecordPaymentAsync(Payment(sent, 40m, PaymentMethod.Check, "CHK-1"));
        Assert.True(partial.Succeeded, string.Join("; ", partial.Errors));
        Assert.Equal(InvoiceStatus.PartiallyPaid, partial.Invoice?.Status);
        Assert.Equal(40.00m, partial.Invoice?.AmountPaid);
        Assert.Equal(60.00m, partial.Invoice?.BalanceDue);
        Assert.True(partial.Invoice?.CanRecordPayment);
        Assert.False(partial.Invoice?.CanVoid);
        Assert.Single(partial.Invoice!.Payments);
        Assert.Equal("CHK-1", partial.Invoice.Payments[0].Reference);
        Assert.True(partial.Invoice.Payments[0].CanReverse);
        Assert.NotEqual(default, partial.Invoice.Payments[0].CreatedAtUtc);

        InvoiceResult paid = await invoices.RecordPaymentAsync(Payment(partial.Invoice, 60m, PaymentMethod.BankTransfer, "WIRE-2"));
        Assert.True(paid.Succeeded, string.Join("; ", paid.Errors));
        Assert.Equal(InvoiceStatus.Paid, paid.Invoice?.Status);
        Assert.Equal(100.00m, paid.Invoice?.AmountPaid);
        Assert.Equal(0m, paid.Invoice?.BalanceDue);
        Assert.False(paid.Invoice?.CanRecordPayment);
        Assert.Equal(2, paid.Invoice?.Payments.Count);

        InvoiceResult reloaded = await invoices.GetAsync(sent.Id);
        Assert.Equal(InvoiceStatus.Paid, reloaded.Invoice?.Status);
        Assert.Equal(2, reloaded.Invoice?.Payments.Count);

        InvoiceListResult listed = await invoices.ListAsync(new InvoiceListQuery
        {
            Search = _marker,
            Status = InvoiceStatusFilter.Paid,
            PageSize = 100
        });
        Assert.Contains(listed.Page!.Items, item => item.Id == sent.Id);

        await using var db = CreateDb();
        Guid? createdBy = await db.InvoicePayments
            .Where(payment => payment.InvoiceId == sent.Id)
            .Select(payment => payment.CreatedByUserId)
            .FirstAsync();
        Assert.NotNull(createdBy);
    }

    [Fact]
    public async Task Draft_void_zero_and_overpayments_are_rejected()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);

        InvoiceResult created = await invoices.CreateAsync(Header(clientId));
        InvoiceResult lined = await invoices.AddLineAsync(Line(created.Invoice!, "Work", 1m, 50m, false));
        InvoiceResult draftPay = await invoices.RecordPaymentAsync(Payment(lined.Invoice!, 10m, PaymentMethod.Cash, null));
        Assert.False(draftPay.Succeeded);
        Assert.Contains(draftPay.Errors, error => error.Contains("draft", StringComparison.OrdinalIgnoreCase));

        InvoiceResult sent = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = lined.Invoice!.Id,
            RowVersion = lined.Invoice.RowVersion
        });
        Assert.True(sent.Succeeded, string.Join("; ", sent.Errors));

        InvoiceResult zero = await invoices.RecordPaymentAsync(Payment(sent.Invoice!, 0m, PaymentMethod.Cash, null));
        Assert.False(zero.Succeeded);
        Assert.Contains(zero.Errors, error => error.Contains("greater than zero", StringComparison.OrdinalIgnoreCase));

        InvoiceResult negative = await invoices.RecordPaymentAsync(Payment(sent.Invoice!, -1m, PaymentMethod.Cash, null));
        Assert.False(negative.Succeeded);

        InvoiceResult overpay = await invoices.RecordPaymentAsync(Payment(sent.Invoice!, 50.01m, PaymentMethod.Cash, null));
        Assert.False(overpay.Succeeded);
        Assert.Contains(overpay.Errors, error => error.Contains("overpayment", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(InvoiceStatus.Sent, overpay.Invoice?.Status);
        Assert.Equal(0m, overpay.Invoice?.AmountPaid);
        Assert.Empty(overpay.Invoice!.Payments);

        InvoiceResult voided = await invoices.VoidAsync(new VoidInvoiceCommand
        {
            Id = sent.Invoice!.Id,
            RowVersion = sent.Invoice.RowVersion,
            Reason = "Cancelled before payment"
        });
        Assert.True(voided.Succeeded, string.Join("; ", voided.Errors));
        InvoiceResult voidPay = await invoices.RecordPaymentAsync(Payment(voided.Invoice!, 10m, PaymentMethod.Cash, null));
        Assert.False(voidPay.Succeeded);
        Assert.Contains(voidPay.Errors, error => error.Contains("void", StringComparison.OrdinalIgnoreCase));

        await using var db = CreateDb();
        Assert.Equal(0, await db.InvoicePayments.CountAsync(payment => payment.InvoiceId == sent.Invoice.Id));
    }

    [Fact]
    public async Task Reversal_corrects_totals_without_deleting_the_original()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        InvoiceDetailsDto sent = await SentInvoiceAsync(invoices, clientId, 80m);

        InvoiceResult paid = await invoices.RecordPaymentAsync(Payment(sent, 80m, PaymentMethod.PayPal, "TX-80"));
        Assert.True(paid.Succeeded, string.Join("; ", paid.Errors));
        Guid paymentId = paid.Invoice!.Payments[0].Id;

        InvoiceResult voidPaid = await invoices.VoidAsync(new VoidInvoiceCommand
        {
            Id = paid.Invoice.Id,
            RowVersion = paid.Invoice.RowVersion,
            Reason = "Should remain paid"
        });
        Assert.False(voidPaid.Succeeded);

        InvoiceResult reversed = await invoices.ReversePaymentAsync(new ReversePaymentCommand
        {
            Id = paid.Invoice.Id,
            PaymentId = paymentId,
            RowVersion = paid.Invoice.RowVersion,
            Reason = "Paid to the wrong invoice"
        });
        Assert.True(reversed.Succeeded, string.Join("; ", reversed.Errors));
        Assert.Equal(InvoiceStatus.Sent, reversed.Invoice?.Status);
        Assert.Equal(0m, reversed.Invoice?.AmountPaid);
        Assert.Equal(80m, reversed.Invoice?.BalanceDue);
        Assert.Equal(2, reversed.Invoice?.Payments.Count);
        Assert.Contains(reversed.Invoice!.Payments, payment => payment.Id == paymentId && !payment.CanReverse);
        Assert.Contains(reversed.Invoice.Payments, payment => payment.IsReversal && payment.ReversesPaymentId == paymentId);

        InvoiceResult again = await invoices.ReversePaymentAsync(new ReversePaymentCommand
        {
            Id = reversed.Invoice.Id,
            PaymentId = paymentId,
            RowVersion = reversed.Invoice.RowVersion,
            Reason = "Already reversed"
        });
        Assert.False(again.Succeeded);

        Guid reversalId = reversed.Invoice.Payments.Single(payment => payment.IsReversal).Id;
        InvoiceResult reverseReversal = await invoices.ReversePaymentAsync(new ReversePaymentCommand
        {
            Id = reversed.Invoice.Id,
            PaymentId = reversalId,
            RowVersion = reversed.Invoice.RowVersion,
            Reason = "Cannot reverse a reversal"
        });
        Assert.False(reverseReversal.Succeeded);

        InvoiceResult copy = await invoices.DuplicateAsync(new DuplicateInvoiceCommand { Id = reversed.Invoice.Id });
        Assert.True(copy.Succeeded, string.Join("; ", copy.Errors));
        Assert.Empty(copy.Invoice!.Payments);
        Assert.Equal(0m, copy.Invoice.AmountPaid);
        Assert.Equal(InvoiceStatus.Draft, copy.Invoice.Status);
    }

    [Fact]
    public async Task Stale_row_version_does_not_insert_a_payment()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        InvoiceDetailsDto sent = await SentInvoiceAsync(invoices, clientId, 100m);
        byte[] stale = sent.RowVersion;

        InvoiceResult first = await invoices.RecordPaymentAsync(Payment(sent, 40m, PaymentMethod.Cash, "first"));
        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        InvoiceResult second = await invoices.RecordPaymentAsync(new RecordPaymentCommand
        {
            Id = sent.Id,
            RowVersion = stale,
            PaymentDate = sent.IssueDate,
            Amount = 40m,
            Method = PaymentMethod.Cash,
            Reference = "stale"
        });
        Assert.True(second.IsConcurrencyConflict);

        InvoiceResult current = await invoices.GetAsync(sent.Id);
        Assert.Equal(40m, current.Invoice?.AmountPaid);
        Assert.Single(current.Invoice!.Payments);
        Assert.Equal("first", current.Invoice.Payments[0].Reference);

        await using var db = CreateDb();
        Assert.Equal(1, await db.InvoicePayments.CountAsync(payment => payment.InvoiceId == sent.Id));
    }

    [Fact]
    public async Task Concurrent_payments_keep_a_single_winner()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        Guid invoiceId;
        byte[] token;
        DateOnly issueDate;
        await using (AsyncServiceScope setup = provider.CreateAsyncScope())
        {
            (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(setup.ServiceProvider);
            InvoiceDetailsDto sent = await SentInvoiceAsync(invoices, clientId, 100m);
            invoiceId = sent.Id;
            token = sent.RowVersion;
            issueDate = sent.IssueDate;
        }

        InvoiceResult[] results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            IInvoiceService invoices = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            return await invoices.RecordPaymentAsync(new RecordPaymentCommand
            {
                Id = invoiceId,
                RowVersion = token,
                PaymentDate = issueDate,
                Amount = 60m,
                Method = PaymentMethod.Cash,
                Reference = $"race-{index}"
            });
        }));

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => !result.Succeeded);

        await using AsyncServiceScope verify = provider.CreateAsyncScope();
        IInvoiceService reader = verify.ServiceProvider.GetRequiredService<IInvoiceService>();
        InvoiceResult current = await reader.GetAsync(invoiceId);
        Assert.Equal(InvoiceStatus.PartiallyPaid, current.Invoice?.Status);
        Assert.Equal(60m, current.Invoice?.AmountPaid);
        Assert.Equal(40m, current.Invoice?.BalanceDue);
        Assert.Single(current.Invoice!.Payments);

        await using var db = CreateDb();
        Assert.Equal(1, await db.InvoicePayments.CountAsync(payment => payment.InvoiceId == invoiceId));
    }

    [Fact]
    public async Task Database_rejects_invalid_payment_method_amount_and_duplicate_reversal()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        InvoiceDetailsDto sent = await SentInvoiceAsync(invoices, clientId, 50m);
        InvoiceResult paid = await invoices.RecordPaymentAsync(Payment(sent, 50m, PaymentMethod.Other, "orig"));
        Assert.True(paid.Succeeded, string.Join("; ", paid.Errors));
        Guid paymentId = paid.Invoice!.Payments[0].Id;

        InvoiceResult reversed = await invoices.ReversePaymentAsync(new ReversePaymentCommand
        {
            Id = paid.Invoice.Id,
            PaymentId = paymentId,
            RowVersion = paid.Invoice.RowVersion,
            Reason = "Correct the receipt"
        });
        Assert.True(reversed.Succeeded, string.Join("; ", reversed.Errors));

        await using var db = CreateDb();
        Guid extraReversalId = Guid.NewGuid();
        SqlException duplicate = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO InvoicePayments
                    (Id, InvoiceId, PaymentDate, Amount, Method, Reference, Notes, ReversesPaymentId, ReversalReason, CreatedAtUtc)
                VALUES
                    ({extraReversalId}, {sent.Id}, {sent.IssueDate}, 50, N'Cash', NULL, NULL, {paymentId}, N'Second reversal', SYSDATETIMEOFFSET())
                """);
        });
        Assert.True(duplicate.Number is 2601 or 2627);

        SqlException method = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InvoicePayments SET Method = N'Bogus' WHERE Id = {paymentId}");
        });
        Assert.Equal(547, method.Number);

        SqlException amount = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InvoicePayments SET Amount = 0 WHERE Id = {paymentId}");
        });
        Assert.Equal(547, amount.Number);
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_record_payments()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, new UnauthenticatedCurrentUser());
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        InvoiceResult result = await invoices.RecordPaymentAsync(new RecordPaymentCommand
        {
            Id = Guid.NewGuid(),
            RowVersion = [1],
            PaymentDate = new DateOnly(2026, 8, 22),
            Amount = 10m,
            Method = PaymentMethod.Cash
        });
        Assert.True(result.IsForbidden);
    }

    private async Task<(IInvoiceService Invoices, Guid ClientId)> SeedClientAsync(IServiceProvider provider)
    {
        IClientService clients = provider.GetRequiredService<IClientService>();
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Client" });
        Assert.True(client.Succeeded, string.Join("; ", client.Errors));
        return (invoices, client.Client!.Id);
    }

    private async Task<InvoiceDetailsDto> SentInvoiceAsync(IInvoiceService invoices, Guid clientId, decimal amount)
    {
        InvoiceResult created = await invoices.CreateAsync(Header(clientId));
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        InvoiceResult lined = await invoices.AddLineAsync(Line(created.Invoice!, "Work", 1m, amount, false));
        Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));
        InvoiceResult sent = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = lined.Invoice!.Id,
            RowVersion = lined.Invoice.RowVersion
        });
        Assert.True(sent.Succeeded, string.Join("; ", sent.Errors));
        return sent.Invoice!;
    }

    private SaveInvoiceCommand Header(Guid clientId) =>
        new()
        {
            ClientId = clientId,
            IssueDate = new DateOnly(2026, 8, 22),
            DueDate = new DateOnly(2026, 9, 21),
            Notes = _marker
        };

    private static SaveInvoiceLineCommand Line(
        InvoiceDetailsDto invoice,
        string description,
        decimal quantity,
        decimal unitPrice,
        bool taxable) =>
        new()
        {
            Id = invoice.Id,
            RowVersion = invoice.RowVersion,
            Description = description,
            Quantity = quantity,
            Unit = CatalogUnitType.Hour,
            UnitPrice = unitPrice,
            IsTaxable = taxable
        };

    private static RecordPaymentCommand Payment(
        InvoiceDetailsDto invoice,
        decimal amount,
        PaymentMethod method,
        string? reference) =>
        new()
        {
            Id = invoice.Id,
            RowVersion = invoice.RowVersion,
            PaymentDate = invoice.IssueDate,
            Amount = amount,
            Method = method,
            Reference = reference
        };

    private BillFoundryDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<BillFoundryDbContext>()
            .UseSqlServer(_sql.ConnectionString)
            .Options);

    private sealed class FixedDateTimeProvider(DateOnly today) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
