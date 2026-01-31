using EasyLearn.Models;
using Microsoft.EntityFrameworkCore;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace EasyLearn.Services;

public interface ICertificatePaymentService
{
    Task<CertificatePaymentReceipt> ProcessCertificatePaymentAsync(string studentId, int courseId, PaymentMethodType paymentMethod, string studentName, string studentEmail);
    Task<byte[]> GenerateCertificateReceiptPdfAsync(CertificatePaymentReceipt receipt);
    Task<bool> HasPaidForCertificateAsync(string studentId, int courseId);
    Task<bool> HasExamAccessAsync(string studentId, int courseId);
    Task<bool> HasCertificateAccessAsync(string studentId, int courseId);
    string GenerateCertificateReceiptNumber();
}

public class CertificatePaymentService : ICertificatePaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<CertificatePaymentService> _logger;

    public CertificatePaymentService(ApplicationDbContext context, IPaymentService paymentService, ILogger<CertificatePaymentService> logger)
    {
        _context = context;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<CertificatePaymentReceipt> ProcessCertificatePaymentAsync(string studentId, int courseId, PaymentMethodType paymentMethod, string studentName, string studentEmail)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null)
            throw new ArgumentException("Course not found");

        // Check if already paid
        var existingPayment = await _context.CertificatePayments
            .FirstOrDefaultAsync(cp => cp.StudentId == studentId && cp.CourseId == courseId && cp.Status == PaymentStatus.Completed);

        if (existingPayment != null)
            throw new InvalidOperationException("Certificate payment already completed");

        const decimal certificateFee = 500m;
        const decimal examFee = 300m;
        const decimal totalAmount = certificateFee + examFee;

        // Process payment
        var paymentRequest = new PaymentRequest
        {
            Amount = totalAmount,
            StudentId = studentId,
            CourseId = courseId,
            PaymentMethod = paymentMethod,
            Currency = "INR"
        };

        var paymentResult = await _paymentService.ProcessDirectPaymentAsync(paymentRequest);

        if (!paymentResult.IsSuccess)
            throw new InvalidOperationException($"Payment failed: {paymentResult.Message}");

        // Create certificate payment record
        var certificatePayment = new CertificatePayment
        {
            StudentId = studentId,
            CourseId = courseId,
            TransactionId = paymentResult.TransactionId,
            CertificateFee = certificateFee,
            ExamFee = examFee,
            TotalAmount = totalAmount,
            PaymentMethod = paymentResult.PaymentMethod,
            Status = PaymentStatus.Completed,
            ExamAccess = true,
            CertificateAccess = true
        };

        _context.CertificatePayments.Add(certificatePayment);
        await _context.SaveChangesAsync();

        // Create receipt
        var receiptNumber = GenerateCertificateReceiptNumber();
        var receipt = new CertificatePaymentReceipt
        {
            ReceiptNumber = receiptNumber,
            TransactionId = paymentResult.TransactionId,
            StudentName = studentName,
            StudentEmail = studentEmail,
            CourseName = course.Title,
            CertificateFee = certificateFee,
            ExamFee = examFee,
            TotalAmount = totalAmount,
            PaymentMethod = paymentResult.PaymentMethod,
            StudentId = studentId,
            CourseId = courseId,
            CertificatePaymentId = certificatePayment.Id
        };

        _context.CertificatePaymentReceipts.Add(receipt);
        await _context.SaveChangesAsync();

        return receipt;
    }

    public async Task<byte[]> GenerateCertificateReceiptPdfAsync(CertificatePaymentReceipt receipt)
    {
        using var stream = new MemoryStream();
        var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 50, 50, 50, 50);
        var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, stream);
        
        document.Open();
        
        // Add watermark
        AddReceiptWatermark(writer);
        
        // Create main content with gold border
        var mainTable = new iTextSharp.text.pdf.PdfPTable(1) { WidthPercentage = 100 };
        mainTable.DefaultCell.Border = iTextSharp.text.Rectangle.BOX;
        mainTable.DefaultCell.BorderWidth = 3;
        mainTable.DefaultCell.BorderColor = new iTextSharp.text.BaseColor(184, 134, 11); // Gold border
        mainTable.DefaultCell.Padding = 25;
        
        var contentTable = new iTextSharp.text.pdf.PdfPTable(1) { WidthPercentage = 100 };
        contentTable.DefaultCell.Border = iTextSharp.text.Rectangle.NO_BORDER;
        
        // Header with logo and platform name
        AddCertificateReceiptHeader(contentTable);
        
        // Title
        var titleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 26, new iTextSharp.text.BaseColor(0, 51, 102));
        var title = new iTextSharp.text.Paragraph("CERTIFICATE PAYMENT RECEIPT", titleFont)
        {
            Alignment = iTextSharp.text.Element.ALIGN_CENTER,
            SpacingAfter = 25
        };
        contentTable.AddCell(new iTextSharp.text.pdf.PdfPCell(title) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        // Receipt information
        AddCertificateReceiptInfo(contentTable, receipt);
        
        // Student details
        AddCertificateStudentDetails(contentTable, receipt);
        
        // Payment breakdown with enhanced styling
        AddCertificatePaymentBreakdown(contentTable, receipt);
        
        // Verification and footer
        AddCertificateReceiptFooter(contentTable, receipt);
        
        mainTable.AddCell(contentTable);
        document.Add(mainTable);
        
        document.Close();
        return stream.ToArray();
    }
    
    private void AddReceiptWatermark(iTextSharp.text.pdf.PdfWriter writer)
    {
        var watermarkFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 50, new iTextSharp.text.BaseColor(245, 245, 245));
        var cb = writer.DirectContentUnder;
        
        cb.BeginText();
        cb.SetFontAndSize(watermarkFont.BaseFont, 50);
        cb.SetTextMatrix(150, 400);
        cb.ShowText("EASYLEARN");
        cb.EndText();
    }
    
    private void AddCertificateReceiptHeader(iTextSharp.text.pdf.PdfPTable table)
    {
        var headerTable = new iTextSharp.text.pdf.PdfPTable(3) { WidthPercentage = 100 };
        headerTable.SetWidths(new float[] { 1, 3, 1 });
        
        // Logo
        var logoFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 24, new iTextSharp.text.BaseColor(0, 51, 102));
        var logoCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("🎓", logoFont))
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER
        };
        
        // Platform name
        var platformFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 24, new iTextSharp.text.BaseColor(0, 51, 102));
        var platformCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("EASYLEARN", platformFont))
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER
        };
        
        // Seal
        var sealCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("🏆", logoFont))
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER
        };
        
        headerTable.AddCell(logoCell);
        headerTable.AddCell(platformCell);
        headerTable.AddCell(sealCell);
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(headerTable) { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 20 });
    }
    
    private void AddCertificateReceiptInfo(iTextSharp.text.pdf.PdfPTable table, CertificatePaymentReceipt receipt)
    {
        var sectionFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14, new iTextSharp.text.BaseColor(184, 134, 11));
        var section = new iTextSharp.text.Paragraph("RECEIPT INFORMATION", sectionFont) { SpacingAfter = 10 };
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(section) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        var detailsTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
        detailsTable.SetWidths(new float[] { 1, 2 });
        
        var labelFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 12);
        var valueFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 12);
        
        AddCertificateTableRow(detailsTable, "Receipt Number:", receipt.ReceiptNumber, labelFont, valueFont);
        AddCertificateTableRow(detailsTable, "Transaction ID:", receipt.TransactionId, labelFont, valueFont);
        AddCertificateTableRow(detailsTable, "Payment Date:", receipt.PaymentDate.ToString("MMMM dd, yyyy HH:mm:ss"), labelFont, valueFont);
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(detailsTable) { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 15 });
    }
    
    private void AddCertificateStudentDetails(iTextSharp.text.pdf.PdfPTable table, CertificatePaymentReceipt receipt)
    {
        var sectionFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14, new iTextSharp.text.BaseColor(184, 134, 11));
        var section = new iTextSharp.text.Paragraph("STUDENT & COURSE DETAILS", sectionFont) { SpacingAfter = 10 };
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(section) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        var detailsTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
        detailsTable.SetWidths(new float[] { 1, 2 });
        
        var labelFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 12);
        var valueFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 12);
        
        AddCertificateTableRow(detailsTable, "Student Name:", receipt.StudentName, labelFont, valueFont);
        AddCertificateTableRow(detailsTable, "Student Email:", receipt.StudentEmail, labelFont, valueFont);
        AddCertificateTableRow(detailsTable, "Course Name:", receipt.CourseName, labelFont, valueFont);
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(detailsTable) { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 15 });
    }
    
    private void AddCertificatePaymentBreakdown(iTextSharp.text.pdf.PdfPTable table, CertificatePaymentReceipt receipt)
    {
        var sectionFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14, new iTextSharp.text.BaseColor(184, 134, 11));
        var section = new iTextSharp.text.Paragraph("PAYMENT BREAKDOWN", sectionFont) { SpacingAfter = 10 };
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(section) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        var paymentTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
        paymentTable.SetWidths(new float[] { 2, 1 });
        
        var labelFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 12);
        var valueFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 12);
        var totalFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14, new iTextSharp.text.BaseColor(0, 128, 0));
        
        AddCertificateTableRow(paymentTable, "Certificate Fee:", $"₹{receipt.CertificateFee:F2}", labelFont, valueFont);
        AddCertificateTableRow(paymentTable, "Exam Fee:", $"₹{receipt.ExamFee:F2}", labelFont, valueFont);
        
        // Add separator line
        var separatorCell1 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(""))
        {
            Border = iTextSharp.text.Rectangle.TOP_BORDER,
            BorderWidth = 1,
            BorderColor = new iTextSharp.text.BaseColor(184, 134, 11)
        };
        var separatorCell2 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(""))
        {
            Border = iTextSharp.text.Rectangle.TOP_BORDER,
            BorderWidth = 1,
            BorderColor = new iTextSharp.text.BaseColor(184, 134, 11)
        };
        paymentTable.AddCell(separatorCell1);
        paymentTable.AddCell(separatorCell2);
        
        AddCertificateTableRow(paymentTable, "TOTAL AMOUNT:", $"₹{receipt.TotalAmount:F2}", totalFont, totalFont);
        AddCertificateTableRow(paymentTable, "Payment Method:", receipt.PaymentMethod, labelFont, valueFont);
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(paymentTable) { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 20 });
    }
    
    private void AddCertificateReceiptFooter(iTextSharp.text.pdf.PdfPTable table, CertificatePaymentReceipt receipt)
    {
        // Digital signature
        var signature = GenerateReceiptSignature(receipt.ReceiptNumber, receipt.TotalAmount, receipt.PaymentDate);
        var sigFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.COURIER, 10, new iTextSharp.text.BaseColor(128, 128, 128));
        var sigParagraph = new iTextSharp.text.Paragraph($"Digital Signature: {signature}", sigFont)
        {
            Alignment = iTextSharp.text.Element.ALIGN_CENTER,
            SpacingAfter = 15
        };
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(sigParagraph) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        // QR Code and website footer
        var footerTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
        
        var qrFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 10, new iTextSharp.text.BaseColor(128, 128, 128));
        var qrData = $"Receipt: {receipt.ReceiptNumber}|Amount: ₹{receipt.TotalAmount:F2}|Date: {receipt.PaymentDate:yyyy-MM-dd}";
        var qrText = new iTextSharp.text.Paragraph();
        qrText.Add(new iTextSharp.text.Chunk("📱 QR Code for Verification\n", qrFont));
        qrText.Add(new iTextSharp.text.Chunk($"Data: {qrData}", qrFont));
        
        var websiteText = new iTextSharp.text.Paragraph
        {
            Alignment = iTextSharp.text.Element.ALIGN_RIGHT
        };
        websiteText.Add(new iTextSharp.text.Chunk("🌐 www.easylearn.com\n", qrFont));
        websiteText.Add(new iTextSharp.text.Chunk("Thank you for choosing EasyLearn!", qrFont));
        
        footerTable.AddCell(new iTextSharp.text.pdf.PdfPCell(qrText) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        footerTable.AddCell(new iTextSharp.text.pdf.PdfPCell(websiteText) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(footerTable) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        // Final message
        var finalMsg = new iTextSharp.text.Paragraph("This is a digitally generated receipt. No physical signature required.", qrFont)
        {
            Alignment = iTextSharp.text.Element.ALIGN_CENTER,
            SpacingBefore = 10
        };
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(finalMsg) { Border = iTextSharp.text.Rectangle.NO_BORDER });
    }
    
    private void AddCertificateTableRow(iTextSharp.text.pdf.PdfPTable table, string label, string value, iTextSharp.text.Font labelFont, iTextSharp.text.Font valueFont)
    {
        var labelCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(label, labelFont))
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            PaddingBottom = 8
        };
        var valueCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(value, valueFont))
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            PaddingBottom = 8
        };
        table.AddCell(labelCell);
        table.AddCell(valueCell);
    }

    
    private string GenerateReceiptSignature(string receiptNumber, decimal amount, DateTime paymentDate)
    {
        var data = $"{receiptNumber}-{amount:F2}-{paymentDate:yyyyMMddHHmmss}-EasyLearn";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash)[..20]; // First 20 characters for display
    }

    public async Task<bool> HasPaidForCertificateAsync(string studentId, int courseId)
    {
        return await _context.CertificatePayments
            .AnyAsync(cp => cp.StudentId == studentId && cp.CourseId == courseId && cp.Status == PaymentStatus.Completed);
    }

    public async Task<bool> HasExamAccessAsync(string studentId, int courseId)
    {
        var payment = await _context.CertificatePayments
            .FirstOrDefaultAsync(cp => cp.StudentId == studentId && cp.CourseId == courseId && cp.Status == PaymentStatus.Completed);
        
        return payment?.ExamAccess == true;
    }

    public async Task<bool> HasCertificateAccessAsync(string studentId, int courseId)
    {
        var payment = await _context.CertificatePayments
            .FirstOrDefaultAsync(cp => cp.StudentId == studentId && cp.CourseId == courseId && cp.Status == PaymentStatus.Completed);
        
        return payment?.CertificateAccess == true;
    }

    public string GenerateCertificateReceiptNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = new Random().Next(1000, 9999);
        return $"CR{timestamp}{random}";
    }
}