using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LanguageEditor
{
    /// <summary>
    /// نافذة موجه أوامر فيجوال ستوديو المستقلة (Visual Studio Debug Console)
    /// - تظهر كنافذة خارجية مستقلة تماماً
    /// - تدعم عرض النصوص العربية وتشكيل الحروف بدقة 100% دون أي مربعات فارغة
    /// - تقبل الإدخال التفاعلي لتعليمة اقرا لكافة المتغيرات تباعاً بالضغط على Enter دون تعليق
    /// - تغلق تلقائياً عند إغلاق البرنامج الرئيسي أو عند الضغط على أي مفتاح بعد انتهاء التنفيذ
    /// </summary>
    public class VsConsoleForm : Form
    {
        private readonly RichTextBox _terminal;
        private readonly List<string> _tac;
        private readonly string _programName;
        private readonly Dictionary<string, double> _variables = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _labels = new(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<int> _callStack = new();
        private readonly Queue<string> _paramQueue = new();
        private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly CancellationTokenSource _cts = new();

        private TaskCompletionSource<string>? _inputTcs;
        private int _inputPromptStartPos = -1;
        private bool _isFinished = false;

        public VsConsoleForm(List<string> tac, string programName)
        {
            _tac = tac ?? new List<string>();
            _programName = string.IsNullOrEmpty(programName) ? "arabic_program.arb" : Path.GetFileName(programName);

            this.Text = "Administrator: Visual Studio Debug Console";
            this.Size = new Size(880, 520);
            this.MinimumSize = new Size(600, 360);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(12, 12, 12);
            this.ForeColor = Color.FromArgb(220, 220, 220);
            this.RightToLeft = RightToLeft.No; // نمط كونسول قياسي مع نصوص عربية منسابة
            this.ShowInTaskbar = true;

            _terminal = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 12, 12),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 11.5F, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                ReadOnly = true, // للقراءة فقط افتراضياً ويفتح عند طلب الإدخال
                ScrollBars = RichTextBoxScrollBars.Vertical,
                ShortcutsEnabled = true
            };

            _terminal.KeyDown += Terminal_KeyDown;
            _terminal.KeyPress += Terminal_KeyPress;
            _terminal.MouseDown += (s, e) =>
            {
                if (_inputTcs != null && !_inputTcs.Task.IsCompleted)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (_terminal.SelectionStart < _inputPromptStartPos)
                        {
                            _terminal.SelectionStart = _terminal.TextLength;
                            _terminal.SelectionLength = 0;
                        }
                    }));
                }
            };

            this.Controls.Add(_terminal);

            this.FormClosing += (s, e) =>
            {
                _cts.Cancel();
                _inputTcs?.TrySetCanceled();
            };

            this.Shown += async (s, e) =>
            {
                await RunExecutionAsync();
            };
        }

        private void AppendText(string text, Color color)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendText(text, color)));
                return;
            }

            _terminal.SelectionStart = _terminal.TextLength;
            _terminal.SelectionLength = 0;
            _terminal.SelectionColor = color;
            _terminal.AppendText(text);
            _terminal.SelectionColor = _terminal.ForeColor;
            _terminal.SelectionStart = _terminal.TextLength;
            _terminal.SelectionLength = 0;
            _terminal.ScrollToCaret();
        }

        private async Task RunExecutionAsync()
        {
            AppendText("Microsoft Windows [Version 10.0.22631]\n", Color.FromArgb(200, 200, 200));
            AppendText("(c) Microsoft Corporation. All rights reserved.\n\n", Color.FromArgb(200, 200, 200));
            AppendText($"> CompilerProject_CPP.exe \"{_programName}\"\n", Color.FromArgb(220, 220, 220));
            AppendText("--------------------------------------------------------------------------------\n", Color.FromArgb(100, 100, 100));

            // جمع الملصقات Labels
            for (int i = 0; i < _tac.Count; i++)
            {
                string line = _tac[i].Trim();
                if (line.EndsWith(":") && !line.StartsWith("//"))
                {
                    string label = line.Substring(0, line.Length - 1).Trim();
                    _labels[label] = i;
                }
            }

            int pc = 0;
            try
            {
                while (pc < _tac.Count && !_cts.Token.IsCancellationRequested)
                {

                    string rawLine = _tac[pc].Trim();

                    if (string.IsNullOrEmpty(rawLine) || rawLine.StartsWith("//") || rawLine.EndsWith(":"))
                    {
                        pc++;
                        continue;
                    }

                    // أمر الطباعة دون سطر جديد print_raw
                    if (rawLine.StartsWith("print_raw "))
                    {
                        string arg = rawLine.Substring(10).Trim();
                        string lineOut;

                        string resolvedArg = ResolveArrayName(arg);
                        if (arg.StartsWith("\"") && arg.EndsWith("\"") && arg.Length >= 2)
                        {
                            lineOut = arg.Substring(1, arg.Length - 2);
                        }
                        else if (_variables.TryGetValue(resolvedArg, out double v))
                        {
                            lineOut = (v == Math.Floor(v)) ? ((long)v).ToString() : v.ToString(CultureInfo.InvariantCulture);
                        }
                        else if (_variables.TryGetValue(arg, out double v2))
                        {
                            lineOut = (v2 == Math.Floor(v2)) ? ((long)v2).ToString() : v2.ToString(CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            double ev = EvaluateExpr(arg);
                            lineOut = (ev == Math.Floor(ev)) ? ((long)ev).ToString() : ev.ToString(CultureInfo.InvariantCulture);
                        }

                        AppendText(lineOut, Color.FromArgb(235, 235, 235));
                        pc++;
                    }
                    // 1. أمر الطباعة print
                    else if (rawLine.StartsWith("print "))
                    {
                        string arg = rawLine.Substring(6).Trim();
                        string lineOut;

                        string resolvedArg = ResolveArrayName(arg);
                        if (arg.StartsWith("\"") && arg.EndsWith("\"") && arg.Length >= 2)
                        {
                            lineOut = arg.Substring(1, arg.Length - 2);
                        }
                        else if (_variables.TryGetValue(resolvedArg, out double v))
                        {
                            lineOut = (v == Math.Floor(v)) ? ((long)v).ToString() : v.ToString(CultureInfo.InvariantCulture);
                        }
                        else if (_variables.TryGetValue(arg, out double v2))
                        {
                            lineOut = (v2 == Math.Floor(v2)) ? ((long)v2).ToString() : v2.ToString(CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            double ev = EvaluateExpr(arg);
                            lineOut = (ev == Math.Floor(ev)) ? ((long)ev).ToString() : ev.ToString(CultureInfo.InvariantCulture);
                        }

                        AppendText(lineOut + "\n", Color.FromArgb(235, 235, 235));
                        pc++;
                    }
                    // أمر استدعاء الإجراء call
                    else if (rawLine.StartsWith("call "))
                    {
                        string target = rawLine.Substring(5).Trim();
                        _callStack.Push(pc + 1);
                        pc = _labels.TryGetValue(target, out int targetPc) ? targetPc : pc + 1;
                    }
                    // أمر العودة من الإجراء return
                    else if (rawLine == "return")
                    {
                        _aliases.Clear();
                        if (_callStack.Count > 0)
                        {
                            pc = _callStack.Pop();
                        }
                        else
                        {
                            pc++;
                        }
                    }
                    // 2. أمر القفز goto
                    else if (rawLine.StartsWith("goto "))
                    {
                        string target = rawLine.Substring(5).Trim();
                        pc = _labels.TryGetValue(target, out int targetPc) ? targetPc : pc + 1;
                    }
                    // 3. أمر الشرط if ... goto
                    else if (rawLine.StartsWith("if "))
                    {
                        int gPos = rawLine.IndexOf(" goto ");
                        if (gPos > 3)
                        {
                            string cond = rawLine.Substring(3, gPos - 3).Trim();
                            string target = rawLine.Substring(gPos + 6).Trim();
                            double condVal = EvaluateExpr(cond);
                            if (condVal != 0.0)
                            {
                                pc = _labels.TryGetValue(target, out int targetPc) ? targetPc : pc + 1;
                            }
                            else
                            {
                                pc++;
                            }
                        }
                        else
                        {
                            pc++;
                        }
                    }
                    // 4. أمر القراءة من لوحة المفاتيح read
                    else if (rawLine.StartsWith("read "))
                    {
                        string dest = rawLine.Substring(5).Trim();
                        dest = ResolveArrayName(dest);
                        string prompt = $">> [اقرا] ادخل قيمة لـ ({dest}): ";

                        double val = 0.0;
                        bool inputAccepted = false;
                        while (!inputAccepted)
                        {
                            AppendText(prompt, Color.FromArgb(255, 215, 0));

                            _inputPromptStartPos = _terminal.TextLength;
                            _terminal.SelectionStart = _terminal.TextLength;
                            _terminal.SelectionLength = 0;
                            _terminal.ReadOnly = false;
                            _terminal.Focus();

                            _inputTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                            string inputStr = await _inputTcs.Task;
                            string trimmed = inputStr?.Trim() ?? "";

                            if (string.IsNullOrEmpty(trimmed))
                            {
                                AppendText("⚠️ [تنبيه] لم يتم إدخال أي قيمة! يرجى إدخال قيمة عددية:\n", Color.FromArgb(255, 180, 50));
                                continue;
                            }

                            if (TryParseAndSanitizeNumber(trimmed, out double parsedVal))
                            {
                                val = parsedVal;
                                inputAccepted = true;
                            }
                            else
                            {
                                double evalVal = EvaluateExpr(NormalizeEasternDigits(trimmed));
                                if (evalVal != 0.0 || trimmed == "0" || trimmed == "0.0")
                                {
                                    val = evalVal;
                                    inputAccepted = true;
                                }
                                else
                                {
                                    AppendText($"⚠️ [خطأ في الإدخال] القيمة المدخلة (\"{trimmed}\") غير صحيحة! يرجى إدخال رقم صحيح:\n", Color.FromArgb(255, 100, 100));
                                }
                            }
                        }

                        _variables[dest] = val;
                        pc++;
                    }
                    // 5. أمر تمرير المعاملات param
                    else if (rawLine.StartsWith("param "))
                    {
                        string arg = rawLine.Substring(6).Trim();
                        _paramQueue.Enqueue(arg);
                        pc++;
                    }
                    // 6. أمر استلام المعاملات pop_param
                    else if (rawLine.Contains("= pop_param"))
                    {
                        int eqPos = rawLine.IndexOf('=');
                        string dest = rawLine.Substring(0, eqPos).Trim();
                        if (_paramQueue.Count > 0)
                        {
                            string actual = _paramQueue.Dequeue();
                            _aliases[dest] = actual;
                            _variables[dest] = EvaluateExpr(actual);
                        }
                        pc++;
                    }
                    // 7. أمر الإسناد dest = expr
                    else if (rawLine.Contains("="))
                    {
                        int eqPos = rawLine.IndexOf('=');
                        string dest = rawLine.Substring(0, eqPos).Trim();
                        dest = ResolveArrayName(dest);
                        if (_aliases.TryGetValue(dest, out string? realDest))
                        {
                            dest = realDest;
                        }
                        string expr = rawLine.Substring(eqPos + 1).Trim();
                        double res = EvaluateExpr(expr);
                        _variables[dest] = res;
                        pc++;
                    }
                    else
                    {
                        pc++;
                    }
                }
            }
            catch (Exception ex)
            {
                AppendText($"\n❌ خطأ أثناء التنفيذ: {ex.Message}\n", Color.FromArgb(255, 100, 100));
            }

            _terminal.ReadOnly = true;

            if (_cts.Token.IsCancellationRequested)
            {
                AppendText("\n--------------------------------------------------------------------------------\n", Color.FromArgb(100, 100, 100));
                AppendText("Process was cancelled by user.\n", Color.FromArgb(255, 120, 120));
                AppendText("Press any key to close this window . . .\n", Color.FromArgb(180, 180, 180));
            }
            else
            {
                AppendText("\n--------------------------------------------------------------------------------\n", Color.FromArgb(100, 100, 100));
                AppendText("Process exited with code 0 (0x0).\n", Color.FromArgb(200, 200, 200));
                AppendText("Press any key to close this window . . .\n", Color.FromArgb(180, 180, 180));
            }

            _isFinished = true;
            _terminal.SelectionStart = _terminal.TextLength;
            _terminal.SelectionLength = 0;
        }

        private static string NormalizeEasternDigits(string input)
        {
            if (string.IsNullOrEmpty(input)) return "0";
            var sb = new StringBuilder();
            foreach (char c in input)
            {
                if (c >= '٠' && c <= '٩') sb.Append((char)('0' + (c - '٠')));
                else if (c == '،' || c == ',') sb.Append('.');
                else sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        private static bool TryParseAndSanitizeNumber(string rawInput, out double val)
        {
            val = 0.0;
            if (string.IsNullOrWhiteSpace(rawInput)) return false;

            string s = rawInput.Trim();

            // 1. تطبيع الأرقام المشرقية (٠-٩) والفواصل
            s = NormalizeEasternDigits(s);

            // 2. التحقق من وجود أرقام
            bool hasDigits = false;
            foreach (char c in s)
            {
                if (char.IsDigit(c)) { hasDigits = true; break; }
            }
            if (!hasDigits) return false;

            // 3. معالجة الإشارة
            string sign = "";
            if (s.StartsWith("+") || s.StartsWith("-"))
            {
                sign = s.Substring(0, 1);
                s = s.Substring(1).TrimStart();
            }

            // 4. إزالة النقاط والفواصل الزائدة من البداية والنهاية (مثل ..4 أو 4..)
            s = s.TrimStart('.', ',').TrimEnd('.', ',');
            if (string.IsNullOrWhiteSpace(s)) return false;

            // 5. بناء الرقم مع السماح بنقطة عشرية واحدة فقط وتجاهل المسافات الداخلية العرضية
            var sb = new StringBuilder(sign);
            bool dotSeen = false;
            foreach (char c in s)
            {
                if (c == '.' || c == ',')
                {
                    if (!dotSeen)
                    {
                        sb.Append('.');
                        dotSeen = true;
                    }
                }
                else if (char.IsDigit(c))
                {
                    sb.Append(c);
                }
                else if (c == ' ')
                {
                    continue; // تجاهل المسافات داخل الأرقام
                }
                else
                {
                    return false; // احتواء حروف أو رموز غير مقبولة
                }
            }

            string finalNum = sb.ToString();
            return double.TryParse(finalNum, NumberStyles.Float, CultureInfo.InvariantCulture, out val);
        }

        private string ResolveArrayName(string name)
        {
            string s = name.Trim();
            if (string.IsNullOrEmpty(s) || !s.EndsWith("]")) return s;
            int openBracket = s.IndexOf('[');
            int closeBracket = s.LastIndexOf(']');
            if (openBracket > 0 && closeBracket > openBracket)
            {
                string arrayName = s.Substring(0, openBracket).Trim();
                string indexStr = s.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();
                if (_aliases.TryGetValue(arrayName, out string? realArr))
                {
                    arrayName = realArr;
                }
                double idxVal = EvaluateExpr(indexStr);
                long idxInt = (long)Math.Round(idxVal);
                return $"{arrayName}[{idxInt}]";
            }
            if (_aliases.TryGetValue(s, out string? realName)) return realName;
            return s;
        }

        private double EvaluateExpr(string expr)
        {
            string s = expr.Trim();
            if (string.IsNullOrEmpty(s)) return 0.0;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double directVal))
                return directVal;

            if (s is "صواب" or "صح" or "true") return 1.0;
            if (s is "خطأ" or "خطا" or "false") return 0.0;

            // العمليات الثنائية
            string[] ops = new[] { "||", "&&", "==", "!=", "<=", ">=", "<", ">", "+", "-", "*", "/", "%" };
            foreach (var op in ops)
            {
                int opIdx = -1;
                if (op == "+" || op == "-")
                {
                    // البحث من اليمين لليسار مع تجنب الإشارة السالبة الأحادية
                    for (int i = s.Length - 1; i > 0; i--)
                    {
                        if (s[i] == op[0] && s[i - 1] != '+' && s[i - 1] != '-' && s[i - 1] != '*' && s[i - 1] != '/' && s[i - 1] != '(')
                        {
                            opIdx = i;
                            break;
                        }
                    }
                }
                else
                {
                    opIdx = s.IndexOf(op, StringComparison.Ordinal);
                }

                if (opIdx > 0 && opIdx < s.Length - 1)
                {
                    string left = s.Substring(0, opIdx).Trim();
                    string right = s.Substring(opIdx + op.Length).Trim();
                    double lv = EvaluateExpr(left);
                    double rv = EvaluateExpr(right);

                    return op switch
                    {
                        "+" => lv + rv,
                        "-" => lv - rv,
                        "*" => lv * rv,
                        "/" => rv != 0 ? lv / rv : 0.0,
                        "%" => rv != 0 ? lv % rv : 0.0,
                        "==" => lv == rv ? 1.0 : 0.0,
                        "!=" => lv != rv ? 1.0 : 0.0,
                        "<" => lv < rv ? 1.0 : 0.0,
                        "<=" => lv <= rv ? 1.0 : 0.0,
                        ">" => lv > rv ? 1.0 : 0.0,
                        ">=" => lv >= rv ? 1.0 : 0.0,
                        "&&" => (lv != 0 && rv != 0) ? 1.0 : 0.0,
                        "||" => (lv != 0 || rv != 0) ? 1.0 : 0.0,
                        _ => 0.0
                    };
                }
            }

            if (s.StartsWith("-")) return -EvaluateExpr(s.Substring(1));

            string resolved = ResolveArrayName(s);
            if (_aliases.TryGetValue(resolved, out string? realRes)) resolved = realRes;
            if (_variables.TryGetValue(resolved, out double varVal)) return varVal;
            if (_variables.TryGetValue(s, out double directVarVal)) return directVarVal;
            if (_aliases.TryGetValue(s, out string? directAlias) && _variables.TryGetValue(directAlias, out double aliasVal)) return aliasVal;

            return 0.0;
        }

        private void Terminal_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_isFinished)
            {
                this.Close();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (_inputTcs != null && !_inputTcs.Task.IsCompleted)
            {
                // ضمان وجود المؤشر في منطقة الإدخال بعد نص الرسالة
                if (_terminal.SelectionStart < _inputPromptStartPos)
                {
                    _terminal.SelectionStart = _terminal.TextLength;
                    _terminal.SelectionLength = 0;
                }

                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;

                    string fullText = _terminal.Text;
                    string userInput = "";
                    if (_inputPromptStartPos >= 0 && _inputPromptStartPos <= fullText.Length)
                    {
                        userInput = fullText.Substring(_inputPromptStartPos).Trim();
                    }

                    _terminal.ReadOnly = true;
                    AppendText("\n", _terminal.ForeColor);

                    var tcs = _inputTcs;
                    _inputTcs = null;
                    _inputPromptStartPos = -1;
                    tcs?.TrySetResult(userInput);
                    return;
                }

                if (e.KeyCode == Keys.Back)
                {
                    if (_terminal.SelectionStart <= _inputPromptStartPos)
                    {
                        e.SuppressKeyPress = true;
                        e.Handled = true;
                        return;
                    }
                }
                else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up)
                {
                    if (_terminal.SelectionStart <= _inputPromptStartPos)
                    {
                        e.SuppressKeyPress = true;
                        e.Handled = true;
                        return;
                    }
                }
                else if (e.KeyCode == Keys.Home)
                {
                    _terminal.SelectionStart = _inputPromptStartPos;
                    _terminal.SelectionLength = 0;
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    return;
                }

                // السماح التام بكافة المفاتيح الأخرى (أرقام، حروف، مسافات) دون اعتراض
                return;
            }

            // خارج وضع الإدخال
            if (e.Control && e.KeyCode == Keys.C) return;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) return;

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void Terminal_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (_isFinished)
            {
                this.Close();
                e.Handled = true;
                return;
            }

            if (_inputTcs != null && !_inputTcs.Task.IsCompleted)
            {
                if (_terminal.SelectionStart < _inputPromptStartPos)
                {
                    _terminal.SelectionStart = _terminal.TextLength;
                    _terminal.SelectionLength = 0;
                }
                // السماح بالطباعة
                return;
            }

            e.Handled = true;
        }
    }
}
