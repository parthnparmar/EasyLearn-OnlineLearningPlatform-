using EasyLearn.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyLearn.Services;

public interface IPaymentService
{
    Task<PaymentResult> ProcessDirectPaymentAsync(PaymentRequest request);
    Task<string> CreatePaymentIntentAsync(decimal amount, PaymentMethodType paymentMethod, string currency = "INR");
    Task<bool> RefundPaymentAsync(string paymentId, decimal? amount = null);
    Task<PaymentStatus> GetPaymentStatusAsync(string paymentId);
    Task<List<PaymentMethodInfo>> GetAvailablePaymentMethodsAsync();
    Task<bool> VerifyPaymentAsync(string transactionId);
}

public class PaymentService : IPaymentService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;
    private readonly ApplicationDbContext _context;

    public PaymentService(IConfiguration configuration, ILogger<PaymentService> logger, ApplicationDbContext context)
    {
        _configuration = configuration;
        _logger = logger;
        _context = context;
    }

    public async Task<PaymentResult> ProcessDirectPaymentAsync(PaymentRequest request)
    {
        try
        {
            _logger.LogInformation($"Processing {request.PaymentMethod} payment for amount: ₹{request.Amount}");
            
            // Validate payment request
            if (request.Amount <= 0)
            {
                return new PaymentResult
                {
                    IsSuccess = false,
                    Status = PaymentStatus.Failed,
                    Message = "Invalid payment amount"
                };
            }

            // Generate transaction ID
            var transactionId = GenerateTransactionId(request.PaymentMethod);
            
            // Simulate processing delay based on payment method
            var delay = request.PaymentMethod switch
            {
                PaymentMethodType.UPI or PaymentMethodType.GooglePay or PaymentMethodType.PhonePe or PaymentMethodType.Paytm => 2000,
                PaymentMethodType.CreditCard or PaymentMethodType.DebitCard => 3000,
                PaymentMethodType.QRCode => 1500,
                _ => 1000
            };
            
            await Task.Delay(delay);
            
            // Enhanced payment processing logic
            var isSuccess = await ProcessPaymentByMethod(request, transactionId);
            
            var result = new PaymentResult
            {
                IsSuccess = isSuccess,
                TransactionId = isSuccess ? transactionId : string.Empty,
                Amount = request.Amount,
                Currency = request.Currency,
                Status = isSuccess ? PaymentStatus.Completed : PaymentStatus.Failed,
                Message = isSuccess ? $"Payment processed successfully via {GetPaymentMethodDisplayName(request.PaymentMethod)}" : "Payment failed. Please try again.",
                PaymentMethod = request.PaymentMethod.ToString()
            };

            // Store payment transaction
            await StorePaymentTransactionAsync(request, result);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment");
            return new PaymentResult
            {
                IsSuccess = false,
                Status = PaymentStatus.Failed,
                Message = "Payment processing failed"
            };
        }
    }

    private async Task<bool> ProcessPaymentByMethod(PaymentRequest request, string transactionId)
    {
        return request.PaymentMethod switch
        {
            PaymentMethodType.CreditCard or PaymentMethodType.DebitCard => await ProcessCardPayment(request, transactionId),
            PaymentMethodType.UPI => await ProcessUPIPayment(request, transactionId),
            PaymentMethodType.QRCode => await ProcessQRPayment(request, transactionId),
            PaymentMethodType.GooglePay or PaymentMethodType.PhonePe or PaymentMethodType.Paytm => await ProcessWalletPayment(request, transactionId),
            _ => false
        };
    }

    private async Task<bool> ProcessCardPayment(PaymentRequest request, string transactionId)
    {
        await Task.Delay(100);
        var random = new Random();
        return random.NextDouble() > 0.05 && request.Amount <= 100000;
    }

    private async Task<bool> ProcessUPIPayment(PaymentRequest request, string transactionId)
    {
        await Task.Delay(100);
        var random = new Random();
        return random.NextDouble() > 0.02;
    }

    private async Task<bool> ProcessQRPayment(PaymentRequest request, string transactionId)
    {
        await Task.Delay(100);
        var random = new Random();
        return random.NextDouble() > 0.03;
    }

    private async Task<bool> ProcessWalletPayment(PaymentRequest request, string transactionId)
    {
        await Task.Delay(100);
        var random = new Random();
        return random.NextDouble() > 0.01;
    }

    private async Task StorePaymentTransactionAsync(PaymentRequest request, PaymentResult result)
    {
        var transaction = new PaymentTransaction
        {
            TransactionId = result.TransactionId,
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            Amount = request.Amount,
            Currency = request.Currency,
            Status = result.Status,
            PaymentMethod = request.PaymentMethod.ToString(),
            GatewayResponse = result.Message,
            CompletedAt = result.IsSuccess ? DateTime.UtcNow : null
        };

        _context.PaymentTransactions.Add(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> VerifyPaymentAsync(string transactionId)
    {
        var transaction = await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId);
        
        return transaction?.Status == PaymentStatus.Completed;
    }

    private string GenerateTransactionId(PaymentMethodType paymentMethod)
    {
        var prefix = paymentMethod switch
        {
            PaymentMethodType.CreditCard => "CC",
            PaymentMethodType.DebitCard => "DC",
            PaymentMethodType.UPI => "UPI",
            PaymentMethodType.QRCode => "QR",
            PaymentMethodType.GooglePay => "GPY",
            PaymentMethodType.PhonePe => "PPE",
            PaymentMethodType.Paytm => "PTM",
            _ => "PAY"
        };
        
        return $"{prefix}{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }

    public async Task<string> CreatePaymentIntentAsync(decimal amount, PaymentMethodType paymentMethod, string currency = "INR")
    {
        try
        {
            // TODO: Implement Stripe Payment Intent creation
            // Example Stripe integration:
            /*
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Stripe uses cents
                Currency = currency.ToLower(),
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                },
            };
            
            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options);
            return paymentIntent.ClientSecret;
            */
            
            await Task.Delay(500); // Simulate API call
            return $"pi_{paymentMethod.ToString().ToLower()}_{Guid.NewGuid().ToString("N")[..16]}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment intent");
            throw;
        }
    }

    public async Task<bool> RefundPaymentAsync(string paymentId, decimal? amount = null)
    {
        try
        {
            _logger.LogInformation($"Processing refund for payment: {paymentId}");
            
            var transaction = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.TransactionId == paymentId);
            
            if (transaction == null || transaction.Status != PaymentStatus.Completed)
                return false;
            
            transaction.Status = PaymentStatus.Refunded;
            await _context.SaveChangesAsync();
            
            await Task.Delay(500);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund");
            return false;
        }
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(string paymentId)
    {
        try
        {
            var transaction = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.TransactionId == paymentId);
            
            return transaction?.Status ?? PaymentStatus.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment status");
            return PaymentStatus.Failed;
        }
    }

    public async Task<List<PaymentMethodInfo>> GetAvailablePaymentMethodsAsync()
    {
        await Task.Delay(100); // Simulate API call
        return PaymentMethods.AvailableMethods.Where(m => m.IsEnabled).ToList();
    }

    private string GetPaymentMethodDisplayName(PaymentMethodType paymentMethod)
    {
        return PaymentMethods.AvailableMethods.FirstOrDefault(m => m.Type == paymentMethod)?.DisplayName ?? paymentMethod.ToString();
    }
}

// Payment Models
public class PaymentRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string StudentId { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public PaymentMethodType PaymentMethod { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class PaymentResult
{
    public bool IsSuccess { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public enum PaymentStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    Refunded = 6
}