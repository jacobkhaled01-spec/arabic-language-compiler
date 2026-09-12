using System;
using System.Collections.Generic;
using System.Text;
using CompilerProject.Models;

namespace CompilerProject.IntermediateCode
{
    /// <summary>
    /// مولد الكود الوسيط (Intermediate Code Generator)
    /// يولد كود ثلاثي العناوين (Three-Address Code - TAC) لجميع تراكيب اللغة العربية
    /// Reference: قواعد لغة البرمجة العربية - جامعة إب (ص 3 - 5)
    /// </summary>
    public class IntermediateCodeGenerator
    {
        private int _tempCounter = 1;
        private int _labelCounter = 1;
        private readonly List<string> _instructions = new List<string>();

        public IReadOnlyList<string> Instructions => _instructions;

        private string NewTemp() => $"t{_tempCounter++}";
        private string NewLabel() => $"L{_labelCounter++}";

        /// <summary>
        /// توليد الكود الوسيط من شجرة الإعراب المجردة (AST)
        /// </summary>
        public List<string> Generate(Node rootNode)
        {
            _instructions.Clear();
            _tempCounter = 1;
            _labelCounter = 1;

            if (rootNode == null) return _instructions;

            _instructions.Add("// ===================================================");
            _instructions.Add($"// بداية الكود الوسيط (TAC) للبرنامج: {rootNode.Name}");
            _instructions.Add("// ===================================================");

            GenerateNode(rootNode);

            _instructions.Add("// نهاية البرنامج");
            return _instructions;
        }

        private void GenerateNode(Node node)
        {
            if (node == null) return;

            switch (node.Value)
            {
                case "ProgramRoot":
                case "Block":
                case "Declarations":
                case "ConstDeclarations":
                case "VarDeclarations":
                case "StatementList":
                    foreach (var child in node.Children)
                    {
                        GenerateNode(child);
                    }
                    break;

                case "ConstDecl":
                    _instructions.Add($"{node.Name} = {node.Val}");
                    break;

                case "Assign":
                    GenerateAssign(node);
                    break;

                case "ReadStatement":
                    if (node.Children.Count > 0)
                    {
                        string varName = node.Children[0].Name;
                        _instructions.Add($"read {varName}");
                    }
                    break;

                case "PrintStatement":
                    GeneratePrint(node);
                    break;

                case "IfStatement":
                    GenerateIf(node);
                    break;

                case "WhileStatement":
                    GenerateWhile(node);
                    break;

                case "RepeatUntilStatement":
                    GenerateRepeatUntil(node);
                    break;

                case "ForStatement":
                    GenerateFor(node);
                    break;
            }
        }

        private void GenerateAssign(Node assignNode)
        {
            string varName = assignNode.Name;
            if (assignNode.Children.Count > 1)
            {
                string exprResult = GenerateExpression(assignNode.Children[1]);
                _instructions.Add($"{varName} = {exprResult}");
            }
        }

        private void GeneratePrint(Node printNode)
        {
            foreach (var child in printNode.Children)
            {
                if (child.Value == "StringLiteral")
                {
                    _instructions.Add($"print \"{child.Val}\"");
                }
                else
                {
                    string res = GenerateExpression(child);
                    _instructions.Add($"print {res}");
                }
            }
        }

        private void GenerateIf(Node ifNode)
        {
            if (ifNode.Children.Count < 2) return;

            string trueLabel = NewLabel();
            string falseLabel = NewLabel();
            string endLabel = NewLabel();

            var condNode = ifNode.Children[0];
            var thenNode = ifNode.Children[1];
            Node? elseNode = ifNode.Children.Count > 2 ? ifNode.Children[2] : null;

            // توليد الشرط
            GenerateCondition(condNode, trueLabel, falseLabel);

            // كتلة Then
            _instructions.Add($"{trueLabel}:");
            GenerateNode(thenNode);

            if (elseNode != null)
            {
                _instructions.Add($"goto {endLabel}");
                _instructions.Add($"{falseLabel}:");
                GenerateNode(elseNode);
                _instructions.Add($"{endLabel}:");
            }
            else
            {
                _instructions.Add($"{falseLabel}:");
            }
        }

        private void GenerateWhile(Node whileNode)
        {
            if (whileNode.Children.Count < 2) return;

            string startLabel = NewLabel();
            string bodyLabel = NewLabel();
            string endLabel = NewLabel();

            _instructions.Add($"{startLabel}:");
            GenerateCondition(whileNode.Children[0], bodyLabel, endLabel);

            _instructions.Add($"{bodyLabel}:");
            GenerateNode(whileNode.Children[1]);
            _instructions.Add($"goto {startLabel}");

            _instructions.Add($"{endLabel}:");
        }

