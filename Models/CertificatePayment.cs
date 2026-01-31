using System.ComponentModel.DataAnnotations;
using EasyLearn.Services;

namespace EasyLearn.Models;

public class CertificatePayment
{
    public int Id { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public decimal CertificateFee { get; set; }
    public decimal ExamFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public bool ExamAccess { get; set; } = false;
    public bool CertificateAccess { get; set; } = false;
    
    // Foreign keys
    public string StudentId { get; set; } = string.Empty;
    public int CourseId { get; set; }
    
    // Navigation properties
    public ApplicationUser Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}

public class CertificatePaymentReceipt
{
    public int Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public decimal CertificateFee { get; set; }
    public decimal ExamFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    
    // Foreign keys
    public string StudentId { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public int CertificatePaymentId { get; set; }
    
    // Navigation properties
    public ApplicationUser Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public CertificatePayment CertificatePayment { get; set; } = null!;
}