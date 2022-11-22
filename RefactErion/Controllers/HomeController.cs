using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using RefactErion.Models;
using SyntaxNode = Microsoft.CodeAnalysis.SyntaxNode;

namespace RefactErion.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost]
    public IActionResult Refactor(string body)
    {
        SyntaxNode rootToReturn;
        var classNode = GetClass(body);

        new RefactoredNodeBuilder()
            .WithConsts(classNode, out rootToReturn)
            .WithNoUnusedVariables(rootToReturn, out rootToReturn)
            .WithInlineTempReturn(rootToReturn, out rootToReturn)
            .WithNoUnusedParameters(rootToReturn, out rootToReturn)
            .Build();
        
        return View("Refactored", new RefactoredModel() { Body = rootToReturn.ToString() });
    }
    
    private SyntaxNode GetClass(string body)
    {
        var syntaxTree = SyntaxFactory.ParseSyntaxTree(body);
        var root = syntaxTree.GetRoot();
        var classNode = root.ChildNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var originalClass = classNode;
        var className = classNode?.Identifier.Text;
        
        if ((className!.StartsWith("I") && char.IsUpper(className.ToCharArray()[1])) || className.StartsWith("II"))
        {
            classNode = classNode.ReplaceToken(classNode!.Identifier,
                SyntaxFactory.Identifier(originalClass!.Identifier.LeadingTrivia,
                    originalClass.Identifier.Text.Remove(0, 1), originalClass.Identifier.TrailingTrivia));
            
            originalClass = classNode;
        }

        return originalClass!;
    }
}