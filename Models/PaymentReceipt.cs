using System.ComponentModel.DataAnnotations;

namespace EasyLearn.Models;

public class PaymentReceipt
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string ReceiptNumber { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string TransactionId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string EnrollmentNumber { get; set; } = string.Empty;
    
    [Required]
    public string StudentId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string StudentName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string StudentEmail { get; set; } = string.Empty;
    
    [StringLength(20)]
    public string StudentPhone { get; set; } = string.Empty;
    
    public int CourseId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string CourseName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string InstructorName { get; set; } = string.Empty;
    
    public decimal Amount { get; set; }
    
    [StringLength(10)]
    public string Currency { get; set; } = "INR";
    
    [Required]
    [StringLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;
    
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;
    
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ApplicationUser Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}