using EasyLearn.Models;

namespace EasyLearn.Services;

public interface IQRCodeService
{
    string GenerateQRCodeUrl(string data);
    string GeneratePaymentQRCode(decimal amount, string courseName, string transactionId);
    string GenerateVerificationUrl(string certificateNumber);
}

public class QRCodeService : IQRCodeService
{
    private readonly IConfiguration _configuration;

    public QRCodeService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateQRCodeUrl(string data)
    {
        var encodedData = Uri.EscapeDataString(data);
        return $"https://chart.googleapis.com/chart?chs=250x250&cht=qr&chl={encodedData}";
    }

    public string GeneratePaymentQRCode(decimal amount, string courseName, string transactionId)
    {
        var upiId = _configuration["Payment:UPI:Id"] ?? "easylearn@paytm";
        var merchantName = _configuration["Payment:UPI:MerchantName"] ?? "EasyLearn";
        
        var upiData = $"upi://pay?pa={upiId}&pn={merchantName}&am={amount}&cu=INR&tn=Course Payment: {courseName}&tr={transactionId}";
        
        return GenerateQRCodeUrl(upiData);
    }

    public string GenerateVerificationUrl(string certificateNumber)
    {
        var baseUrl = _configuration["BaseUrl"] ?? "https://localhost:5001";
        return $"{baseUrl}/verify/{certificateNumber}";
    }
}