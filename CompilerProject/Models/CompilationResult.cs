using System.Collections.Generic;

namespace CompilerProject.Models
{
    /// <summary>
    /// نموذج نتائج مراحل الترجمة الموحد (Unified Compilation Result Model)
    /// يُستخدم لنقل مخرجات التحليل والترجمة بين المترجم والمحرر الرسومي
    /// </summary>
    public class CompilationResult
    {
        public bool IsSuccess { get; set; }
        public string SourceCode { get; set; } = string.Empty;
        public List<TokenDto> Tokens { get; set; } = new List<TokenDto>();
        public NodeDto? AST { get; set; }
        public List<SymbolDto> SymbolTable { get; set; } = new List<SymbolDto>();
        public List<string> SyntaxErrors { get; set; } = new List<string>();
        public List<string> SemanticErrors { get; set; } = new List<string>();
        public List<string> TAC { get; set; } = new List<string>();
        public string AssemblyCode { get; set; } = string.Empty;
        public string CILCode { get; set; } = string.Empty;
        public string ExecutionOutput { get; set; } = string.Empty;
    }

    public class TokenDto
    {
        public string Value { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Line { get; set; }
    }

    public class NodeDto
    {
        public string Value { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string Val { get; set; } = string.Empty;
        public int Line { get; set; }
        public List<NodeDto> Children { get; set; } = new List<NodeDto>();
    }

    public class SymbolDto
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int DeclaredLine { get; set; }
        public string ReferencedLines { get; set; } = string.Empty;
    }
}
