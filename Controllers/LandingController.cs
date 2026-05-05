using Microsoft.AspNetCore.Mvc;

namespace PeakMetrics.Web.Controllers;

/// <summary>
/// Public landing page — no authentication required.
/// </summary>
public class LandingController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
