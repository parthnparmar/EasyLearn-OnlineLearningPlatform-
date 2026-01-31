using EasyLearn.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyLearn.Controllers;

[Route("verify")]
public class VerificationController : Controller
{
    private readonly ApplicationDbContext _context;

    public VerificationController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("{certificateNumber}")]
    public async Task<IActionResult> VerifyCertificate(string certificateNumber)
    {
        var certificate = await _context.Certificates
            .Include(c => c.Student)
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber);

        if (certificate == null)
        {
            ViewBag.IsValid = false;
            ViewBag.Message = "Certificate not found or invalid.";
            return View();
        }

        ViewBag.IsValid = true;
        ViewBag.Certificate = certificate;
        ViewBag.Message = "Certificate is valid and verified.";
        
        return View();
    }
}