        private void GenerateRepeatUntil(Node repeatNode)
        {
            if (repeatNode.Children.Count < 2) return;

            string loopLabel = NewLabel();
            string endLabel = NewLabel();

            _instructions.Add($"{loopLabel}:");
            GenerateNode(repeatNode.Children[0]); // الجسم أولاً

            // الفحص: إذا تحقق الشرط يخرج، وإلا يكرر
            GenerateCondition(repeatNode.Children[1], endLabel, loopLabel);
            _instructions.Add($"{endLabel}:");
        }

        private void GenerateFor(Node forNode)
        {
            string loopVar = forNode.Name;
            string startVal = GenerateExpression(forNode.Children[0]);
            string endVal = GenerateExpression(forNode.Children[1]);
            string stepVal = forNode.Children.Count > 3 ? GenerateExpression(forNode.Children[2]) : "1";
            var bodyNode = forNode.Children[forNode.Children.Count - 1];

            string startLabel = NewLabel();
            string bodyLabel = NewLabel();
            string endLabel = NewLabel();

            // التهيئة الأولية
            _instructions.Add($"{loopVar} = {startVal}");

            _instructions.Add($"{startLabel}:");
            _instructions.Add($"if {loopVar} <= {endVal} goto {bodyLabel}");
            _instructions.Add($"goto {endLabel}");

            _instructions.Add($"{bodyLabel}:");
            GenerateNode(bodyNode);
            _instructions.Add($"{loopVar} = {loopVar} + {stepVal}");
            _instructions.Add($"goto {startLabel}");

            _instructions.Add($"{endLabel}:");
        }

        private void GenerateCondition(Node condNode, string trueLabel, string falseLabel)
        {
            if (condNode.Value == "BinaryExpr")
            {
                if (IsRelationalOp(condNode.Val))
                {
                    string left = GenerateExpression(condNode.Children[0]);
                    string right = GenerateExpression(condNode.Children[1]);
                    _instructions.Add($"if {left} {condNode.Val} {right} goto {trueLabel}");
                    _instructions.Add($"goto {falseLabel}");
                    return;
                }

                if (condNode.Val == "&&")
                {
                    string nextLabel = NewLabel();
                    GenerateCondition(condNode.Children[0], nextLabel, falseLabel);
                    _instructions.Add($"{nextLabel}:");
                    GenerateCondition(condNode.Children[1], trueLabel, falseLabel);
                    return;
                }

                if (condNode.Val == "||")
                {
                    string nextLabel = NewLabel();
                    GenerateCondition(condNode.Children[0], trueLabel, nextLabel);
                    _instructions.Add($"{nextLabel}:");
                    GenerateCondition(condNode.Children[1], trueLabel, falseLabel);
                    return;
                }
            }

            string res = GenerateExpression(condNode);
            _instructions.Add($"if {res} != 0 goto {trueLabel}");
            _instructions.Add($"goto {falseLabel}");
        }

        private string GenerateExpression(Node exprNode)
        {
            if (exprNode == null) return "";

            if (exprNode.Value == "Number") return exprNode.Val;
            if (exprNode.Value == "Variable") return exprNode.Name;
            if (exprNode.Value == "BooleanLiteral") return exprNode.Val == "صح" ? "1" : "0";
            if (exprNode.Value == "StringLiteral") return $"\"{exprNode.Val}\"";
            if (exprNode.Value == "CharLiteral") return $"'{exprNode.Val}'";

            if (exprNode.Value == "FieldAccess")
            {
                string target = exprNode.Children.Count > 0 ? GenerateExpression(exprNode.Children[0]) : "obj";
                return $"{target}.{exprNode.Name}";
            }

            if (exprNode.Value == "IndexedAccess")
            {
                string target = exprNode.Children.Count > 0 ? GenerateExpression(exprNode.Children[0]) : "arr";
                string idx = exprNode.Children.Count > 1 ? GenerateExpression(exprNode.Children[1]) : "0";
                return $"{target}[{idx}]";
            }

            if (exprNode.Value == "UnaryExpr")
            {
                string operand = GenerateExpression(exprNode.Children[0]);
                string t = NewTemp();
                _instructions.Add($"{t} = {exprNode.Val} {operand}");
                return t;
            }

            if (exprNode.Value == "BinaryExpr")
            {
                string left = GenerateExpression(exprNode.Children[0]);
                string right = GenerateExpression(exprNode.Children[1]);
                string t = NewTemp();
                _instructions.Add($"{t} = {left} {exprNode.Val} {right}");
                return t;
            }

            return exprNode.Name;
        }

        private static bool IsRelationalOp(string op)
        {
            return op is "==" or "!=" or "<" or ">" or "<=" or ">=";
        }
    }
}
