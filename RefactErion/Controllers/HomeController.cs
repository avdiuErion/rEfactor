using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
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
    public IActionResult Refactor(string body, string refactorType)
    {
        SyntaxNode rootToReturn = null;
        var classNode = GetClass(body);

        var nodeWithConsts = MakeConsts(classNode);
        //var nodeWithSplittedInlines = SplitInlineTemp(nodeWithConsts);
        //var nodeWithInlineReturn = GenerateInlineTemp(nodeWithConsts);
        var nodeWithRemovedUnusedVariable = RemoveUnusedVariables(nodeWithConsts);

        rootToReturn = nodeWithRemovedUnusedVariable;
        
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
    
    private SyntaxNode MakeConsts(SyntaxNode classNode)
    {
        var originalMethodDecl = classNode?.ChildNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        SyntaxNode newRoot = null;
        var methodDecl = originalMethodDecl;
        var nodesToReplace = new List<SyntaxNode>();
        var replacementNodeMap = new Dictionary<SyntaxNode, SyntaxNode>();

        foreach (LocalDeclarationStatementSyntax localDeclaration in methodDecl?.Body?.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().ToList())
        {
            var variableName = localDeclaration.Declaration.Variables.FirstOrDefault().Identifier;
            if (!methodDecl.Body.DescendantNodesAndTokens().ToList().Except(localDeclaration.DescendantNodesAndTokens()).ToList()
                    .Any(x => x.IsKind(SyntaxKind.SimpleAssignmentExpression) 
                              && x.ChildNodesAndTokens().First().ToString().Equals(variableName.ToString())))
                {
                    nodesToReplace.Add(localDeclaration);
                    SyntaxToken firstToken = localDeclaration.GetFirstToken();
                    SyntaxTriviaList leadingTrivia = firstToken.LeadingTrivia;
                    LocalDeclarationStatementSyntax trimmedLocal = localDeclaration.ReplaceToken(
                        firstToken, firstToken.WithLeadingTrivia(SyntaxTriviaList.Empty));

                    // Create a const token with the leading trivia.
                    SyntaxToken constToken = SyntaxFactory.Token(leadingTrivia, SyntaxKind.ConstKeyword, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker));

                    // Insert the const token into the modifiers list, creating a new modifiers list.
                    SyntaxTokenList newModifiers = trimmedLocal.Modifiers.Insert(0, constToken);
                    // Produce the new local declaration.
                    LocalDeclarationStatementSyntax newLocal = trimmedLocal
                        .WithModifiers(newModifiers)
                        .WithDeclaration(localDeclaration.Declaration);

                    // Add an annotation to format the new local declaration.
                    LocalDeclarationStatementSyntax formattedLocal = newLocal.WithAdditionalAnnotations(Formatter.Annotation);
                    
                    replacementNodeMap.Add(localDeclaration, formattedLocal);
                }
        }
        
        methodDecl = methodDecl?.ReplaceNodes(nodesToReplace, computeReplacementNode: (o, n) => replacementNodeMap[o]);
                    
        newRoot = classNode.ReplaceNode(originalMethodDecl!, methodDecl!);
        newRoot = newRoot.NormalizeWhitespace();
        
        return newRoot!;
    }
    
    public SyntaxNode SplitInlineTemp(SyntaxNode classNode)
    {
        var originalMethodDecl = classNode?.ChildNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var methodDecl = originalMethodDecl;
        var nodesToRename = new List<SyntaxNode>();
        var replacementNodeMap = new Dictionary<SyntaxNode, SyntaxNode>(nodesToRename.Count());

        for (int i = 0; i < methodDecl?.Body?.Statements.OfType<ExpressionStatementSyntax>().Count(); i++)
        {
            var nodeToCompare = methodDecl.Body.Statements.OfType<ExpressionStatementSyntax>().ToList()[i];
            for (int j = 0; j < methodDecl.Body?.Statements.OfType<LocalDeclarationStatementSyntax>().Count(); j++)
            {
                var variableDeclaratorToCompare =
                    methodDecl.Body?.Statements.OfType<LocalDeclarationStatementSyntax>().ToList()[j].ChildNodes()
                        .OfType<VariableDeclarationSyntax>().FirstOrDefault()?.ChildNodes()
                        .OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Identifier.Text;
                if (nodeToCompare?.Expression?.ChildNodes().OfType<IdentifierNameSyntax>().FirstOrDefault()?.Identifier
                        .Text == variableDeclaratorToCompare)
                {
                    nodesToRename.Add(nodeToCompare!);
                    replacementNodeMap.Add(nodeToCompare!, SyntaxGenerator.GetGenerator(new AdhocWorkspace(), LanguageNames.CSharp)
                        .LocalDeclarationStatement(" variable" + (i + 1),
                            nodeToCompare?.Expression?.ChildNodes().OfType<LiteralExpressionSyntax>().FirstOrDefault()
                                .WithLeadingTrivia()));
                }
            }
        }
        
        methodDecl = methodDecl.ReplaceNodes(nodesToRename, computeReplacementNode: (o, n) => replacementNodeMap[o]);
        methodDecl = methodDecl.WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = classNode.ReplaceNode(originalMethodDecl, methodDecl);
        newRoot = newRoot.NormalizeWhitespace();
        
        return newRoot;
    }

    private SyntaxNode GenerateInlineTemp(SyntaxNode classNode)
    {
        var originalMethodDecl = classNode?.ChildNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var methodDecl = originalMethodDecl;
        var variableDeclarator =
            methodDecl?.Body?.DescendantNodes().OfType<VariableDeclaratorSyntax>().LastOrDefault();
        var returnStatement = methodDecl?.Body?.DescendantNodes().OfType<ReturnStatementSyntax>().First();
        var newReturnStatement = SyntaxFactory.ReturnStatement(variableDeclarator?.Initializer?.Value);
    
        methodDecl = methodDecl?.ReplaceNode(returnStatement!, newReturnStatement);
        
        var variableDeclaratorFinal =
            methodDecl?.Body?.DescendantNodes().OfType<VariableDeclaratorSyntax>().LastOrDefault();

        methodDecl = methodDecl?.RemoveNode(variableDeclaratorFinal.Parent.Parent, SyntaxRemoveOptions.KeepNoTrivia);
        methodDecl = methodDecl?.WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = classNode.ReplaceNode(originalMethodDecl!, methodDecl!);
        newRoot = newRoot.NormalizeWhitespace();

        return newRoot;
    }
    
    private SyntaxNode RemoveUnusedVariables(SyntaxNode classNode)
    {
        var originalMethodDecl = classNode?.ChildNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var methodDecl = originalMethodDecl;
        var variablesToRemove = new List<SyntaxNode>();

        foreach (var declaration in methodDecl.Body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            var identifier = declaration.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault().Identifier;
            var allNodes = methodDecl.Body.DescendantNodes().Except(declaration.DescendantNodesAndSelf()).ToList();

            if (!allNodes.Any(x => x.ToString().Equals(identifier.Text)))
            {
                variablesToRemove.Add(identifier.Parent.Parent.Parent);
            }
        }

        methodDecl = methodDecl.RemoveNodes(variablesToRemove, SyntaxRemoveOptions.KeepNoTrivia);
        methodDecl = methodDecl?.WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = classNode.ReplaceNode(originalMethodDecl!, methodDecl!);
        newRoot = newRoot.NormalizeWhitespace();

        return newRoot;
    }
}