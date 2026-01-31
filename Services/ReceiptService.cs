using EasyLearn.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyLearn.Services;

public interface IReceiptService
{
    Task<PaymentReceipt> CreateReceiptAsync(string studentId, int courseId, string transactionId, decimal amount, string paymentMethod, string studentName, string studentEmail, string studentPhone);
    Task<byte[]> GenerateReceiptPdfAsync(PaymentReceipt receipt);
    string GenerateEnrollmentNumber();
    string GenerateReceiptNumber();
}

public class ReceiptService : IReceiptService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReceiptService> _logger;

    public ReceiptService(ApplicationDbContext context, ILogger<ReceiptService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentReceipt> CreateReceiptAsync(string studentId, int courseId, string transactionId, decimal amount, string paymentMethod, string studentName, string studentEmail, string studentPhone)
    {
        var course = await _context.Courses
            .Include(c => c.Instructor)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null)
            throw new ArgumentException("Course not found");

        var enrollmentNumber = GenerateEnrollmentNumber();
        var receiptNumber = GenerateReceiptNumber();

        var receipt = new PaymentReceipt
        {
            ReceiptNumber = receiptNumber,
            TransactionId = transactionId,
            EnrollmentNumber = enrollmentNumber,
            StudentName = studentName,
            StudentEmail = studentEmail,
            StudentPhone = studentPhone,
            CourseName = course.Title,
            InstructorName = $"{course.Instructor.FirstName} {course.Instructor.LastName}",
            Amount = amount,
            PaymentMethod = paymentMethod,
            StudentId = studentId,
            CourseId = courseId
        };

        _context.PaymentReceipts.Add(receipt);
        await _context.SaveChangesAsync();

        return receipt;
    }

    public async Task<byte[]> GenerateReceiptPdfAsync(PaymentReceipt receipt)
    {
        using var stream = new MemoryStream();
        var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 50, 50, 50, 50);
        var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, stream);
        
        document.Open();
        
        // Add watermark
        AddReceiptWatermark(writer);
        
        // Create main content with border
        var mainTable = new iTextSharp.text.pdf.PdfPTable(1) { WidthPercentage = 100 };
        mainTable.DefaultCell.Border = iTextSharp.text.Rectangle.BOX;
        mainTable.DefaultCell.BorderWidth = 2;
        mainTable.DefaultCell.BorderColor = new iTextSharp.text.BaseColor(0, 51, 102); // Blue border
        mainTable.DefaultCell.Padding = 20;
        
        var contentTable = new iTextSharp.text.pdf.PdfPTable(1) { WidthPercentage = 100 };
        contentTable.DefaultCell.Border = iTextSharp.text.Rectangle.NO_BORDER;
        
        // Header with logo and platform name
        AddReceiptHeader(contentTable);
        
        // Title
        var titleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 24, new iTextSharp.text.BaseColor(0, 51, 102));
        var title = new iTextSharp.text.Paragraph("PAYMENT RECEIPT", titleFont)
        {
            Alignment = iTextSharp.text.Element.ALIGN_CENTER,
            SpacingAfter = 20
        };
        contentTable.AddCell(new iTextSharp.text.pdf.PdfPCell(title) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        // Receipt details
        AddReceiptDetails(contentTable, receipt);
        
        // Student details
        AddStudentDetails(contentTable, receipt);
        
        // Course details
        AddCourseDetails(contentTable, receipt);
        
        // Payment details
        AddPaymentDetails(contentTable, receipt);
        
        // Footer with QR code and website
        AddReceiptFooter(contentTable, receipt);
        
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
    
    private void AddReceiptHeader(iTextSharp.text.pdf.PdfPTable table)
    {
        var headerTable = new iTextSharp.text.pdf.PdfPTable(3) { WidthPercentage = 100 };
        headerTable.SetWidths(new float[] { 1, 3, 1 });
        
        // Logo
        var logoFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 20, new iTextSharp.text.BaseColor(0, 51, 102));
        var logoCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("🎓", logoFont))
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER
        };
        
        // Platform name
        var platformFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 22, new iTextSharp.text.BaseColor(0, 51, 102));
        var platformCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("EASYLEARN", platformFont))
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER
        };
        
        // Seal
        var sealCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("⭐", logoFont))
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER
        };
        
        headerTable.AddCell(logoCell);
        headerTable.AddCell(platformCell);
        headerTable.AddCell(sealCell);
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(headerTable) { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 15 });
    }
    
    private void AddReceiptDetails(iTextSharp.text.pdf.PdfPTable table, PaymentReceipt receipt)
    {
        var sectionFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14, new iTextSharp.text.BaseColor(184, 134, 11));
        var section = new iTextSharp.text.Paragraph("RECEIPT INFORMATION", sectionFont) { SpacingAfter = 10 };
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(section) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        var detailsTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
        detailsTable.SetWidths(new float[] { 1, 2 });
        
        var labelFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 12);
        var valueFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 12);
        
        AddReceiptTableRow(detailsTable, "Receipt Number:", receipt.ReceiptNumber, labelFont, valueFont);
        AddReceiptTableRow(detailsTable, "Transaction ID:", receipt.TransactionId, labelFont, valueFont);
        AddReceiptTableRow(detailsTable, "Enrollment Number:", receipt.EnrollmentNumber, labelFont, valueFont);
        AddReceiptTableRow(detailsTable, "Payment Date:", receipt.PaymentDate.ToString("MMMM dd, yyyy HH:mm:ss"), labelFont, valueFont);
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(detailsTable) { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 15 });
    }
    
    private void AddStudentDetails(iTextSharp.text.pdf.PdfPTable table, PaymentReceipt receipt)
    {
        var sectionFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14, new iTextSharp.text.BaseColor(184, 134, 11));
        var section = new iTextSharp.text.Paragraph("STUDENT DETAILS", sectionFont) { SpacingAfter = 10 };
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(section) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        var detailsTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
        detailsTable.SetWidths(new float[] { 1, 2 });
        
        var labelFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 12);
        var valueFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 12);
        
        AddReceiptTableRow(detailsTable, "Name:", receipt.StudentName, labelFont, valueFont);
        AddReceiptTableRow(detailsTable, "Email:", receipt.StudentEmail, labelFont, valueFont);
        AddReceiptTableRow(detailsTable, "Phone:", receipt.StudentPhone, labelFont, valueFont);
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(detailsTable) { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 15 });
    }
    
    private void AddCourseDetails(iTextSharp.text.pdf.PdfPTable table, PaymentReceipt receipt)
    {
        var sectionFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14, new iTextSharp.text.BaseColor(184, 134, 11));
        var section = new iTextSharp.text.Paragraph("COURSE DETAILS", sectionFont) { SpacingAfter = 10 };
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(section) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        var detailsTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
        detailsTable.SetWidths(new float[] { 1, 2 });
        
        var labelFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 12);
        var valueFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 12);
        
        AddReceiptTableRow(detailsTable, "Course Name:", receipt.CourseName, labelFont, valueFont);
        AddReceiptTableRow(detailsTable, "Instructor:", receipt.InstructorName, labelFont, valueFont);
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(detailsTable) { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 15 });
    }
    
    private void AddPaymentDetails(iTextSharp.text.pdf.PdfPTable table, PaymentReceipt receipt)
    {
        var sectionFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14, new iTextSharp.text.BaseColor(184, 134, 11));
        var section = new iTextSharp.text.Paragraph("PAYMENT DETAILS", sectionFont) { SpacingAfter = 10 };
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(section) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        var detailsTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
        detailsTable.SetWidths(new float[] { 1, 2 });
        
        var labelFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 12);
        var valueFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 12);
        var amountFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14, new iTextSharp.text.BaseColor(0, 128, 0));
        
        AddReceiptTableRow(detailsTable, "Amount:", $"{receipt.Currency} {receipt.Amount:F2}", labelFont, amountFont);
        AddReceiptTableRow(detailsTable, "Payment Method:", receipt.PaymentMethod, labelFont, valueFont);
        AddReceiptTableRow(detailsTable, "Status:", receipt.Status, labelFont, valueFont);
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(detailsTable) { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 20 });
    }
    
    private void AddReceiptFooter(iTextSharp.text.pdf.PdfPTable table, PaymentReceipt receipt)
    {
        var footerTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
        
        // QR Code section
        var qrFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 10, new iTextSharp.text.BaseColor(128, 128, 128));
        var qrText = new iTextSharp.text.Paragraph();
        qrText.Add(new iTextSharp.text.Chunk("📱 QR Code for Verification\n", qrFont));
        qrText.Add(new iTextSharp.text.Chunk($"Receipt: {receipt.ReceiptNumber}\n", qrFont));
        qrText.Add(new iTextSharp.text.Chunk($"Amount: {receipt.Currency} {receipt.Amount:F2}", qrFont));
        
        // Website section
        var websiteText = new iTextSharp.text.Paragraph
        {
            Alignment = iTextSharp.text.Element.ALIGN_RIGHT
        };
        websiteText.Add(new iTextSharp.text.Chunk("🌐 www.easylearn.com\n", qrFont));
        websiteText.Add(new iTextSharp.text.Chunk("Thank you for choosing EasyLearn!", qrFont));
        
        footerTable.AddCell(new iTextSharp.text.pdf.PdfPCell(qrText) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        footerTable.AddCell(new iTextSharp.text.pdf.PdfPCell(websiteText) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        table.AddCell(new iTextSharp.text.pdf.PdfPCell(footerTable) { Border = iTextSharp.text.Rectangle.NO_BORDER });
    }
    
    private void AddReceiptTableRow(iTextSharp.text.pdf.PdfPTable table, string label, string value, iTextSharp.text.Font labelFont, iTextSharp.text.Font valueFont)
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

    public string GenerateEnrollmentNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = new Random().Next(1000, 9999);
        return $"ER{timestamp}{random}";
    }

    public string GenerateReceiptNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = new Random().Next(1000, 9999);
        return $"RC{timestamp}{random}";
    }
}