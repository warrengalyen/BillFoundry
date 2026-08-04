namespace BillFoundry.Web.Components.Pages;

public sealed record ReportBarItem(string Label, decimal Value, string FormattedValue, decimal Scale)
{
    public int WidthPercent
    {
        get
        {
            if (Scale <= 0m || Value <= 0m)
            {
                return 0;
            }

            int percent = (int)decimal.Round(Value / Scale * 100m, 0, MidpointRounding.AwayFromZero);
            return Math.Clamp(percent, Value > 0m ? 2 : 0, 100);
        }
    }
}
