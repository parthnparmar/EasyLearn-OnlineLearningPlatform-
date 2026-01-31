using System.ComponentModel.DataAnnotations;

namespace EasyLearn.Models;

public class ExamVerification
{
    public int Id { get; set; }
    
    [Required]
    public string StudentId { get; set; } = string.Empty;
    
    [Required]
    public int ExamId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string EnrollmentNumber { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;
    
    public bool CaptchaVerified { get; set; } = false;
    public string CaptchaAnswer { get; set; } = string.Empty;
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
    public bool IsVerified { get; set; } = false;
    
    // Navigation properties
    public ApplicationUser Student { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
}

public class PreExamVerificationViewModel
{
    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
    public string FullName { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Enrollment number is required")]
    [StringLength(50, ErrorMessage = "Enrollment number cannot exceed 50 characters")]
    public string EnrollmentNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Please enter a valid phone number")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Please solve the CAPTCHA")]
    public int CaptchaAnswer { get; set; }
    
    public int ExamId { get; set; }
    public Exam? Exam { get; set; }
    public string CaptchaQuestion { get; set; } = string.Empty;
    public int CaptchaCorrectAnswer { get; set; }
    
    [Required(ErrorMessage = "You must agree to the terms and conditions")]
    public bool AgreeToTerms { get; set; } = false;
}

public class MissedExamRequestViewModel
{
    public int ExamId { get; set; }
    public Exam? Exam { get; set; }
    
    [Required(ErrorMessage = "Please provide a reason for missing the exam")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string Reason { get; set; } = string.Empty;
}