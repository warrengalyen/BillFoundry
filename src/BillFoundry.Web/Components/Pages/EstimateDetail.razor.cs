using BillFoundry.Application.Estimates;
using BillFoundry.Domain.Estimates;
using BillFoundry.Web.Estimates;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class EstimateDetail
{
    private bool _loading = true;
    private bool _notFound;
    private Guid? _editingLineId;

    [Parameter]
    public Guid Id { get; set; }

    private EstimateDetailsDto? Estimate { get; set; }

    private IReadOnlyList<EstimateCatalogOption> CatalogItems { get; set; } = [];

    private EstimateLineInput LineInput { get; set; } = new();

    private Guid SelectedCatalogItemId { get; set; }

    private string? StatusMessage { get; set; }

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    private decimal PreviewLineAmount => EstimateCalculator.LineAmount(LineInput.Quantity, LineInput.UnitPrice);

    protected override async Task OnParametersSetAsync()
    {
        EstimateOptionsResult options = await Estimates.GetOptionsAsync();
        if (options.Succeeded && options.Options is not null)
        {
            CatalogItems = options.Options.CatalogItems;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        EstimateResult result = await Estimates.GetAsync(Id);
        _loading = false;
        ApplyResult(result, successMessage: null);
    }

    private void BeginEditLine(EstimateLineDto line)
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

        EstimateCatalogOption? item = CatalogItems.FirstOrDefault(option => option.Id == SelectedCatalogItemId);
        if (item is not null)
        {
            LineInput.ApplyCatalogItem(item);
        }
    }

    private async Task SaveLineAsync()
    {
        if (Estimate is null)
        {
            return;
        }

        ClearMessages();
        EstimateResult result;
        if (_editingLineId is Guid lineId)
        {
            result = await Estimates.UpdateLineAsync(LineInput.ToUpdateCommand(Id, lineId, Estimate.RowVersion));
        }
        else
        {
            result = await Estimates.AddLineAsync(LineInput.ToAddCommand(Id, Estimate.RowVersion));
        }

        string message = _editingLineId is null ? "The line was added." : "The line was saved.";
        if (result.Succeeded)
        {
            CancelLineEdit();
            ApplyResult(result, message);
            return;
        }

        ApplyResult(result, successMessage: null);
    }

    private async Task RemoveLineAsync(Guid lineId)
    {
        if (Estimate is null)
        {
            return;
        }

        ClearMessages();
        EstimateResult result = await Estimates.RemoveLineAsync(new RemoveEstimateLineCommand
        {
            Id = Id,
            LineId = lineId,
            RowVersion = Estimate.RowVersion
        });
        ApplyResult(result, "The line was removed.");
    }

    private async Task MoveLineAsync(Guid lineId, int delta)
    {
        if (Estimate is null)
        {
            return;
        }

        List<Guid> ids = [.. Estimate.Lines.Select(line => line.Id)];
        int index = ids.IndexOf(lineId);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= ids.Count)
        {
            return;
        }

        ids.RemoveAt(index);
        ids.Insert(target, lineId);

        ClearMessages();
        EstimateResult result = await Estimates.ReorderLinesAsync(new ReorderEstimateLinesCommand
        {
            Id = Id,
            RowVersion = Estimate.RowVersion,
            LineIds = ids
        });
        ApplyResult(result, "The line order was updated.");
    }

    private async Task TransitionAsync(EstimateStatus target)
    {
        if (Estimate is null)
        {
            return;
        }

        ClearMessages();
        EstimateResult result = await Estimates.TransitionAsync(new TransitionEstimateCommand
        {
            Id = Id,
            RowVersion = Estimate.RowVersion,
            Target = target
        });
        ApplyResult(result, $"The estimate is {EstimateStatusRules.Label(target).ToLowerInvariant()}.");
    }

    private async Task DuplicateAsync()
    {
        ClearMessages();
        EstimateResult result = await Estimates.DuplicateAsync(new DuplicateEstimateCommand { Id = Id });
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.Succeeded && result.Estimate is not null)
        {
            Navigation.NavigateTo($"/Estimates/{result.Estimate.Id}");
            return;
        }

        ApplyResult(result, successMessage: null);
    }

    private void ApplyResult(EstimateResult result, string? successMessage)
    {
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.IsNotFound)
        {
            _notFound = true;
            Estimate = null;
            return;
        }

        if (result.Estimate is not null)
        {
            Estimate = result.Estimate;
        }

        if (result.Succeeded)
        {
            StatusMessage = successMessage;
            return;
        }

        Errors = [.. result.Errors];
        if (result.IsConcurrencyConflict)
        {
            ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "The estimate was updated by another user.";
            Errors = [];
        }
    }

    private void ClearMessages()
    {
        StatusMessage = null;
        ErrorMessage = null;
        Errors = [];
    }
}
