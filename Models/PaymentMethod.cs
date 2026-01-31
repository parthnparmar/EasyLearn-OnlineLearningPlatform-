using System.ComponentModel.DataAnnotations;

namespace EasyLearn.Models;

public enum PaymentMethodType
{
    CreditCard = 1,
    DebitCard = 2,
    UPI = 3,
    GooglePay = 4,
    PhonePe = 5,
    Paytm = 6,
    QRCode = 7
}

public class PaymentMethodInfo
{
    public PaymentMethodType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public static class PaymentMethods
{
    public static readonly List<PaymentMethodInfo> AvailableMethods = new()
    {
        new() { Type = PaymentMethodType.CreditCard, DisplayName = "Credit Card", Icon = "fas fa-credit-card", IsEnabled = true },
        new() { Type = PaymentMethodType.DebitCard, DisplayName = "Debit Card", Icon = "fas fa-credit-card", IsEnabled = true },
        new() { Type = PaymentMethodType.UPI, DisplayName = "UPI", Icon = "fas fa-mobile-alt", IsEnabled = true },
        new() { Type = PaymentMethodType.QRCode, DisplayName = "QR Code", Icon = "fas fa-qrcode", IsEnabled = true },
        new() { Type = PaymentMethodType.GooglePay, DisplayName = "Google Pay", Icon = "fab fa-google-pay", IsEnabled = true },
        new() { Type = PaymentMethodType.PhonePe, DisplayName = "PhonePe", Icon = "fas fa-mobile-alt", IsEnabled = true },
        new() { Type = PaymentMethodType.Paytm, DisplayName = "Paytm", Icon = "fas fa-wallet", IsEnabled = true }
    };
}