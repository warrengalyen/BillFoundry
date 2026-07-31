using System.ComponentModel.DataAnnotations;
using BillFoundry.Application.Invoices;
using BillFoundry.Domain.Invoices;

namespace BillFoundry.Web.Invoices;

public sealed class InvoiceInput
{
    [Required]
    public Guid ClientId { get; set; }

    [Required]
    public DateOnly IssueDate { get; set; }

    [Required]
    public DateOnly DueDate { get; set; }

    [StringLength(Invoice.PurchaseOrderMaxLength)]
    public string? PurchaseOrder { get; set; }

    [StringLength(Invoice.NotesMaxLength)]
    public string? Notes { get; set; }

    [StringLength(Invoice.PaymentInstructionsMaxLength)]
    public string? PaymentInstructions { get; set; }

    [Range(0, (double)Invoice.MaxDiscount)]
    public decimal Discount { get; set; }

    [Range(0, (double)Invoice.MaxTaxRatePercent)]
    public decimal TaxRatePercent { get; set; }

    public string RowVersionBase64 { get; set; } = string.Empty;

    public byte[] RowVersionBytes =>
        string.IsNullOrWhiteSpace(RowVersionBase64) ? [] : Convert.FromBase64String(RowVersionBase64);

    public void CopyFrom(InvoiceDetailsDto invoice)
    {
        ClientId = invoice.ClientId;
        IssueDate = invoice.IssueDate;
        DueDate = invoice.DueDate;
        PurchaseOrder = invoice.PurchaseOrder;
        Notes = invoice.Notes;
        PaymentInstructions = invoice.PaymentInstructions;
        Discount = invoice.Discount;
        TaxRatePercent = invoice.TaxRatePercent;
        RowVersionBase64 = Convert.ToBase64String(invoice.RowVersion);
    }

    public void ApplyDefaults(InvoiceFormOptions options)
    {
        IssueDate = options.Today;
        DueDate = options.DefaultPaymentTermsDays > 0
            ? options.Today.AddDays(options.DefaultPaymentTermsDays)
            : options.Today;
        Notes = options.DefaultNotes;
        PaymentInstructions = options.DefaultPaymentInstructions;
        if (options.Clients.Count == 1)
        {
            ClientId = options.Clients[0].Id;
        }
    }

    public SaveInvoiceCommand ToCreateCommand() => ToSaveCommand();

    public UpdateInvoiceCommand ToUpdateCommand(Guid id)
    {
        UpdateInvoiceCommand command = new()
        {
            Id = id,
            RowVersion = RowVersionBytes
        };
        CopyTo(command);
        return command;
    }

    private SaveInvoiceCommand ToSaveCommand()
    {
        var command = new SaveInvoiceCommand();
        CopyTo(command);
        return command;
    }

    private void CopyTo(SaveInvoiceCommand command)
    {
        command.ClientId = ClientId;
        command.IssueDate = IssueDate;
        command.DueDate = DueDate;
        command.PurchaseOrder = PurchaseOrder;
        command.Notes = Notes;
        command.PaymentInstructions = PaymentInstructions;
        command.Discount = Discount;
        command.TaxRatePercent = TaxRatePercent;
    }
}
