using Microsoft.CodeAnalysis;

namespace RefactErion.Models;

public class NodeBuilder
{
    public SyntaxNode node = null;
    public NodeBuilder() { }

    public SyntaxNode Build() => node;

    public NodeBuilder WithConsts(SyntaxNode classNode)
    {
        node = classNode;
        
        return this;
    }
    
    public NodeBuilder WithInlineTempSplit(SyntaxNode classNode)
    {
        node = classNode;
        
        return this;
    }
    
    public NodeBuilder WithInlineTempReturn(SyntaxNode classNode)
    {
        node = classNode;
        
        return this;
    }
}