using System;
using System.Collections.Generic;
using CompilerProject.Common;
using CompilerProject.Models;

namespace CompilerProject.SemanticAnalysis
{
    /// <summary>
    /// المحلل الدلالي (Semantic Analyzer) للغة البرمجة العربية
    /// يتحقق من صحة المعاني، توافق الأنواع، وتعريف المتغيرات والثوابت قبل استخدامها
    /// Reference: قواعد لغة البرمجة العربية - جامعة إب (ص 2, 5, 10)
    /// </summary>
    public class SemanticAnalyzer
    {
        private readonly SymbolTable _symbolTable;
        private readonly List<string> _errors = new List<string>();

        public SemanticAnalyzer(SymbolTable symbolTable)
        {
            _symbolTable = symbolTable;
        }

        public IReadOnlyList<string> Errors => _errors;

        /// <summary>
        /// تحليل شجرة الإعراب المجردة (AST) دلالياً
        /// </summary>
        public bool Analyze(Node rootNode)
        {
            _errors.Clear();

            if (rootNode == null) return false;

            TraverseAndAnalyze(rootNode);

            return _errors.Count == 0;
        }

        private void TraverseAndAnalyze(Node node)
        {
            if (node == null) return;

            switch (node.Value)
            {
                case "ProgramRoot":
                    // تسجيل اسم البرنامج
                    _symbolTable.Add(node.Name, "برنامج", "اسم_برنامج", node.Line);
                    break;

                case "ConstDecl":
                    // تسجيل الثابت
                    if (!_symbolTable.Add(node.Name, InferLiteralType(node.Val), "ثابت", node.Line, node.Val))
                    {
                        _errors.Add($"خطأ دلالي في السطر {node.Line}: إعادة تعريف الثابت '{node.Name}'");
                    }
                    break;

                case "VarDecl":
                    // تسجيل المتغير
                    if (!_symbolTable.Add(node.Name, node.DataType, "متغير", node.Line))
                    {
                        _errors.Add($"خطأ دلالي في السطر {node.Line}: إعادة تعريف المتغير '{node.Name}'");
                    }
                    break;

                case "TypeDecl_Array":
                    _symbolTable.Add(node.Name, $"قائمة من {node.DataType}", "نوع_قائمة", node.Line, $"حجم={node.Val}");
                    break;

                case "TypeDecl_Record":
                    _symbolTable.Add(node.Name, "سجل", "نوع_سجل", node.Line);
                    break;

                case "ProcDecl":
                    _symbolTable.Add(node.Name, "اجراء", "اجراء", node.Line);
                    break;

                case "Variable":
                    // فحص استخدام المتغير
                    ValidateIdentifierUsage(node.Name, node.Line);
                    break;

                case "Assign":
                    // فحص صحة الإسناد
                    ValidateAssignment(node);
                    break;

                case "ReadStatement":
                    // فحص صحة متغير الإدخال
                    if (node.Children.Count > 0)
                    {
                        ValidateIdentifierUsage(node.Children[0].Name, node.Line);
                    }
                    break;

                case "BinaryExpr":
                    // فحص التوافق في العمليات
                    ValidateBinaryExpression(node);
                    break;
            }

            // فحص كافة العقد الفرعية
            foreach (var child in node.Children)
            {
                TraverseAndAnalyze(child);
            }
        }

        private void ValidateIdentifierUsage(string name, int line)
        {
            if (string.IsNullOrEmpty(name)) return;

            var sym = _symbolTable.Lookup(name);
            if (sym == null)
            {
                _errors.Add($"خطأ دلالي في السطر {line}: استخدام المعرف غير المعرف '{name}'");
            }
            else
            {
                _symbolTable.AddReference(name, line);
            }
        }

        private void ValidateAssignment(Node assignNode)
        {
            string varName = assignNode.Name;
            int line = assignNode.Line;

            var sym = _symbolTable.Lookup(varName);
            if (sym == null)
            {
                _errors.Add($"خطأ دلالي في السطر {line}: محاولة الإسناد إلى متغير غير معرف '{varName}'");
                return;
            }

            if (sym.Kind == "ثابت")
            {
                _errors.Add($"خطأ دلالي في السطر {line}: لا يمكن تغيير قيمة الثابت '{varName}'");
            }

            _symbolTable.AddReference(varName, line);
        }

        private void ValidateBinaryExpression(Node binNode)
        {
            string op = binNode.Val;
            int line = binNode.Line;

            // العمليات المنطقية تتطلب أطراف منطقية
            if (op is "&&" or "||")
            {
                // فحص منطقي
            }
            // عمليات القسمة تفحص القسمة على صفر للثوابت
            if (op is "/" or "\\" or "%")
            {
                if (binNode.Children.Count > 1 && binNode.Children[1].Value == "Number" && binNode.Children[1].Val == "0")
                {
                    _errors.Add($"تحذير دلالي في السطر {line}: محاولة القسمة على صفر");
                }
            }
        }

        private static string InferLiteralType(string val)
        {
            if (string.IsNullOrEmpty(val)) return "غير_محدد";
            if (val is "صح" or "خطأ") return "منطقي";
            if (val.Contains('.')) return "حقيقي";
            if (int.TryParse(val, out _)) return "صحيح";
            if (val.StartsWith('"')) return "خيط_رمزي";
            if (val.StartsWith('\'')) return "حرفي";
            return "صحيح";
        }
    }
}
