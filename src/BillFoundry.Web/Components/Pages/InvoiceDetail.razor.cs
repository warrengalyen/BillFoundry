using System.ComponentModel.DataAnnotations;
using BillFoundry.Application.Auditing;
using BillFoundry.Application.Invoices;
using BillFoundry.Domain.Invoices;
using BillFoundry.Web.Invoices;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class InvoiceDetail
{
    private bool _loading = true;
    private bool _notFound;
    private Guid? _editingLineId;
    private Guid? _reversingPaymentId;
    private DateOnly _today;

    [Parameter]
    public Guid Id { get; set; }

    private InvoiceDetailsDto? Invoice { get; set; }

    private IReadOnlyList<AuditEventDto> Activity { get; set; } = [];

    private IReadOnlyList<InvoiceCatalogOption> CatalogItems { get; set; } = [];

    private InvoiceLineInput LineInput { get; set; } = new();

    private VoidInvoiceInput VoidInput { get; set; } = new();

    private PaymentInput PaymentInput { get; set; } = new();

    private ReversePaymentInput ReverseInput { get; set; } = new();

    private Guid SelectedCatalogItemId { get; set; }

    private string? StatusMessage { get; set; }

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    private decimal PreviewLineAmount => InvoiceCalculator.LineAmount(LineInput.Quantity, LineInput.UnitPrice);

    protected override async Task OnParametersSetAsync()
    {
        InvoiceOptionsResult options = await Invoices.GetOptionsAsync();
        if (options.Succeeded && options.Options is not null)
        {
            CatalogItems = options.Options.CatalogItems;
            _today = options.Options.Today;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        InvoiceResult result = await Invoices.GetAsync(Id);
        _loading = false;
        await ApplyResultAsync(result, successMessage: null);
        InitializePaymentForm();
    }

    private void BeginEditLine(InvoiceLineDto line)
    {
        _editingLineId = line.Id;
        LineInput.CopyFrom(line);
        SelectedCatalogItemId = line.CatalogItemId ?? Guid.Empty;
        ClearMessages();
    }

    private void CancelLineEdit()
    {
        _editingLineId = null;
        LineInput = new();
        SelectedCatalogItemId = Guid.Empty;
    }

    private void ApplyCatalogSelection()
    {
        if (SelectedCatalogItemId == Guid.Empty)
        {
            LineInput.CatalogItemId = null;
            return;
        }

        InvoiceCatalogOption? item = CatalogItems.FirstOrDefault(option => option.Id == SelectedCatalogItemId);
        if (item is not null)
        {
            LineInput.ApplyCatalogItem(item);
        }
    }

    private async Task SaveLineAsync()
    {
        if (Invoice is null)
        {
            return;
        }

        ClearMessages();
        InvoiceResult result;
        if (_editingLineId is Guid lineId)
        {
            result = await Invoices.UpdateLineAsync(LineInput.ToUpdateCommand(Id, lineId, Invoice.RowVersion));
        }
        else
        {
            result = await Invoices.AddLineAsync(LineInput.ToAddCommand(Id, Invoice.RowVersion));
        }

        string message = _editingLineId is null ? "The line was added." : "The line was saved.";
        if (result.Succeeded)
        {
            CancelLineEdit();
            await ApplyResultAsync(result, message);
            return;
        }

        await ApplyResultAsync(result, successMessage: null);
    }

    private async Task RemoveLineAsync(Guid lineId)
    {
        if (Invoice is null)
        {
            return;
        }

        ClearMessages();
        InvoiceResult result = await Invoices.RemoveLineAsync(new RemoveInvoiceLineCommand
        {
            Id = Id,
            LineId = lineId,
            RowVersion = Invoice.RowVersion
        });
        await ApplyResultAsync(result, "The line was removed.");
    }

    private async Task MoveLineAsync(Guid lineId, int delta)
    {
        if (Invoice is null)
        {
            return;
        }

        List<Guid> ids = [.. Invoice.Lines.Select(line => line.Id)];
        int index = ids.IndexOf(lineId);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= ids.Count)
        {
            return;
        }

        ids.RemoveAt(index);
        ids.Insert(target, lineId);

        ClearMessages();
        InvoiceResult result = await Invoices.ReorderLinesAsync(new ReorderInvoiceLinesCommand
        {
            Id = Id,
            RowVersion = Invoice.RowVersion,
            LineIds = ids
        });
        await ApplyResultAsync(result, "The line order was updated.");
    }

    private async Task MarkSentAsync()
    {
        if (Invoice is null)
        {
            return;
        }

        ClearMessages();
        InvoiceResult result = await Invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = Id,
            RowVersion = Invoice.RowVersion
        });
        await ApplyResultAsync(result, "The invoice is sent.");
        InitializePaymentForm();
    }

    private async Task VoidAsync()
    {
        if (Invoice is null)
        {
            return;
        }

        ClearMessages();
        InvoiceResult result = await Invoices.VoidAsync(new VoidInvoiceCommand
        {
            Id = Id,
            RowVersion = Invoice.RowVersion,
            Reason = VoidInput.Reason
        });
        if (result.Succeeded)
        {
            VoidInput = new();
        }

        await ApplyResultAsync(result, "The invoice is void.");
    }

    private async Task RecordPaymentAsync()
    {
        if (Invoice is null)
        {
            return;
        }

        ClearMessages();
        InvoiceResult result = await Invoices.RecordPaymentAsync(PaymentInput.ToCommand(Id, Invoice.RowVersion));
        if (result.Succeeded)
        {
            CancelReverse();
            await ApplyResultAsync(result, "The payment was recorded.");
            InitializePaymentForm();
            return;
        }

        await ApplyResultAsync(result, successMessage: null);
    }

    private void BeginReverse(Guid paymentId)
    {
        _reversingPaymentId = paymentId;
        ReverseInput = new();
        ClearMessages();
    }

    private void CancelReverse()
    {
        _reversingPaymentId = null;
        ReverseInput = new();
    }

    private async Task ReversePaymentAsync()
    {
        if (Invoice is null || _reversingPaymentId is not Guid paymentId)
        {
            return;
        }

        ClearMessages();
        InvoiceResult result = await Invoices.ReversePaymentAsync(new ReversePaymentCommand
        {
            Id = Id,
            PaymentId = paymentId,
            RowVersion = Invoice.RowVersion,
            Reason = ReverseInput.Reason
        });
        if (result.Succeeded)
        {
            CancelReverse();
            await ApplyResultAsync(result, "The payment was reversed.");
            InitializePaymentForm();
            return;
        }

        await ApplyResultAsync(result, successMessage: null);
    }

    private void InitializePaymentForm()
    {
        if (Invoice is null || !Invoice.CanRecordPayment)
        {
            return;
        }

        PaymentInput.ApplyDefaults(_today, Invoice.BalanceDue);
    }

    private async Task DuplicateAsync()
    {
        ClearMessages();
        InvoiceResult result = await Invoices.DuplicateAsync(new DuplicateInvoiceCommand { Id = Id });
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.Succeeded && result.Invoice is not null)
        {
            Navigation.NavigateTo($"/Invoices/{result.Invoice.Id}");
            return;
        }

        await ApplyResultAsync(result, successMessage: null);
    }

    private async Task ApplyResultAsync(InvoiceResult result, string? successMessage)
    {
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.IsNotFound)
        {
            _notFound = true;
            Invoice = null;
            Activity = [];
            return;
        }

        if (result.Invoice is not null)
        {
            Invoice = result.Invoice;
        }

        if (Invoice is not null)
        {
            await RefreshActivityAsync();
        }

        if (result.Succeeded)
        {
            StatusMessage = successMessage;
            return;
        }

        Errors = [.. result.Errors];
        if (result.IsConcurrencyConflict)
        {
            ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "The invoice was updated by another user.";
            Errors = [];
        }
    }

    private async Task RefreshActivityAsync()
    {
        AuditQueryResult<IReadOnlyList<AuditEventDto>> result =
            await Audit.ListForEntityAsync(AuditEntityTypes.Invoice, Id);
        Activity = result.Succeeded && result.Value is not null ? result.Value : [];
    }

    private void ClearMessages()
    {
        StatusMessage = null;
        ErrorMessage = null;
        Errors = [];
    }

    public sealed class VoidInvoiceInput
    {
        [Required]
        [StringLength(Domain.Invoices.Invoice.VoidReasonMaxLength)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class ReversePaymentInput
    {
        [Required]
        [StringLength(InvoicePayment.ReversalReasonMaxLength)]
        public string Reason { get; set; } = string.Empty;
    }
}
