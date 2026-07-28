using System.ComponentModel.DataAnnotations;
using BillFoundry.Application.Estimates;
using BillFoundry.Domain.Estimates;

namespace BillFoundry.Web.Estimates;

public sealed class EstimateInput
{
    [Required]
    public Guid ClientId { get; set; }

    [Required]
    public DateOnly IssueDate { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    [StringLength(Estimate.NotesMaxLength)]
    public string? Notes { get; set; }

    [StringLength(Estimate.TermsMaxLength)]
    public string? Terms { get; set; }

    [Range(0, (double)Estimate.MaxDiscount)]
    public decimal Discount { get; set; }

    [Range(0, (double)Estimate.MaxTaxRatePercent)]
    public decimal TaxRatePercent { get; set; }

    public string RowVersionBase64 { get; set; } = string.Empty;

    public byte[] RowVersionBytes =>
        string.IsNullOrWhiteSpace(RowVersionBase64) ? [] : Convert.FromBase64String(RowVersionBase64);

    public void CopyFrom(EstimateDetailsDto estimate)
    {
        ClientId = estimate.ClientId;
        IssueDate = estimate.IssueDate;
        ExpirationDate = estimate.ExpirationDate;
        Notes = estimate.Notes;
        Terms = estimate.Terms;
        Discount = estimate.Discount;
        TaxRatePercent = estimate.TaxRatePercent;
        RowVersionBase64 = Convert.ToBase64String(estimate.RowVersion);
    }

    public void ApplyDefaults(EstimateFormOptions options)
    {
        IssueDate = options.Today;
        ExpirationDate = options.DefaultPaymentTermsDays > 0
            ? options.Today.AddDays(options.DefaultPaymentTermsDays)
            : null;
        Notes = options.DefaultNotes;
        if (options.Clients.Count == 1)
        {
            ClientId = options.Clients[0].Id;
        }
    }

    public SaveEstimateCommand ToCreateCommand() => ToSaveCommand();

    public UpdateEstimateCommand ToUpdateCommand(Guid id)
    {
        UpdateEstimateCommand command = new()
        {
            Id = id,
            RowVersion = RowVersionBytes
        };
        CopyTo(command);
        return command;
    }

    private SaveEstimateCommand ToSaveCommand()
    {
        var command = new SaveEstimateCommand();
        CopyTo(command);
        return command;
    }

    private void CopyTo(SaveEstimateCommand command)
    {
        command.ClientId = ClientId;
        command.IssueDate = IssueDate;
        command.ExpirationDate = ExpirationDate;
        command.Notes = Notes;
        command.Terms = Terms;
        command.Discount = Discount;
        command.TaxRatePercent = TaxRatePercent;
    }
}
