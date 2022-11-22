using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace RefactErion.Models;

public class RefactoredNodeBuilder
{
    public SyntaxNode node = null;
    public RefactoredNodeBuilder() { }

    public SyntaxNode Build() => node;

    public RefactoredNodeBuilder WithConsts(SyntaxNode classNode, out SyntaxNode node)
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

                    SyntaxToken constToken = SyntaxFactory.Token(leadingTrivia, SyntaxKind.ConstKeyword, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker));
                    SyntaxTokenList newModifiers = trimmedLocal.Modifiers.Insert(0, constToken);

                    LocalDeclarationStatementSyntax newLocal = trimmedLocal
                        .WithModifiers(newModifiers)
                        .WithDeclaration(localDeclaration.Declaration);
                    
                    LocalDeclarationStatementSyntax formattedLocal = newLocal.WithAdditionalAnnotations(Formatter.Annotation);
                    
                    replacementNodeMap.Add(localDeclaration, formattedLocal);
                }
        }
        
        methodDecl = methodDecl?.ReplaceNodes(nodesToReplace, computeReplacementNode: (o, n) => replacementNodeMap[o]);
                    
        newRoot = classNode.ReplaceNode(originalMethodDecl!, methodDecl!);
        newRoot = newRoot.NormalizeWhitespace();

        node = newRoot;
        
        return this;
    }

    public RefactoredNodeBuilder WithNoUnusedVariables(SyntaxNode classNode, out SyntaxNode node)
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

        node = newRoot;
        
        return this;
    }
    
    public RefactoredNodeBuilder WithInlineTempReturn(SyntaxNode classNode, out SyntaxNode node)
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

        node = newRoot;
        
        return this;
    }
    
    public RefactoredNodeBuilder WithNoUnusedParameters(SyntaxNode classNode, out SyntaxNode node)
    {
        var originalMethodDecl = classNode?.ChildNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var methodDecl = originalMethodDecl;
        var parameterList = originalMethodDecl.ParameterList;
        var parametersToRemove = new List<ParameterSyntax>();
        
        foreach (var parameter in methodDecl.ParameterList.Parameters)
        {
            var allNodes = methodDecl.Body.DescendantNodes();
        
            if (!allNodes.OfType<IdentifierNameSyntax>().ToList().Any(x => x.Identifier.Text.Equals(parameter.Identifier.Text)))
            {
                parametersToRemove.Add(parameter);
            }
        }
        
        parameterList = parameterList.RemoveNodes(parametersToRemove, SyntaxRemoveOptions.KeepNoTrivia);
        methodDecl = methodDecl.WithParameterList(parameterList);
        methodDecl = methodDecl?.WithAdditionalAnnotations(Formatter.Annotation);
        
        var newRoot = classNode.ReplaceNode(originalMethodDecl!, methodDecl!);
        newRoot = newRoot.NormalizeWhitespace();

        node = newRoot;
        
        return this;
    }
}