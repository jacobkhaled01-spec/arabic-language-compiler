namespace CompilerProject.Models
{
    /// <summary>
    /// يمثل الوحدة المعجمية (Token) المستخرجة من المحلل اللغوي
    /// </summary>
    public class Token
    {
        /// <summary>
        /// النص الحقيقي للرمز المعجمي في الكود المصدري
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// نوع الرمز المعجمي (Keyword, Identifier, Number, ...)
        /// </summary>
        public TokenType Type { get; set; }

        /// <summary>
        /// رقم السطر الذي ظهر فيه الرمز
        /// </summary>
        public int Line { get; set; }

        public Token() { }

        public Token(string value, TokenType type, int line)
        {
            Value = value;
            Type = type;
            Line = line;
        }

        public override string ToString()
        {
            return $"[السطر {Line,2}] {Type,-12} : {Value}";
        }
    }
}
