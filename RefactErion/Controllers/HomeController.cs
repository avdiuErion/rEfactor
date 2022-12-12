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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost]
    public IActionResult Refactor(string body, string refactoringType)
    {
        SyntaxNode rootToReturn = null;
        var classNode = GetClass(body);

        switch (refactoringType)
        {
            case "makeConsts":
                new RefactoredNodeBuilder().MakeConsts(classNode, out rootToReturn).Build();
                break;
            case "splitInline":
                new RefactoredNodeBuilder().SplitInlineTemp(classNode, out rootToReturn).Build();
                break;
            case "removeVariables":
                new RefactoredNodeBuilder().RemoveUnusedVariables(classNode, out rootToReturn).Build();
                break;
            case "inlineTemp":
                new RefactoredNodeBuilder().ReturnInlineTemp(classNode, out rootToReturn).Build();
                break;
            case "removeParams":
                new RefactoredNodeBuilder().RemoveUnusedParameters(classNode, out rootToReturn).Build();
                break;
            default:
                new RefactoredNodeBuilder()
                    .MakeConsts(classNode, out rootToReturn)
                    .RemoveUnusedVariables(rootToReturn, out rootToReturn)
                    .ReturnInlineTemp(rootToReturn, out rootToReturn)
                    .RemoveUnusedParameters(rootToReturn, out rootToReturn)
                    .Build();
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