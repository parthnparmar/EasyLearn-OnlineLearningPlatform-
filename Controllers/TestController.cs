using Microsoft.AspNetCore.Mvc;

namespace EasyLearn.Controllers;

public class TestController : Controller
{
    public IActionResult Index()
    {
        return Content("Test route works!");
    }
    
    public IActionResult BrainGames()
    {
        return Content("Brain Games test route works!");
    }
}