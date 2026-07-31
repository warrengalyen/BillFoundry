namespace BillFoundry.Domain.Invoices;

public enum PaymentMethod
{
    Cash = 0,
    Check = 1,
    BankTransfer = 2,
    CreditCard = 3,
    PayPal = 4,
    Other = 5
}

public static class PaymentMethodDisplay
{
    public static string Label(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Check => "Check",
        PaymentMethod.BankTransfer => "Bank transfer",
        PaymentMethod.CreditCard => "Credit card",
        PaymentMethod.PayPal => "PayPal",
        PaymentMethod.Other => "Other",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "The payment method is not supported.")
    };

    public static bool IsDefined(PaymentMethod method) => Enum.IsDefined(method);

    public static IReadOnlyList<PaymentMethod> All { get; } =
        [PaymentMethod.Cash, PaymentMethod.Check, PaymentMethod.BankTransfer, PaymentMethod.CreditCard, PaymentMethod.PayPal, PaymentMethod.Other];
}
