using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace RefactErion.Models;

public class RefactoringService
{
    public SyntaxNode node = null;

    public RefactoringService()
    {
    }

    public SyntaxNode Build() => node;

    public SyntaxNode MakeConsts(SyntaxNode classNode)
    {
        var originalMethodDecl = classNode?.ChildNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        SyntaxNode newRoot = null;
        var methodDecl = originalMethodDecl;
        var nodesToReplace = new List<SyntaxNode>();
        var replacementNodeMap = new Dictionary<SyntaxNode, SyntaxNode>();

        foreach (LocalDeclarationStatementSyntax localDeclaration in methodDecl?.Body?.DescendantNodes()
                     .OfType<LocalDeclarationStatementSyntax>().ToList())
        {
            var variableName = localDeclaration.Declaration.Variables.FirstOrDefault().Identifier;
            if (!methodDecl.Body.DescendantNodesAndTokens().ToList().Except(localDeclaration.DescendantNodesAndTokens())
                    .ToList()
                    .Any(x => x.IsKind(SyntaxKind.SimpleAssignmentExpression)
                              && x.ChildNodesAndTokens().First().ToString().Equals(variableName.ToString())))
            {
                nodesToReplace.Add(localDeclaration);
                SyntaxToken firstToken = localDeclaration.GetFirstToken();
                SyntaxTriviaList leadingTrivia = firstToken.LeadingTrivia;
                LocalDeclarationStatementSyntax trimmedLocal = localDeclaration.ReplaceToken(
                    firstToken, firstToken.WithLeadingTrivia(SyntaxTriviaList.Empty));

                SyntaxToken constToken = SyntaxFactory.Token(leadingTrivia, SyntaxKind.ConstKeyword,
                    SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker));
                SyntaxTokenList newModifiers = trimmedLocal.Modifiers.Insert(0, constToken);

                LocalDeclarationStatementSyntax newLocal = trimmedLocal
                    .WithModifiers(newModifiers)
                    .WithDeclaration(localDeclaration.Declaration);

                LocalDeclarationStatementSyntax formattedLocal =
                    newLocal.WithAdditionalAnnotations(Formatter.Annotation);

                replacementNodeMap.Add(localDeclaration, formattedLocal);
            }
        }

        methodDecl = methodDecl?.ReplaceNodes(nodesToReplace, computeReplacementNode: (o, n) => replacementNodeMap[o]);

        newRoot = classNode.ReplaceNode(originalMethodDecl!, methodDecl!);
        newRoot = newRoot.NormalizeWhitespace();

        return newRoot;
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
    public SyntaxNode RemoveUnusedVariables(SyntaxNode classNode)
    {
        var originalMethodDecl = classNode?.ChildNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var methodDecl = originalMethodDecl;
        var variablesToRemove = new List<SyntaxNode>();

        foreach (var declaration in methodDecl.Body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            var identifier = declaration.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault()
                .Identifier;
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

    public SyntaxNode ReturnInlineTemp(SyntaxNode classNode)
    {
        var originalMethodDecl = classNode?.ChildNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var methodDecl = originalMethodDecl;
        var variableDeclarator =
            methodDecl?.Body?.DescendantNodes().OfType<VariableDeclaratorSyntax>().LastOrDefault();
        var returnStatement = methodDecl?.Body?.DescendantNodes().OfType<ReturnStatementSyntax>().First();
        SyntaxNode newReturnStatement = null;
        var listToFilter = new List<SyntaxNode>();
        listToFilter.AddRange(methodDecl.Body.DescendantNodes().OfType<VariableDeclaratorSyntax>().ToList());
        listToFilter.AddRange(methodDecl.Body.DescendantNodes().OfType<AssignmentExpressionSyntax>().ToList());

        var returnStatementIdentifier = returnStatement.ChildNodes().OfType<IdentifierNameSyntax>().FirstOrDefault();
        foreach (var variable in listToFilter)
        {
            if (variable.IsKind(SyntaxKind.VariableDeclarator))
            {
                var varDeclatator = variable as VariableDeclaratorSyntax;
                if (varDeclatator.Identifier.Text.Equals(returnStatementIdentifier.Identifier.Text))
                {
                    newReturnStatement = SyntaxFactory.ReturnStatement(varDeclatator?.Initializer?.Value);
                    methodDecl = methodDecl?.ReplaceNode(returnStatement!, newReturnStatement);
                    var declaratorAfterReplace = methodDecl.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                        .FirstOrDefault(x => x.Identifier.Text.Equals(varDeclatator.Identifier.Text));
                    methodDecl = methodDecl.RemoveNode(declaratorAfterReplace.Parent.Parent,
                        SyntaxRemoveOptions.KeepNoTrivia);
                    break;
                }
            }
            else
            {
                var varExpression = variable as AssignmentExpressionSyntax;
                if (varExpression.Left.ToString().Equals(returnStatementIdentifier.Identifier.Text))
                {
                    newReturnStatement = SyntaxFactory.ReturnStatement(varExpression?.Right);
                    methodDecl = methodDecl?.ReplaceNode(returnStatement!, newReturnStatement);
                    var expressionAfterReplacement = methodDecl.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                        .FirstOrDefault(x => x.Left.ToString().Equals(varExpression.Left.ToString()));
                    methodDecl = methodDecl.RemoveNode(expressionAfterReplacement.Parent, SyntaxRemoveOptions.KeepNoTrivia);
                    break;
                }
            }
        }
        
        methodDecl = methodDecl?.WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = classNode.ReplaceNode(originalMethodDecl!, methodDecl!);
        newRoot = newRoot.NormalizeWhitespace();

        return newRoot;
    }

    public SyntaxNode RemoveUnusedParameters(SyntaxNode classNode)
    {
        var originalMethodDecl = classNode?.ChildNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var methodDecl = originalMethodDecl;
        var parameterList = originalMethodDecl.ParameterList;
        var parametersToRemove = new List<ParameterSyntax>();

        foreach (var parameter in methodDecl.ParameterList.Parameters)
        {
            var allNodes = methodDecl.Body.DescendantNodes();

            if (!allNodes.OfType<IdentifierNameSyntax>().ToList()
                    .Any(x => x.Identifier.Text.Equals(parameter.Identifier.Text)))
            {
                parametersToRemove.Add(parameter);
            }
        }

        parameterList = parameterList.RemoveNodes(parametersToRemove, SyntaxRemoveOptions.KeepNoTrivia);
        methodDecl = methodDecl.WithParameterList(parameterList);
        methodDecl = methodDecl?.WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = classNode.ReplaceNode(originalMethodDecl!, methodDecl!);
        newRoot = newRoot.NormalizeWhitespace();

        return newRoot;
    }
}