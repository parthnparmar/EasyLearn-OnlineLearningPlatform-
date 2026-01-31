using EasyLearn.Models;
using Microsoft.EntityFrameworkCore;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Security.Cryptography;

namespace EasyLearn.Services;

public interface ICertificateService
{
    Task<Certificate> GenerateCertificateAsync(string studentId, int courseId);
    Task<byte[]> GeneratePdfCertificateAsync(Certificate certificate);
    Task<byte[]> GenerateCertificatePdfAsync(string studentId, int courseId);
    Task<string> GenerateCertificateNumberAsync(string studentId, int courseId);
    string GenerateVerificationUrl(string certificateNumber);
    Task<string> GenerateExamCertificateAsync(string studentName, string courseTitle, double percentage, string certificateNumber, string instructorName);
}

public class CertificateService : ICertificateService
{
    private readonly ApplicationDbContext _context;
    private readonly ICertificatePaymentService _certificatePaymentService;
    private readonly IQRCodeService _qrCodeService;
    private readonly IConfiguration _configuration;

    public CertificateService(ApplicationDbContext context, ICertificatePaymentService certificatePaymentService, IQRCodeService qrCodeService, IConfiguration configuration)
    {
        _context = context;
        _certificatePaymentService = certificatePaymentService;
        _qrCodeService = qrCodeService;
        _configuration = configuration;
    }

    public async Task<byte[]> GenerateCertificatePdfAsync(string studentId, int courseId)
    {
        var student = await _context.Users.FindAsync(studentId);
        var course = await _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Lessons)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (student == null || course == null)
            throw new ArgumentException("Student or course not found");

        var certificateNumber = await GenerateCertificateNumberAsync(studentId, courseId);
        var completionDate = DateTime.UtcNow;
        var validUntil = completionDate.AddYears(1);
        var courseDuration = CalculateCourseDuration(course);

        using var stream = new MemoryStream();
        var document = new Document(PageSize.A4, 30, 30, 30, 30);
        var writer = PdfWriter.GetInstance(document, stream);
        
        document.Open();

        // Create certificate border
        var borderTable = new PdfPTable(1) { WidthPercentage = 100 };
        borderTable.DefaultCell.Border = iTextSharp.text.Rectangle.BOX;
        borderTable.DefaultCell.BorderWidth = 6;
        borderTable.DefaultCell.BorderColor = new BaseColor(44, 90, 160);
        borderTable.DefaultCell.Padding = 15;

        var innerTable = new PdfPTable(1) { WidthPercentage = 100 };
        innerTable.DefaultCell.Border = iTextSharp.text.Rectangle.BOX;
        innerTable.DefaultCell.BorderWidth = 2;
        innerTable.DefaultCell.BorderColor = new BaseColor(212, 175, 55);
        innerTable.DefaultCell.Padding = 25;

