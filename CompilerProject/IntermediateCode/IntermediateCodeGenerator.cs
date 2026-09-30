using System;
using System.Collections.Generic;
using System.Linq;
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

            if (rootNode.Children.Count > 0)
            {
                var block = rootNode.Children[0];
                if (block.Children.Count > 0)
                {
                    var decls = block.Children[0];
                    bool hasProcs = decls.Children.Any(c => c.Value == "ProcDecl");
                    string mainLabel = "L_MAIN_ENTRY";
                    if (hasProcs)
                    {
                        _instructions.Add($"goto {mainLabel}");
                    }
                    GenerateNode(decls);
                    if (hasProcs)
                    {
                        _instructions.Add($"{mainLabel}:");
                    }
                    if (block.Children.Count > 1)
                    {
                        GenerateNode(block.Children[1]);
                    }
                }
            }

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
                    foreach (var child in node.Children)
                    {
                        string varName = child.Name;
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

                case "ProcDecl":
                    string procName = node.Name;
                    _instructions.Add($"proc_{procName}:");
                    foreach (var child in node.Children)
                    {
                        if (child.Value is "FormalParams" or "Parameters")
                        {
                            foreach (var p in child.Children)
                            {
                                _instructions.Add($"{p.Name} = pop_param");
                            }
                        }
                        else if (child.Value == "Block")
                        {
                            GenerateNode(child);
                        }
                    }
                    _instructions.Add("return");
                    break;

                case "CallStatement":
                    foreach (var arg in node.Children)
                    {
                        string argVal = GenerateExpression(arg);
                        _instructions.Add($"param {argVal}");
                    }
                    _instructions.Add($"call proc_{node.Name}");
                    break;
            }
        }

        private void GenerateAssign(Node assignNode)
        {
            string varName = assignNode.Name;
            if (assignNode.Children.Count > 0 && assignNode.Children[0].Value == "IndexedAccess")
            {
                var idxAccess = assignNode.Children[0];
                string target = idxAccess.Children.Count > 0 ? GenerateExpression(idxAccess.Children[0]) : "arr";
                string idx = idxAccess.Children.Count > 1 ? GenerateExpression(idxAccess.Children[1]) : "0";
                varName = $"{target}[{idx}]";
            }
            if (assignNode.Children.Count > 1)
            {
                string exprResult = GenerateExpression(assignNode.Children[1]);
                _instructions.Add($"{varName} = {exprResult}");
            }
        }

        private void GeneratePrint(Node printNode)
        {
            for (int i = 0; i < printNode.Children.Count; i++)
            {
                var child = printNode.Children[i];
                bool isLast = (i + 1 == printNode.Children.Count);
                string cmd = isLast ? "print " : "print_raw ";
                if (child.Value == "StringLiteral")
                {
                    _instructions.Add($"{cmd}\"{child.Val}\"");
                }
                else
                {
                    string res = GenerateExpression(child);
                    _instructions.Add($"{cmd}{res}");
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
