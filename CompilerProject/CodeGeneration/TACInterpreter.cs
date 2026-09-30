using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CompilerProject.CodeGeneration
{
    /// <summary>
    /// مفسر الكود الوسيط ثلاثي العناوين (TAC Interpreter) لمحرك C#
    /// يحقق التطابق التام 100% مع محرك C++ في تنفيذ الإجراءات، وتمرير المعاملات بالقيمة وبالمرجع،
    /// والمصفوفات، وفحص الحدود التشغيلي، والسجلات، والحلقات المتداخلة.
    /// </summary>
    public class TACInterpreter
    {
        private readonly List<string> _tac;
        private readonly Dictionary<string, double> _variables = new();
        private readonly Dictionary<string, string> _stringVars = new();
        private readonly Dictionary<string, int> _labels = new();
        private readonly Stack<int> _callStack = new();
        private readonly Queue<string> _paramQueue = new();
        private Dictionary<string, string> _aliases = new();
        private readonly Stack<Dictionary<string, string>> _aliasStack = new();
        private readonly Dictionary<string, long> _arrayBounds = new();
        private readonly Queue<string> _inputTokens = new();

        public TACInterpreter(List<string> tac, string userInput = "")
        {
            _tac = tac ?? new List<string>();
            PrepareInput(userInput);
        }

        private void PrepareInput(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput)) return;
            string normalized = rawInput.Replace('،', ' ').Replace(',', ' ');
            var tokens = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var t in tokens)
            {
                _inputTokens.Enqueue(t.Trim());
            }
        }

        private string ResolveArrayName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.EndsWith("]"))
            {
                int openBracket = name.IndexOf('[');
                int closeBracket = name.LastIndexOf(']');
                if (openBracket != -1 && closeBracket > openBracket)
                {
                    string arrayName = name.Substring(0, openBracket).Trim();
                    string indexStr = name.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();

                    string baseArray = arrayName;
                    if (_aliases.TryGetValue(arrayName, out var aliasedArr))
                    {
                        arrayName = aliasedArr;
                    }

                    double idxVal = EvaluateExpr(indexStr);
                    long idxInt = (long)Math.Round(idxVal);

                    long maxBound = -1;
                    if (_arrayBounds.TryGetValue(arrayName, out var b1)) maxBound = b1;
                    else if (_arrayBounds.TryGetValue(baseArray, out var b2)) maxBound = b2;

                    if (maxBound > 0 && (idxInt < 1 || idxInt > maxBound))
                    {
                        throw new Exception($"\n❌ [خطأ تشغيلي - Runtime Error]: تجاوز حدود المصفوفة! الفهرس [{idxInt}] يقع خارج نطاق المصفوفة '{baseArray}' المصرح بها بحجم [{maxBound}] (النطاق: 1 .. {maxBound}).\n");
                    }

                    return $"{arrayName}[{idxInt}]";
                }
            }

            if (_aliases.TryGetValue(name, out var aliasTarget))
            {
                return aliasTarget;
            }

            return name;
        }

        public double EvaluateExpr(string expr)
        {
            string s = expr?.Trim() ?? "";
            if (string.IsNullOrEmpty(s)) return 0.0;

            // Direct number
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double numVal))
            {
                return numVal;
            }

            // Booleans
            if (s is "صواب" or "true" or "صح") return 1.0;
            if (s is "خطأ" or "false" or "خطا") return 0.0;

            // Binary operators
            string[] ops = { "==", "!=", "<=", ">=", "<", ">", "&&", "||", "+", "-", "*", "/", "\\", "%", "^" };
            foreach (var op in ops)
            {
                string pattern = " " + op + " ";
                int pos = s.IndexOf(pattern, StringComparison.Ordinal);
                if (pos != -1)
                {
                    string leftStr = s.Substring(0, pos);
                    string rightStr = s.Substring(pos + pattern.Length);
                    double leftVal = EvaluateExpr(leftStr);
                    double rightVal = EvaluateExpr(rightStr);

                    return op switch
                    {
                        "+" => leftVal + rightVal,
                        "-" => leftVal - rightVal,
                        "*" => leftVal * rightVal,
                        "/" => (rightVal != 0.0) ? (leftVal / rightVal) : 0.0,
                        "\\" => (rightVal != 0.0) ? Math.Truncate(leftVal / rightVal) : 0.0,
                        "%" => (rightVal != 0.0) ? (leftVal % rightVal) : 0.0,
                        "^" => Math.Pow(leftVal, rightVal),
                        "==" => (leftVal == rightVal) ? 1.0 : 0.0,
                        "!=" => (leftVal != rightVal) ? 1.0 : 0.0,
                        "<" => (leftVal < rightVal) ? 1.0 : 0.0,
                        "<=" => (leftVal <= rightVal) ? 1.0 : 0.0,
                        ">" => (leftVal > rightVal) ? 1.0 : 0.0,
                        ">=" => (leftVal >= rightVal) ? 1.0 : 0.0,
                        "&&" => (leftVal != 0.0 && rightVal != 0.0) ? 1.0 : 0.0,
                        "||" => (leftVal != 0.0 || rightVal != 0.0) ? 1.0 : 0.0,
                        _ => 0.0
                    };
                }
            }

            // Unary operators
            if (s.StartsWith("-")) return -EvaluateExpr(s.Substring(1));
            if (s.StartsWith("+")) return EvaluateExpr(s.Substring(1));
            if (s.StartsWith("!")) return EvaluateExpr(s.Substring(1)) == 0.0 ? 1.0 : 0.0;

            // Variable / array element
            string resolved = ResolveArrayName(s);
            if (_aliases.TryGetValue(resolved, out var aliased)) resolved = aliased;

            if (_variables.TryGetValue(resolved, out var v1)) return v1;
            if (_variables.TryGetValue(s, out var v2)) return v2;
            if (_aliases.TryGetValue(s, out var a2) && _variables.TryGetValue(a2, out var v3)) return v3;

            return 0.0;
        }

        public string Execute()
        {
            _variables.Clear();
            _stringVars.Clear();
            _labels.Clear();
            _paramQueue.Clear();
            _aliases.Clear();
            _aliasStack.Clear();
            _arrayBounds.Clear();

            var output = new StringBuilder();

            // 1. Pass 1: جمع التسميات ومقاسات المصفوفات
            for (int i = 0; i < _tac.Count; i++)
            {
                string line = _tac[i].Trim();
                if (line.EndsWith(":"))
                {
                    string label = line.Substring(0, line.Length - 1).Trim();
                    _labels[label] = i;
                }
                else if (line.StartsWith("alloc_array "))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && long.TryParse(parts[2], out long bSize))
                    {
                        _arrayBounds[parts[1]] = bSize;
                    }
                }
            }

            // 2. Pass 2: تنفيذ التعليمات
            int pc = 0;
            int totalSteps = 0;
            const int MAX_STEPS = 500000;

            while (pc < _tac.Count && totalSteps++ < MAX_STEPS)
            {
                string line = _tac[pc].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("alloc_array "))
                {
                    pc++;
                    continue;
                }

                if (line.EndsWith(":"))
                {
                    pc++;
                    continue;
                }

                // Read
                if (line.StartsWith("read "))
                {
                    string dest = ResolveArrayName(line.Substring(5).Trim());
                    if (_inputTokens.Count > 0)
                    {
                        string tok = _inputTokens.Dequeue();
                        if (double.TryParse(tok, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                        {
                            _variables[dest] = val;
                            _stringVars.Remove(dest);
                        }
                        else
                        {
                            _stringVars[dest] = tok;
                            _variables[dest] = 0.0;
                        }

                        if (_aliases.TryGetValue(dest, out var aliasTarget))
                        {
                            string tVar = ResolveArrayName(aliasTarget);
                            if (tVar != dest)
                            {
                                if (_variables.ContainsKey(dest)) _variables[tVar] = _variables[dest];
                                if (_stringVars.ContainsKey(dest)) _stringVars[tVar] = _stringVars[dest];
                            }
                        }
                    }
                    pc++;
                    continue;
                }

                // Print
                if (line.StartsWith("print "))
                {
                    string expr = line.Substring(6).Trim();
                    string resolved = ResolveArrayName(expr);
                    if (_stringVars.TryGetValue(resolved, out var sVal))
                    {
                        output.AppendLine(sVal);
                    }
                    else if (_stringVars.TryGetValue(expr, out var sVal2))
                    {
                        output.AppendLine(sVal2);
                    }
                    else if ((expr.StartsWith("\"") && expr.EndsWith("\"")) || (expr.StartsWith("'") && expr.EndsWith("'")))
                    {
                        output.AppendLine(expr.Substring(1, expr.Length - 2));
                    }
                    else
                    {
                        double val = EvaluateExpr(expr);
                        output.AppendLine((val == Math.Floor(val)) ? ((long)val).ToString() : val.ToString("G", CultureInfo.InvariantCulture));
                    }
                    pc++;
                }
                else if (line.StartsWith("print_raw "))
                {
                    string expr = line.Substring(10).Trim();
                    string resolved = ResolveArrayName(expr);
                    if (_stringVars.TryGetValue(resolved, out var sVal))
                    {
                        output.Append(sVal);
                    }
                    else if (_stringVars.TryGetValue(expr, out var sVal2))
                    {
                        output.Append(sVal2);
                    }
                    else if ((expr.StartsWith("\"") && expr.EndsWith("\"")) || (expr.StartsWith("'") && expr.EndsWith("'")))
                    {
                        output.Append(expr.Substring(1, expr.Length - 2));
                    }
                    else
                    {
                        double val = EvaluateExpr(expr);
                        output.Append((val == Math.Floor(val)) ? ((long)val).ToString() : val.ToString("G", CultureInfo.InvariantCulture));
                    }
                    pc++;
                }
                // Read
                else if (line.StartsWith("read "))
                {
                    string dest = ResolveArrayName(line.Substring(5).Trim());
                    if (_inputTokens.Count > 0)
                    {
                        string token = _inputTokens.Dequeue();
                        if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double readNum))
                        {
                            _variables[dest] = readNum;
                            _stringVars.Remove(dest);
                        }
                        else
                        {
                            _stringVars[dest] = token;
                            _variables[dest] = 0.0;
                        }
                    }
                    pc++;
                }
                // Call
                else if (line.StartsWith("call "))
                {
                    string target = line.Substring(5).Trim();
                    _callStack.Push(pc + 1);
                    _aliasStack.Push(new Dictionary<string, string>(_aliases));
                    _aliases.Clear();
                    if (_labels.TryGetValue(target, out int targetPc))
                    {
                        pc = targetPc;
                    }
                    else
                    {
                        pc++;
                    }
                }
                // Return
                else if (line == "return")
                {
                    if (_aliasStack.Count > 0)
                    {
                        _aliases = _aliasStack.Pop();
                    }
                    else
                    {
                        _aliases.Clear();
                    }
                    if (_callStack.Count > 0)
                    {
                        pc = _callStack.Pop();
                    }
                    else
                    {
                        pc++;
                    }
                }
                // Goto
                else if (line.StartsWith("goto "))
                {
                    string target = line.Substring(5).Trim();
                    if (_labels.TryGetValue(target, out int targetPc)) pc = targetPc;
                    else pc++;
                }
                // If ... goto
                else if (line.StartsWith("if "))
                {
                    int gotoPos = line.IndexOf(" goto ", StringComparison.Ordinal);
                    string cond = line.Substring(3, gotoPos - 3).Trim();
                    string target = line.Substring(gotoPos + 6).Trim();

                    double condVal = EvaluateExpr(cond);
                    if (condVal != 0.0)
                    {
                        if (_labels.TryGetValue(target, out int targetPc)) pc = targetPc;
                        else pc++;
                    }
                    else
                    {
                        pc++;
                    }
                }
                // Param
                else if (line.StartsWith("param "))
                {
                    string arg = line.Substring(6).Trim();
                    arg = ResolveArrayName(arg);
                    if (_aliases.TryGetValue(arg, out var aliased)) arg = aliased;
                    _paramQueue.Enqueue(arg);
                    pc++;
                }
                // Pop param by reference: dest = pop_param_ref
                else if (line.Contains("= pop_param_ref"))
                {
                    int eqPos = line.IndexOf('=');
                    string dest = line.Substring(0, eqPos).Trim();
                    if (_paramQueue.Count > 0)
                    {
                        string actual = _paramQueue.Dequeue();
                        bool isIdent = !string.IsNullOrEmpty(actual) && (char.IsLetter(actual[0]) || actual[0] == '_' || (actual[0] >= '\u0600' && actual[0] <= '\u06FF'));
                        if (isIdent)
                        {
                            _aliases[dest] = actual;
                            if (_arrayBounds.TryGetValue(actual, out var b)) _arrayBounds[dest] = b;
                        }
                        _variables[dest] = EvaluateExpr(actual);
                        if (_stringVars.TryGetValue(actual, out var s)) _stringVars[dest] = s;
                    }
                    pc++;
                }
                // Pop param by value: dest = pop_param
                else if (line.Contains("= pop_param"))
                {
                    int eqPos = line.IndexOf('=');
                    string dest = line.Substring(0, eqPos).Trim();
                    if (_paramQueue.Count > 0)
                    {
                        string actual = _paramQueue.Dequeue();
                        // Call-by-value: NO ALIAS CREATED
                        if (_arrayBounds.TryGetValue(actual, out var b)) _arrayBounds[dest] = b;
                        _variables[dest] = EvaluateExpr(actual);
                        if (_stringVars.TryGetValue(actual, out var s)) _stringVars[dest] = s;

                        // Deep copy array elements locally if actual was an array
                        string prefix = actual + "[";
                        var toCopyNum = new List<KeyValuePair<string, double>>();
                        foreach (var kv in _variables)
                        {
                            if (kv.Key.StartsWith(prefix))
                            {
                                string suffix = kv.Key.Substring(actual.Length);
                                toCopyNum.Add(new KeyValuePair<string, double>(dest + suffix, kv.Value));
                            }
                        }
                        foreach (var kv in toCopyNum) _variables[kv.Key] = kv.Value;

                        var toCopyStr = new List<KeyValuePair<string, string>>();
                        foreach (var kv in _stringVars)
                        {
                            if (kv.Key.StartsWith(prefix))
                            {
                                string suffix = kv.Key.Substring(actual.Length);
                                toCopyStr.Add(new KeyValuePair<string, string>(dest + suffix, kv.Value));
                            }
                        }
                        foreach (var kv in toCopyStr) _stringVars[kv.Key] = kv.Value;
                    }
                    pc++;
                }
                // Assignment: dest = expr
                else if (line.Contains("="))
                {
                    int eqPos = line.IndexOf('=');
                    string dest = ResolveArrayName(line.Substring(0, eqPos).Trim());
                    string expr = line.Substring(eqPos + 1).Trim();

                    if ((expr.StartsWith("\"") && expr.EndsWith("\"")) || (expr.StartsWith("'") && expr.EndsWith("'")))
                    {
                        string strVal = expr.Substring(1, expr.Length - 2);
                        _stringVars[dest] = strVal;
                        _variables[dest] = 0.0;
                        if (_aliases.TryGetValue(dest, out var aliasTarget))
                        {
                            string tVar = ResolveArrayName(aliasTarget);
                            if (tVar != dest) _stringVars[tVar] = strVal;
                        }
                    }
                    else if (_stringVars.TryGetValue(expr, out var strVarVal))
                    {
                        _stringVars[dest] = strVarVal;
                        _variables[dest] = 0.0;
                        if (_aliases.TryGetValue(dest, out var aliasTarget))
                        {
                            string tVar = ResolveArrayName(aliasTarget);
                            if (tVar != dest) _stringVars[tVar] = strVarVal;
                        }
                    }
                    else
                    {
                        double res = EvaluateExpr(expr);
                        _variables[dest] = res;
                        _stringVars.Remove(dest);

                        if (_aliases.TryGetValue(dest, out var aliasTarget))
                        {
                            string tVar = ResolveArrayName(aliasTarget);
                            if (tVar != dest)
                            {
                                _variables[tVar] = res;
                                _stringVars.Remove(tVar);
                            }
                        }
                    }
                    pc++;
                }
                else
                {
                    pc++;
                }
            }

            return output.ToString();
        }
    }
}
