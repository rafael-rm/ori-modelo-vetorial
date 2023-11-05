using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using ModeloVetorial.Models;
using ModeloVetorial.Util;

namespace ModeloVetorial.Controllers;

public class SearchController : Controller
{
    private static InvertedIndex? _indexInstance;
    private readonly ILogger<SearchController> _logger;

    public SearchController(ILogger<SearchController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult StartPage()
    {
        return View();
    }

    [HttpGet("Authors")]
    public IActionResult Authors()
    {
        return View();
    }

    [HttpGet("SearchResult")]
    public async Task<IActionResult> SearchResult(string query)
    {
        if (_indexInstance is null)
            _indexInstance = new InvertedIndex();

        var result = await _indexInstance.Search(query);

        return View(result);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public IActionResult OpenHtml(string fileName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var path = $"{Directory.GetParent(Environment.CurrentDirectory)?.FullName}\\ModeloVetorial\\Data\\htmlFiles";
        var filePath = Path.Combine(path, fileName);

        var fileHtml = System.IO.File.ReadAllText(filePath, Encoding.GetEncoding(1252));
        return Content(fileHtml, "text/html");
    }

    public async Task<IActionResult> ResetIndex()
    {
        _indexInstance = new InvertedIndex();
        var projectDirectory = Directory.GetParent(Environment.CurrentDirectory)?.FullName;
        var pathFolderIndex = projectDirectory + "\\ModeloVetorial\\Data\\index.json";
        System.IO.File.Delete(pathFolderIndex);
        await _indexInstance.StartIndex();
        return Ok();
    }
}