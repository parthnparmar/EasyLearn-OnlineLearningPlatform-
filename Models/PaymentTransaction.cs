using System.ComponentModel.DataAnnotations;
using EasyLearn.Services;

namespace EasyLearn.Models;

public class PaymentTransaction
{
    public int Id { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentStatus Status { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string GatewayResponse { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    // Navigation properties
    public ApplicationUser Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}