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
    public IActionResult Refactor(string body, string refactoringType)
    {
        var rootToReturn = "";
        var classNode = GetClass(body);

        var refactoringService = new RefactoringService();

        switch (refactoringType)
        {
            case "makeConsts":
                rootToReturn = refactoringService.MakeConsts(classNode).ToFullString();
                break;
            case "splitInline":
                rootToReturn = refactoringService.SplitInlineTemp(classNode).ToFullString();
                break;
            case "removeVariables":
                rootToReturn = refactoringService.MakeConsts(classNode).ToFullString();
                break;
            case "inlineTemp":
                rootToReturn = refactoringService.MakeConsts(classNode).ToFullString();
                break;
            case "removeParams":
                rootToReturn = refactoringService.MakeConsts(classNode).ToFullString();
                break;
        }
        
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