using Microsoft.AspNetCore.Mvc;

namespace EasyLearn.Controllers;

public class HelpController : Controller
{
    [Route("help")]
    public IActionResult Index()
    {
        return View();
    }

    [Route("help/student")]
    public IActionResult Student()
    {
        return View();
    }

    [Route("help/instructor")]
    public IActionResult Instructor()
    {
        return View();
    }

    [Route("help/admin")]
    public IActionResult Admin()
    {
        return View();
    }

    [Route("help/getting-started")]
    public IActionResult GettingStarted()
    {
        return View();
    }

    [Route("help/faq")]
    public IActionResult FAQ()
    {
        return View();
    }

    [Route("help/contact")]
    public IActionResult Contact()
    {
        return View();
    }
}