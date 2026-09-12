using System;
using System.Collections.Generic;

namespace CompilerProject.Models
{
    /// <summary>
    /// تمثل العقدة في شجرة الإعراب المجردة (Abstract Syntax Tree - AST)
    /// Reference: قواعد لغة البرمجة العربية - جامعة إب (ص 1 - 6)
    /// </summary>
    public class Node
    {
        /// <summary>
        /// نوع العقدة النحوية (مثل: ProgramRoot, Block, ConstDecl, VarDecl, Assign, IfStatement, ForStatement, BinaryExpr, ...)
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// اسم المعرف أو المتغير أو الإجراء المرتبط بالعقدة
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// نوع البيانات المرتبط بالعقدة (مثل: صحيح، حقيقي، منطقي، حرفي، خيط_رمزي)
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// القيمة الفعلية (في حال كانت العقدة تمثل قيمة ثابتة أو معامل)
        /// </summary>
        public string Val { get; set; } = string.Empty;

        /// <summary>
        /// رقم السطر في الكود المصدري
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// قائمة العقد الفرعية المتفرعة من هذه العقدة
        /// </summary>
        public List<Node> Children { get; set; } = new List<Node>();

        public Node() { }

        public Node(string value, int line = 0)
        {
            Value = value;
            Line = line;
        }

        public Node(string value, string name, int line = 0)
        {
            Value = value;
            Name = name;
            Line = line;
        }

        public Node(string value, string name, string dataType, int line = 0)
        {
            Value = value;
            Name = name;
            DataType = dataType;
            Line = line;
        }

        public void AddChild(Node child)
        {
            if (child != null)
            {
                Children.Add(child);
            }
        }

        /// <summary>
        /// طباعة شجرة الـ AST بشكل هرمي مرئي
        /// </summary>
        public void Print(string indent = "", bool isLast = true)
        {
            Console.Write(indent);
            Console.Write(isLast ? "└── " : "├── ");

            string details = Value;
            if (!string.IsNullOrEmpty(Name)) details += $" [اسم: {Name}]";
            if (!string.IsNullOrEmpty(DataType)) details += $" [نوع: {DataType}]";
            if (!string.IsNullOrEmpty(Val)) details += $" [قيمة: {Val}]";
            if (Line > 0) details += $" (السطر {Line})";

            Console.WriteLine(details);

            indent += isLast ? "    " : "│   ";
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Print(indent, i == Children.Count - 1);
            }
        }
    }
}