        // Header
        var headerFont = FontFactory.GetFont(FontFactory.TIMES_BOLD, 32, new BaseColor(44, 90, 160));
        var header = new Paragraph("CERTIFICATE", headerFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 5 };
        var subHeader = new Paragraph("OF COMPLETION", FontFactory.GetFont(FontFactory.TIMES_ROMAN, 14, new BaseColor(102, 102, 102))) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 20 };
        
        innerTable.AddCell(new PdfPCell(header) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        innerTable.AddCell(new PdfPCell(subHeader) { Border = iTextSharp.text.Rectangle.NO_BORDER });

        // Body
        var bodyFont = FontFactory.GetFont(FontFactory.TIMES_ROMAN, 14, new BaseColor(51, 51, 51));
        var nameFont = FontFactory.GetFont(FontFactory.TIMES_BOLD, 24, new BaseColor(44, 90, 160));
        var courseFont = FontFactory.GetFont(FontFactory.TIMES_BOLDITALIC, 18, new BaseColor(212, 175, 55));

        var bodyText = new Paragraph { Alignment = Element.ALIGN_CENTER, SpacingAfter = 15 };
        bodyText.Add(new Chunk("This is to certify that\n\n", bodyFont));
        bodyText.Add(new Chunk($"{student.FirstName} {student.LastName}\n\n", nameFont));
        bodyText.Add(new Chunk("has successfully completed the course\n\n", bodyFont));
        bodyText.Add(new Chunk($"{course.Title}\n\n", courseFont));
        bodyText.Add(new Chunk($"Completed on {completionDate:MMMM dd, yyyy}", bodyFont));
        
        innerTable.AddCell(new PdfPCell(bodyText) { Border = iTextSharp.text.Rectangle.NO_BORDER });

        // Validity Information
        var validityFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(220, 20, 60));
        var validityText = new Paragraph($"Valid Until: {validUntil:dd/MM/yyyy}", validityFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 20 };
        innerTable.AddCell(new PdfPCell(validityText) { Border = iTextSharp.text.Rectangle.NO_BORDER });

        // Footer with signatures and dummy stamp
        var footerTable = new PdfPTable(3) { WidthPercentage = 100, SpacingBefore = 20 };
        var signatureFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
        
        var instructor = new Paragraph { Alignment = Element.ALIGN_CENTER };
        instructor.Add(new Chunk("_________________\n", signatureFont));
        instructor.Add(new Chunk($"{course.Instructor.FirstName} {course.Instructor.LastName}\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
        instructor.Add(new Chunk("Instructor", signatureFont));
        
        // Dummy Stamp/Seal
        var stamp = new Paragraph { Alignment = Element.ALIGN_CENTER };
        stamp.Add(new Chunk("⭐", FontFactory.GetFont(FontFactory.HELVETICA, 30, new BaseColor(212, 175, 55))));
        stamp.Add(new Chunk("\n[OFFICIAL SEAL]\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, new BaseColor(44, 90, 160))));
        stamp.Add(new Chunk("EasyLearn\nPlatform", FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(102, 102, 102))));
        
        var director = new Paragraph { Alignment = Element.ALIGN_CENTER };
        director.Add(new Chunk("_________________\n", signatureFont));
        director.Add(new Chunk("Director\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
        director.Add(new Chunk("EasyLearn Platform", signatureFont));
        
        footerTable.AddCell(new PdfPCell(instructor) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        footerTable.AddCell(new PdfPCell(stamp) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        footerTable.AddCell(new PdfPCell(director) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        innerTable.AddCell(new PdfPCell(footerTable) { Border = iTextSharp.text.Rectangle.NO_BORDER });

        // Certificate ID
        var certId = new Paragraph($"Certificate ID: {certificateNumber}", FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(153, 153, 153))) { Alignment = Element.ALIGN_RIGHT, SpacingBefore = 10 };
        innerTable.AddCell(new PdfPCell(certId) { Border = iTextSharp.text.Rectangle.NO_BORDER });

        borderTable.AddCell(innerTable);
        document.Add(borderTable);

        document.Close();
        return stream.ToArray();
    }

    private void AddWatermark(PdfWriter writer, Document document)
    {
        // Remove watermark for cleaner certificate
    }

    private void AddCertificateHeader(PdfPTable table)
    {
        var headerTable = new PdfPTable(3) { WidthPercentage = 100 };
        headerTable.SetWidths(new float[] { 1, 3, 1 });

        // Enhanced logo with better styling
        var logoFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 32, new BaseColor(26, 54, 93));
        var logoCell = new PdfPCell(new Phrase("🎓", logoFont))
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_CENTER,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            BackgroundColor = new BaseColor(248, 249, 250),
            Padding = 15
        };

        // Enhanced platform name with gradient effect simulation
        var platformFont = FontFactory.GetFont(FontFactory.TIMES_BOLD, 32, new BaseColor(26, 54, 93));
        var platformPhrase = new Phrase("EASYLEARN", platformFont);
        var platformCell = new PdfPCell(platformPhrase)
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_CENTER,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            BackgroundColor = new BaseColor(212, 175, 55, 50), // Light gold background
            Padding = 15
        };

        // Enhanced seal
        var sealCell = new PdfPCell(new Phrase("🏆", logoFont))
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_CENTER,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            BackgroundColor = new BaseColor(248, 249, 250),
            Padding = 15
        };

        headerTable.AddCell(logoCell);
        headerTable.AddCell(platformCell);
        headerTable.AddCell(sealCell);

        // Add the header with enhanced styling
        var headerCell = new PdfPCell(headerTable) 
        { 
            Border = iTextSharp.text.Rectangle.NO_BORDER, 
            PaddingBottom = 25,
            BackgroundColor = new BaseColor(255, 255, 255)
        };
        
        table.AddCell(headerCell);
    }

    private void AddCertificateBody(PdfPTable table, ApplicationUser student, Course course, DateTime completionDate, string courseDuration)
    {
        var bodyFont = FontFactory.GetFont(FontFactory.TIMES_ROMAN, 20, new BaseColor(45, 55, 72));
        var nameFont = FontFactory.GetFont(FontFactory.TIMES_BOLD, 28, new BaseColor(26, 54, 93));
        var courseFont = FontFactory.GetFont(FontFactory.TIMES_BOLDITALIC, 22, new BaseColor(212, 175, 55));
        var dateFont = FontFactory.GetFont(FontFactory.TIMES_BOLD, 18, new BaseColor(26, 54, 93));

        var bodyText = new Paragraph
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingAfter = 35
        };

        bodyText.Add(new Chunk("This is to certify that\n\n", bodyFont));
        
        // Enhanced student name with background
        var nameChunk = new Chunk($"{student.FirstName} {student.LastName}\n\n", nameFont);
        nameChunk.SetBackground(new BaseColor(248, 249, 250), 10, 5, 10, 5);
        bodyText.Add(nameChunk);
        
        bodyText.Add(new Chunk("has successfully completed the course\n\n", bodyFont));
        
        // Enhanced course name
        var courseChunk = new Chunk($"\"{course.Title}\"\n\n", courseFont);
        bodyText.Add(courseChunk);
        
        // Enhanced completion date
        bodyText.Add(new Chunk("📅 Completed on ", bodyFont));
        bodyText.Add(new Chunk($"{completionDate:MMMM dd, yyyy}", dateFont));

        // Add body with enhanced background
        var bodyCell = new PdfPCell(bodyText) 
        { 
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            BackgroundColor = new BaseColor(255, 255, 255, 230),
            Padding = 25
        };
        
        table.AddCell(bodyCell);
    }

    private void AddCertificateDetails(PdfPTable table, string certificateNumber, string courseDuration)
    {
        var detailsTable = new PdfPTable(2) { WidthPercentage = 85, HorizontalAlignment = Element.ALIGN_CENTER };
        var detailFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 13, new BaseColor(64, 64, 64));
        var iconFont = FontFactory.GetFont(FontFactory.HELVETICA, 14, new BaseColor(212, 175, 55));

        // Enhanced certificate ID
        var certIdPhrase = new Phrase();
        certIdPhrase.Add(new Chunk("📋 ", iconFont));
        certIdPhrase.Add(new Chunk($"Certificate ID: {certificateNumber}", detailFont));
        
        var certIdCell = new PdfPCell(certIdPhrase)
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_LEFT,
            PaddingTop = 25,
            BackgroundColor = new BaseColor(255, 255, 255, 200),
            Padding = 15
        };

        // Enhanced duration
        var durationPhrase = new Phrase();
        durationPhrase.Add(new Chunk("⏱️ ", iconFont));
        durationPhrase.Add(new Chunk($"Course Duration: {courseDuration}", detailFont));
        
        var durationCell = new PdfPCell(durationPhrase)
        {
            Border = iTextSharp.text.Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            PaddingTop = 25,
            BackgroundColor = new BaseColor(255, 255, 255, 200),
            Padding = 15
        };

        detailsTable.AddCell(certIdCell);
        detailsTable.AddCell(durationCell);

        var detailsCell = new PdfPCell(detailsTable) 
        { 
            Border = iTextSharp.text.Rectangle.BOX,
            BorderColor = new BaseColor(212, 175, 55, 100),
            BorderWidth = 1,
            Padding = 10
        };
        
        table.AddCell(detailsCell);
    }

    private void AddCertificateFooter(PdfPTable table, Course course)
    {
        var footerTable = new PdfPTable(2) { WidthPercentage = 100 };
        var signatureFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, new BaseColor(0, 0, 0));
        var nameFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, new BaseColor(26, 54, 93));
        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, new BaseColor(102, 102, 102));

        // Enhanced instructor signature
        var instructorSignature = new Paragraph
        {
            Alignment = Element.ALIGN_CENTER
        };
        instructorSignature.Add(new Chunk("👩🏫\n", FontFactory.GetFont(FontFactory.HELVETICA, 28)));
        instructorSignature.Add(new Chunk("_________________________\n", signatureFont));
        instructorSignature.Add(new Chunk($"{course.Instructor.FirstName} {course.Instructor.LastName}\n", nameFont));
        instructorSignature.Add(new Chunk("Course Instructor", titleFont));

        // Enhanced platform seal
        var platformSeal = new Paragraph
        {
            Alignment = Element.ALIGN_CENTER
        };
        platformSeal.Add(new Chunk("🏆\n", FontFactory.GetFont(FontFactory.HELVETICA, 32)));
        platformSeal.Add(new Chunk("_________________________\n", signatureFont));
        platformSeal.Add(new Chunk("EasyLearn Platform\n", nameFont));
        platformSeal.Add(new Chunk("Official Certification", titleFont));

        // Enhanced signature cells with background
        var instructorCell = new PdfPCell(instructorSignature) 
        { 
            Border = iTextSharp.text.Rectangle.NO_BORDER, 
            PaddingTop = 40,
            BackgroundColor = new BaseColor(255, 255, 255, 180),
            Padding = 20
        };
        
        var sealCell = new PdfPCell(platformSeal) 
        { 
            Border = iTextSharp.text.Rectangle.NO_BORDER, 
            PaddingTop = 40,
            BackgroundColor = new BaseColor(255, 255, 255, 180),
            Padding = 20
        };

        footerTable.AddCell(instructorCell);
        footerTable.AddCell(sealCell);

        // Add footer with border
        var footerCell = new PdfPCell(footerTable) 
        { 
            Border = iTextSharp.text.Rectangle.TOP_BORDER,
            BorderColorTop = new BaseColor(212, 175, 55),
            BorderWidthTop = 2,
            PaddingTop = 20
        };
        
        table.AddCell(footerCell);
    }

    private void AddCertificateQRCode(PdfPTable table, string certificateNumber)
    {
        var qrTable = new PdfPTable(2) { WidthPercentage = 100 };
        var qrFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, new BaseColor(102, 102, 102));
        var urlFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, new BaseColor(128, 128, 128));

        var verificationUrl = GenerateVerificationUrl(certificateNumber);
        
        // Enhanced QR section
        var qrText = new Paragraph
        {
            Alignment = Element.ALIGN_LEFT
        };
        qrText.Add(new Chunk("📱 Digital Verification\n", qrFont));
        qrText.Add(new Chunk("Scan QR code or visit:\n", urlFont));
        qrText.Add(new Chunk(verificationUrl, urlFont));

        // Enhanced website section
        var websiteText = new Paragraph
        {
            Alignment = Element.ALIGN_RIGHT
        };
        websiteText.Add(new Chunk("🌐 www.easylearn.com\n", qrFont));
        websiteText.Add(new Chunk("\"Making Education Accessible\"\n", urlFont));
        websiteText.Add(new Chunk("© 2024 EasyLearn Platform", urlFont));

        // Enhanced cells with background
        var qrCell = new PdfPCell(qrText) 
        { 
            Border = iTextSharp.text.Rectangle.NO_BORDER, 
            PaddingTop = 35,
            BackgroundColor = new BaseColor(212, 175, 55, 20),
            Padding = 15
        };
        
        var websiteCell = new PdfPCell(websiteText) 
        { 
            Border = iTextSharp.text.Rectangle.NO_BORDER, 
            PaddingTop = 35,
            BackgroundColor = new BaseColor(212, 175, 55, 20),
            Padding = 15
        };

        qrTable.AddCell(qrCell);
        qrTable.AddCell(websiteCell);

        table.AddCell(new PdfPCell(qrTable) { Border = iTextSharp.text.Rectangle.NO_BORDER });
    }

    private string CalculateCourseDuration(Course course)
    {
        var totalLessons = course.Lessons?.Count ?? 0;
        var estimatedHours = totalLessons * 1.5;
        return $"{estimatedHours:F0} Hours";
    }

    public async Task<string> GenerateCertificateNumberAsync(string studentId, int courseId)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var hash = ComputeHash($"{studentId}-{courseId}-{timestamp}");
        return $"EL-CERT-{timestamp}-{hash[..6].ToUpper()}";
    }

    public string GenerateVerificationUrl(string certificateNumber)
    {
        var baseUrl = _configuration["BaseUrl"] ?? "https://localhost:5001";
        return $"{baseUrl}/Verification/VerifyCertificate?number={certificateNumber}";
    }

    private string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }

    public async Task<Certificate> GenerateCertificateAsync(string studentId, int courseId)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .ThenInclude(c => c.Instructor)
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == courseId);

        if (enrollment == null)
            throw new InvalidOperationException("Student not enrolled in course");
            
        var totalLessons = await _context.Lessons.CountAsync(l => l.CourseId == courseId && l.IsActive);
        var completedLessons = await _context.LessonProgresses
            .CountAsync(lp => lp.StudentId == studentId && 
                             lp.Lesson.CourseId == courseId && 
                             lp.IsCompleted);
                             
        if (totalLessons == 0 || completedLessons < totalLessons)
            throw new InvalidOperationException("Course not completed");

        var existingCertificate = await _context.Certificates
            .FirstOrDefaultAsync(c => c.StudentId == studentId && c.CourseId == courseId);

        if (existingCertificate != null)
            return existingCertificate;

        var certificateNumber = await GenerateCertificateNumberAsync(studentId, courseId);
        
        // Generate PDF and save to file
        var pdfBytes = await GenerateCertificatePdfAsync(studentId, courseId);
        var fileName = $"certificate_{certificateNumber}.pdf";
        var filePath = Path.Combine("certificates", fileName);
        var fullPath = Path.Combine("wwwroot", filePath);
        
        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        
        // Save PDF to file
        await File.WriteAllBytesAsync(fullPath, pdfBytes);

        var certificate = new Certificate
        {
            StudentId = studentId,
            CourseId = courseId,
            CertificateNumber = certificateNumber,
            IssuedAt = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddYears(1),
            FilePath = "/" + filePath.Replace("\\", "/")
        };

        _context.Certificates.Add(certificate);
        await _context.SaveChangesAsync();

        return certificate;
    }

    public async Task<byte[]> GeneratePdfCertificateAsync(Certificate certificate)
    {
        // If file exists, read it
        if (!string.IsNullOrEmpty(certificate.FilePath))
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", certificate.FilePath.TrimStart('/'));
            if (File.Exists(filePath))
            {
                return await File.ReadAllBytesAsync(filePath);
            }
        }
        
        // Otherwise generate new PDF
        return await GenerateCertificatePdfAsync(certificate.StudentId, certificate.CourseId);
    }

    private string GenerateCertificateNumber()
    {
        return $"EL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    public async Task<string> GenerateExamCertificateAsync(string studentName, string courseTitle, double percentage, string certificateNumber, string instructorName)
    {
        var fileName = $"exam_certificate_{certificateNumber}.pdf";
        var filePath = Path.Combine("wwwroot", "certificates", fileName);
        
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        using var stream = new FileStream(filePath, FileMode.Create);
        var document = new Document(PageSize.A4, 30, 30, 30, 30);
        var writer = PdfWriter.GetInstance(document, stream);
        
        document.Open();

        // Create certificate border
        var borderTable = new PdfPTable(1) { WidthPercentage = 100 };
        borderTable.DefaultCell.Border = iTextSharp.text.Rectangle.BOX;
        borderTable.DefaultCell.BorderWidth = 6;
        borderTable.DefaultCell.BorderColor = new BaseColor(44, 90, 160);
        borderTable.DefaultCell.Padding = 15;

        var innerTable = new PdfPTable(1) { WidthPercentage = 100 };
        innerTable.DefaultCell.Border = iTextSharp.text.Rectangle.BOX;
        innerTable.DefaultCell.BorderWidth = 2;
        innerTable.DefaultCell.BorderColor = new BaseColor(212, 175, 55);
        innerTable.DefaultCell.Padding = 25;

        // Header
        var headerFont = FontFactory.GetFont(FontFactory.TIMES_BOLD, 32, new BaseColor(44, 90, 160));
        var header = new Paragraph("EXAM CERTIFICATE", headerFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 5 };
        var subHeader = new Paragraph("OF ACHIEVEMENT", FontFactory.GetFont(FontFactory.TIMES_ROMAN, 14, new BaseColor(102, 102, 102))) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 20 };
        
        innerTable.AddCell(new PdfPCell(header) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        innerTable.AddCell(new PdfPCell(subHeader) { Border = iTextSharp.text.Rectangle.NO_BORDER });

        // Body
        var bodyFont = FontFactory.GetFont(FontFactory.TIMES_ROMAN, 14, new BaseColor(51, 51, 51));
        var nameFont = FontFactory.GetFont(FontFactory.TIMES_BOLD, 24, new BaseColor(44, 90, 160));
        var courseFont = FontFactory.GetFont(FontFactory.TIMES_BOLDITALIC, 18, new BaseColor(212, 175, 55));
        var percentageFont = FontFactory.GetFont(FontFactory.TIMES_BOLD, 20, new BaseColor(220, 20, 60));

        var issuedDate = DateTime.UtcNow;
        var validUntil = issuedDate.AddYears(1);

        var bodyText = new Paragraph { Alignment = Element.ALIGN_CENTER, SpacingAfter = 15 };
        bodyText.Add(new Chunk("This is to certify that\n\n", bodyFont));
        bodyText.Add(new Chunk($"{studentName}\n\n", nameFont));
        bodyText.Add(new Chunk("has successfully passed the examination for\n\n", bodyFont));
        bodyText.Add(new Chunk($"{courseTitle}\n\n", courseFont));
        bodyText.Add(new Chunk($"with {percentage:F1}% marks\n\n", percentageFont));
        bodyText.Add(new Chunk($"Issued on {issuedDate:MMMM dd, yyyy}", bodyFont));
        
        innerTable.AddCell(new PdfPCell(bodyText) { Border = iTextSharp.text.Rectangle.NO_BORDER });

        // Validity Information
        var validityFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(220, 20, 60));
        var validityText = new Paragraph($"Valid Until: {validUntil:dd/MM/yyyy}", validityFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 20 };
        innerTable.AddCell(new PdfPCell(validityText) { Border = iTextSharp.text.Rectangle.NO_BORDER });

        // Footer with signatures and official stamp
        var footerTable = new PdfPTable(3) { WidthPercentage = 100, SpacingBefore = 20 };
        var signatureFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
        
        var instructor = new Paragraph { Alignment = Element.ALIGN_CENTER };
        instructor.Add(new Chunk("_________________\n", signatureFont));
        instructor.Add(new Chunk($"{instructorName}\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
        instructor.Add(new Chunk("Course Instructor", signatureFont));
        
        // Official Stamp/Seal
        var stamp = new Paragraph { Alignment = Element.ALIGN_CENTER };
        stamp.Add(new Chunk("🏆", FontFactory.GetFont(FontFactory.HELVETICA, 40, new BaseColor(212, 175, 55))));
        stamp.Add(new Chunk("\n[OFFICIAL SEAL]\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(44, 90, 160))));
        stamp.Add(new Chunk("EasyLearn\nExam Board", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, new BaseColor(102, 102, 102))));
        
        var director = new Paragraph { Alignment = Element.ALIGN_CENTER };
        director.Add(new Chunk("_________________\n", signatureFont));
        director.Add(new Chunk("Director\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
        director.Add(new Chunk("EasyLearn Platform", signatureFont));
        
        footerTable.AddCell(new PdfPCell(instructor) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        footerTable.AddCell(new PdfPCell(stamp) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        footerTable.AddCell(new PdfPCell(director) { Border = iTextSharp.text.Rectangle.NO_BORDER });
        
        innerTable.AddCell(new PdfPCell(footerTable) { Border = iTextSharp.text.Rectangle.NO_BORDER });

        // Certificate ID and validity info
        var certInfo = new Paragraph { Alignment = Element.ALIGN_CENTER, SpacingBefore = 10 };
        certInfo.Add(new Chunk($"Certificate ID: {certificateNumber}\n", FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(153, 153, 153))));
        certInfo.Add(new Chunk($"Valid from {issuedDate:dd/MM/yyyy} to {validUntil:dd/MM/yyyy}", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, new BaseColor(220, 20, 60))));
        innerTable.AddCell(new PdfPCell(certInfo) { Border = iTextSharp.text.Rectangle.NO_BORDER });

        borderTable.AddCell(innerTable);
        document.Add(borderTable);

        document.Close();
        return filePath;
    }
}

public interface IProgressService
{
    Task<int> CalculateCourseProgressAsync(string studentId, int courseId);
    Task UpdateLessonProgressAsync(string studentId, int lessonId, bool completed);
    Task<bool> IsCourseCompletedAsync(string studentId, int courseId);
    Task HandleCourseCompletionAsync(string studentId, int courseId);
}

public class ProgressService : IProgressService
{
    private readonly ApplicationDbContext _context;
    private readonly IExamService _examService;

    public ProgressService(ApplicationDbContext context, IExamService examService)
    {
        _context = context;
        _examService = examService;
    }

    public async Task<int> CalculateCourseProgressAsync(string studentId, int courseId)
    {
        var totalLessons = await _context.Lessons.CountAsync(l => l.CourseId == courseId && l.IsActive);
        if (totalLessons == 0) return 0;

        var completedLessons = await _context.LessonProgresses
            .CountAsync(lp => lp.StudentId == studentId && 
                             lp.Lesson.CourseId == courseId && 
                             lp.IsCompleted);

        return (int)Math.Round((double)completedLessons / totalLessons * 100);
    }

    public async Task UpdateLessonProgressAsync(string studentId, int lessonId, bool completed)
    {
        var progress = await _context.LessonProgresses
            .FirstOrDefaultAsync(lp => lp.StudentId == studentId && lp.LessonId == lessonId);

        if (progress == null)
        {
            progress = new LessonProgress
            {
                StudentId = studentId,
                LessonId = lessonId,
                IsCompleted = completed,
                CompletedAt = completed ? DateTime.UtcNow : null
            };
            _context.LessonProgresses.Add(progress);
        }
        else
        {
            progress.IsCompleted = completed;
            progress.CompletedAt = completed ? DateTime.UtcNow : null;
        }

        await _context.SaveChangesAsync();

        // Update course enrollment progress
        var lesson = await _context.Lessons.FindAsync(lessonId);
        if (lesson != null)
        {
            var courseProgress = await CalculateCourseProgressAsync(studentId, lesson.CourseId);
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == lesson.CourseId);

            if (enrollment != null)
            {
                enrollment.Progress = courseProgress;
                if (courseProgress == 100 && enrollment.CompletedAt == null)
                {
                    enrollment.CompletedAt = DateTime.UtcNow;
                    enrollment.IsCompleted = true;
                    await _context.SaveChangesAsync();
                    
                    // Schedule exam for student
                    await HandleCourseCompletionAsync(studentId, lesson.CourseId);
                }
                else
                {
                    await _context.SaveChangesAsync();
                }
            }
        }
    }

    public async Task<bool> IsCourseCompletedAsync(string studentId, int courseId)
    {
        var progress = await CalculateCourseProgressAsync(studentId, courseId);
        return progress == 100;
    }

    public async Task HandleCourseCompletionAsync(string studentId, int courseId)
    {
        // Schedule exam for the student
        await _examService.ScheduleExamForStudentAsync(studentId, courseId);
    }
}