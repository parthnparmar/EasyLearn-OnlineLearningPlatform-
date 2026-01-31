using System.ComponentModel.DataAnnotations;

namespace EasyLearn.Models;

public class ReExamPayment
{
    public int Id { get; set; }
    
    [Required]
    public string StudentId { get; set; } = string.Empty;
    
    public int ExamAttemptId { get; set; }
    public int ExamId { get; set; }
    public int CourseId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string StudentName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string StudentEmail { get; set; } = string.Empty;
    
    public decimal ReExamFee { get; set; } = 200;
    
    [Required]
    public PaymentMethodType PaymentMethod { get; set; }
    
    [Required]
    [StringLength(50)]
    public string TransactionId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(20)]
    public string PaymentStatus { get; set; } = "Completed";
    
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ApplicationUser Student { get; set; } = null!;
    public ExamAttempt ExamAttempt { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
    public Course Course { get; set; } = null!;
}