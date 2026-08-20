using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SensorApi;
using SensorDashboard.Models;

namespace SensorDashboard.Controllers;

public class HomeController(SensorDbContext database, IConfiguration configuration) : Controller
{
    public IActionResult Index()
    {
        ViewData["SensorHubUrl"] = configuration["SensorApi:HubUrl"];
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Recent(int limit = 60, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 1000);
        var readings = await database.Readings
            .AsNoTracking()
            .OrderByDescending(reading => reading.ReadTime)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Json(readings);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
