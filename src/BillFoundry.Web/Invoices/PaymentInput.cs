using System.ComponentModel.DataAnnotations;
using BillFoundry.Application.Invoices;
using BillFoundry.Domain.Invoices;

namespace BillFoundry.Web.Invoices;

public sealed class PaymentInput
{
    [Required]
    public DateOnly PaymentDate { get; set; }

    [Range(typeof(decimal), "0.01", "99999999.99")]
    public decimal Amount { get; set; }

    [Required]
    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;

    [StringLength(InvoicePayment.ReferenceMaxLength)]
    public string? Reference { get; set; }

    [StringLength(InvoicePayment.NotesMaxLength)]
    public string? Notes { get; set; }

    public void ApplyDefaults(DateOnly today, decimal balanceDue)
    {
        PaymentDate = today;
        Amount = balanceDue;
        Method = PaymentMethod.BankTransfer;
        Reference = null;
        Notes = null;
    }

    public RecordPaymentCommand ToCommand(Guid invoiceId, byte[] rowVersion) =>
        new()
        {
            Id = invoiceId,
            RowVersion = rowVersion,
            PaymentDate = PaymentDate,
            Amount = Amount,
            Method = Method,
            Reference = Reference,
            Notes = Notes
        };
}
