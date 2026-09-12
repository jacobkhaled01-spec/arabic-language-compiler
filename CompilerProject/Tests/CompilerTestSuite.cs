using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CompilerProject.Common;
using CompilerProject.LexicalAnalysis;
using CompilerProject.SyntaxAnalysis;
using CompilerProject.SemanticAnalysis;
using CompilerProject.IntermediateCode;
using CompilerProject.CodeGeneration;
using CompilerProject.Models;

namespace CompilerProject.Tests
{
    /// <summary>
    /// منظومة الاختبارات الشاملة لفحص كل جزء بشكل مستقل على جميع الأخطاء والاحتمالات
    /// (Exhaustive Unit, Error, and Integration Test Suite)
    /// </summary>
    public static class CompilerTestSuite
    {
        public static void RunAllTests()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=========================================================================================");
            Console.WriteLine("🔬 بدء الفحص الشامل لجميع مكونات المترجم العربي على كافة الاحتمالات والأخطاء");
            Console.WriteLine("=========================================================================================");
            Console.WriteLine();

            int totalTests = 0;
            int passedTests = 0;

            // =========================================================================
            // 1. اختبارات المحلل المعجمي المنفردة (Lexer Independent Tests)
            // =========================================================================
            Console.WriteLine("🔹 [المجموعة 1]: اختبارات المحلل المعجمي (Lexer Tests)");
            RunTest("1.1 استخراج الكلمات المحجوزة والمعرفات والرموز", TestLexerBasic, ref totalTests, ref passedTests);
            RunTest("1.2 تطبيع الأرقام المشرقية (٠-٩) وتحويلها لأرقام نظامية", TestLexerEasternDigits, ref totalTests, ref passedTests);
            RunTest("1.3 قراءة السلاسل النصية بعلامات التنصيص المنحنية الذكية “”", TestLexerSmartQuotes, ref totalTests, ref passedTests);
            RunTest("1.4 تجاهل التعليقات الفردية والممتدة (// و /* */)", TestLexerComments, ref totalTests, ref passedTests);
            RunTest("1.5 فحص الأعداد العشرية الحقيقية (Real Numbers)", TestLexerRealNumbers, ref totalTests, ref passedTests);
            Console.WriteLine();

            // =========================================================================
            // 2. اختبارات المحلل النحوي وشجرة الإعراب (Parser & AST Tests)
            // =========================================================================
            Console.WriteLine("🔹 [المجموعة 2]: اختبارات المحلل النحوي وشجرة الإعراب (Parser & AST Tests)");
            RunTest("2.1 إعراب رأس البرنامج والكتلة البرمجية الكاملة", TestParserProgramStructure, ref totalTests, ref passedTests);
            RunTest("2.2 إعراب تعريفات الثوابت والمتغيرات والأنواع (قائمة وسجل)", TestParserDeclarations, ref totalTests, ref passedTests);
            RunTest("2.3 إعراب الجمل الشرطية والحلقات الثلاث (اذا، طالما، اعد، كرر)", TestParserStatements, ref totalTests, ref passedTests);
            RunTest("2.4 إعراب التعابير الحسابية والمنطقية المركبة وأسبقية العمليات", TestParserExpressions, ref totalTests, ref passedTests);
            Console.WriteLine();

            // =========================================================================
            // 3. اختبارات كشف الأخطاء النحوية (Syntax Error Negative Tests)
            // =========================================================================
            Console.WriteLine("🔹 [المجموعة 3]: اختبارات كشف الأخطاء النحوية (Syntax Error Negative Tests)");
            RunTest("3.1 كشف خطأ غياب كلمة 'برنامج' في البداية", TestSyntaxErrorMissingProgramKeyword, ref totalTests, ref passedTests);
            RunTest("3.2 كشف خطأ نسيان الفاصلة المنقوطة '؛' بعد اسم البرنامج", TestSyntaxErrorMissingSemicolon, ref totalTests, ref passedTests);
            RunTest("3.3 كشف خطأ تعليمة غير مكتملة (معرف بمفرده 'س' بدون '=' أو '(')", TestSyntaxErrorIncompleteStatement, ref totalTests, ref passedTests);
            RunTest("3.4 كشف خطأ غياب كلمة 'فان' في الجملة الشرطية", TestSyntaxErrorMissingThenKeyword, ref totalTests, ref passedTests);
            RunTest("3.5 كشف خطأ غياب كلمة 'استمر' في حلقة طالما", TestSyntaxErrorMissingWhileKeyword, ref totalTests, ref passedTests);
            RunTest("3.6 كشف خطأ غياب كلمة 'حتى' في حلقة أعد", TestSyntaxErrorMissingUntilKeyword, ref totalTests, ref passedTests);
            RunTest("3.7 كشف خطأ تعبير حسابي غير سليم (10 + * 5)", TestSyntaxErrorMalformedExpression, ref totalTests, ref passedTests);
            RunTest("3.8 كشف خطأ نسيان النقطة '.' في نهاية البرنامج", TestSyntaxErrorMissingDot, ref totalTests, ref passedTests);
            Console.WriteLine();

            // =========================================================================
            // 4. اختبارات جدول الرموز والتحليل الدلالي (Symbol Table & Semantic Tests)
            // =========================================================================
            Console.WriteLine("🔹 [المجموعة 4]: اختبارات جدول الرموز والتحليل الدلالي (Semantic Tests)");
            RunTest("4.1 إضافة واسترجاع الرموز وتتبع أسطر التعريف والاستخدام", TestSymbolTableTracking, ref totalTests, ref passedTests);
            RunTest("4.2 كشف خطأ استخدام متغير غير معرف في الكود", TestSemanticErrorUndeclaredVariable, ref totalTests, ref passedTests);
            RunTest("4.3 كشف خطأ محاولة تعديل قيمة ثابت برمجياً", TestSemanticErrorConstantModification, ref totalTests, ref passedTests);
            RunTest("4.4 كشف خطأ إعادة تعريف نفس المتغير في نفس النطاق", TestSemanticErrorDuplicateDeclaration, ref totalTests, ref passedTests);
            Console.WriteLine();

            // =========================================================================
            // 5. اختبارات توليد الكود الوسيط وأسمبلي والتشغيل (TAC, CodeGen & Execution)
            // =========================================================================
            Console.WriteLine("🔹 [المجموعة 5]: اختبارات الكود الوسيط والتجميع والتكامل (TAC & Execution Tests)");
            RunTest("5.1 توليد كود ثلاثي العناوين (TAC) للشروط والعمليات", TestTACGeneration, ref totalTests, ref passedTests);
            RunTest("5.2 توليد كود أسمبلي x86 و .NET CIL سليم", TestAssemblyGeneration, ref totalTests, ref passedTests);
            RunTest("5.3 تكامل كامل: حساب أسبقية العمليات (10 + 5 * 2 = 20)", TestIntegrationPrecedence, ref totalTests, ref passedTests);
            RunTest("5.4 تكامل كامل: حلقة العداد التراكمية كرر (المجموع = 10)", TestIntegrationForLoop, ref totalTests, ref passedTests);
            RunTest("5.5 تكامل كامل: حلقة أعد حتى ومضاعفة القيمة (الناتج = 6)", TestIntegrationRepeatUntil, ref totalTests, ref passedTests);
            Console.WriteLine();

            // ملخص التقرير النهائي
            Console.WriteLine("=========================================================================================");
            double percentage = (double)passedTests / totalTests * 100.0;
            Console.WriteLine($"📊 التقرير النهائي للاختبارات: نجح {passedTests} من أصل {totalTests} اختباراً (بنسبة {percentage:F1}%)");
            Console.WriteLine("=========================================================================================");
        }

        private static void RunTest(string testName, Func<bool> testFunc, ref int total, ref int passed)
        {
            total++;
            try
            {
                bool result = testFunc();
                if (result)
                {
                    passed++;
                    Console.WriteLine($"   ✔️ [نجح] {testName}");
                }
                else
                {
                    Console.WriteLine($"   ❌ [فشل] {testName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ [استثناء غير متوقع]: {testName} -> {ex.Message}");
            }
        }

        // --- المجموعة 1 ---
        private static bool TestLexerBasic()
        {
            string code = "برنامج فحص ؛ متغير س : صحيح ؛ { س = 10 ؛ } .";
            var tokens = new Lexer(code).Tokenize();
            return tokens.Count >= 10 && tokens.Any(t => t.Value == "برنامج" && t.Type == TokenType.Keyword);
        }

        private static bool TestLexerEasternDigits()
        {
            string code = "س = ٢٥ + ٥ ؛";
            var tokens = new Lexer(code).Tokenize();
            var numTokens = tokens.Where(t => t.Type == TokenType.Number).ToList();
            return numTokens.Count == 2 && numTokens[0].Value == "25" && numTokens[1].Value == "5";
        }

        private static bool TestLexerSmartQuotes()
        {
            string code = "اطبع ( “مرحبا بكم” ) ؛";
            var tokens = new Lexer(code).Tokenize();
            return tokens.Any(t => t.Type == TokenType.String && t.Value == "مرحبا بكم");
        }

        private static bool TestLexerComments()
        {
            string code = "// تعليق\nبرنامج تجربة ؛ /* تعليق ممتد */ { } .";
            var tokens = new Lexer(code).Tokenize();
            return tokens.Any(t => t.Value == "تجربة") && !tokens.Any(t => t.Value.Contains("تعليق"));
        }

        private static bool TestLexerRealNumbers()
        {
            string code = "س = 12.75 ؛";
            var tokens = new Lexer(code).Tokenize();
            return tokens.Any(t => t.Type == TokenType.Number && t.Value == "12.75");
        }

        // --- المجموعة 2 ---
        private static bool TestParserProgramStructure()
        {
            string code = "برنامج هيكل ؛ { } .";
            var ast = new Parser(new Lexer(code).Tokenize()).ParseProgram();
            return ast != null && ast.Value == "ProgramRoot" && ast.Name == "هيكل";
        }

        private static bool TestParserDeclarations()
        {
            string code = @"برنامج تعريفات ؛ 
ثابت الحد = 100 ؛ 
نوع درجات = قائمة [ 5 ] من صحيح ؛
نوع طالب = سجل { رقم : صحيح ؛ معدل : حقيقي } ؛
متغير س , ص : صحيح ؛
{ } .";
            var ast = new Parser(new Lexer(code).Tokenize()).ParseProgram();
            return ast != null && ast.Children[0].Children[0].Value == "Declarations";
        }

        private static bool TestParserStatements()
        {
            string code = @"برنامج شروط ؛
متغير س : صحيح ؛
{
    اذا ( س > 0 ) فان { س = 1 ؛ } والا { س = 0 ؛ } ؛
    طالما ( س < 10 ) استمر { س = س + 1 ؛ } ؛
    اعد { س = س - 1 ؛ } حتى ( س == 0 ) ؛
    كرر ( س = 1 الى 5 اضف 1 ) { اطبع ( س ) ؛ } ؛
} .";
            var ast = new Parser(new Lexer(code).Tokenize()).ParseProgram();
            return ast != null;
        }

        private static bool TestParserExpressions()
        {
            string code = @"برنامج تعابير ؛ متغير س : صحيح ؛ { س = 10 + 5 * 2 - ( 4 \ 2 ) ؛ } .";
            var ast = new Parser(new Lexer(code).Tokenize()).ParseProgram();
            return ast != null;
        }

        // --- المجموعة 3 (الأخطاء النحوية) ---
        private static bool TestSyntaxErrorMissingProgramKeyword()
        {
            try
            {
                string code = "بدء_بدون_كلمة_برنامج ؛ { } .";
                new Parser(new Lexer(code).Tokenize()).ParseProgram();
                return false; // يجب ألا ينجح
            }
            catch (Exception ex) { return ex.Message.Contains("برنامج"); }
        }

        private static bool TestSyntaxErrorMissingSemicolon()
        {
            try
            {
                string code = "برنامج اسم_بدون_فاصلة { } .";
                new Parser(new Lexer(code).Tokenize()).ParseProgram();
                return false;
            }
            catch (Exception ex) { return ex.Message.Contains("فاصلة منقوطة"); }
        }

        private static bool TestSyntaxErrorIncompleteStatement()
        {
            try
            {
                // جملة غير مكتملة: معرف س بمفرده
                string code = "برنامج فحص ؛ متغير س , ص : صحيح ؛ { س ص = 10 ؛ } .";
                new Parser(new Lexer(code).Tokenize()).ParseProgram();
                return false;
            }
            catch (Exception ex) { return ex.Message.Contains("تعليمة غير مكتملة"); }
        }

        private static bool TestSyntaxErrorMissingThenKeyword()
        {
            try
            {
                string code = "برنامج فحص ؛ متغير س : صحيح ؛ { اذا ( س > 0 ) { س = 1 ؛ } } .";
                new Parser(new Lexer(code).Tokenize()).ParseProgram();
                return false;
            }
            catch (Exception ex) { return ex.Message.Contains("فان"); }
        }

        private static bool TestSyntaxErrorMissingWhileKeyword()
        {
            try
            {
                string code = "برنامج فحص ؛ متغير س : صحيح ؛ { طالما ( س > 0 ) { س = 1 ؛ } } .";
                new Parser(new Lexer(code).Tokenize()).ParseProgram();
                return false;
            }
            catch (Exception ex) { return ex.Message.Contains("استمر"); }
        }

        private static bool TestSyntaxErrorMissingUntilKeyword()
        {
            try
            {
                string code = "برنامج فحص ؛ متغير س : صحيح ؛ { اعد { س = 1 ؛ } ( س > 0 ) ؛ } .";
                new Parser(new Lexer(code).Tokenize()).ParseProgram();
                return false;
            }
            catch (Exception ex) { return ex.Message.Contains("حتى"); }
        }

        private static bool TestSyntaxErrorMalformedExpression()
        {
            try
            {
                string code = "برنامج فحص ؛ متغير س : صحيح ؛ { س = 10 + * 5 ؛ } .";
                new Parser(new Lexer(code).Tokenize()).ParseProgram();
                return false;
            }
            catch { return true; }
        }

        private static bool TestSyntaxErrorMissingDot()
        {
            try
            {
                string code = "برنامج فحص ؛ { } ";
                new Parser(new Lexer(code).Tokenize()).ParseProgram();
                return false;
            }
            catch (Exception ex) { return ex.Message.Contains("نقطة"); }
        }

        // --- المجموعة 4 (التحليل الدلالي وجدول الرموز) ---
        private static bool TestSymbolTableTracking()
        {
            var st = new SymbolTable();
            st.Add("س", "صحيح", "متغير", 5);
            st.AddReference("س", 7);
            st.AddReference("س", 9);

            var sym = st.Lookup("س");
            return sym != null && sym.ReferencedLines.Count == 2 && sym.ReferencedLines.Contains(7);
        }

        private static bool TestSemanticErrorUndeclaredVariable()
        {
            string code = "برنامج فحص ؛ متغير س : صحيح ؛ { س = متغير_مجهول + 5 ؛ } .";
            var ast = new Parser(new Lexer(code).Tokenize()).ParseProgram();
            var st = new SymbolTable();
            var sa = new SemanticAnalyzer(st);
            bool ok = sa.Analyze(ast);
            return !ok && sa.Errors.Any(e => e.Contains("متغير_مجهول"));
        }

        private static bool TestSemanticErrorConstantModification()
        {
            string code = "برنامج فحص ؛ ثابت الحد = 50 ؛ { الحد = 70 ؛ } .";
            var ast = new Parser(new Lexer(code).Tokenize()).ParseProgram();
            var st = new SymbolTable();
            var sa = new SemanticAnalyzer(st);
            bool ok = sa.Analyze(ast);
            return !ok && sa.Errors.Any(e => e.Contains("الثابت"));
        }

        private static bool TestSemanticErrorDuplicateDeclaration()
        {
            string code = "برنامج فحص ؛ متغير س : صحيح ؛ س : حقيقي ؛ { س = 10 ؛ } .";
            var ast = new Parser(new Lexer(code).Tokenize()).ParseProgram();
            var st = new SymbolTable();
            var sa = new SemanticAnalyzer(st);
            bool ok = sa.Analyze(ast);
            return !ok && sa.Errors.Any(e => e.Contains("إعادة تعريف") || e.Contains("تعريف"));
        }

        // --- المجموعة 5 (TAC و التجميع والتكامل) ---
        private static bool TestTACGeneration()
        {
            string code = "برنامج فحص ؛ متغير س : صحيح ؛ { اذا ( س > 5 ) فان { س = 10 ؛ } ؛ } .";
            var ast = new Parser(new Lexer(code).Tokenize()).ParseProgram();
            var tac = new IntermediateCodeGenerator().Generate(ast);
            return tac.Any(t => t.Contains("if") && t.Contains("goto"));
        }

        private static bool TestAssemblyGeneration()
        {
            var tacList = new List<string> { "س = 10", "ص = 20", "t1 = س + ص", "print t1" };
            var asmGen = new AssemblyCodeGenerator(tacList);
            string asm = asmGen.GenerateX86Assembly();
            string cil = asmGen.GenerateCIL();
            return !string.IsNullOrEmpty(asm) && !string.IsNullOrEmpty(cil) && asm.Contains(".386") && cil.Contains(".assembly");
        }

        private static bool TestIntegrationPrecedence()
        {
            string code = "برنامج فحص ؛ متغير س : صحيح ؛ { س = 10 + 5 * 2 ؛ اطبع ( س ) ؛ } .";
            var res = CompilerRunner.Compile(code, false);
            return res.IsSuccess && res.ExecutionOutput.Contains("20");
        }

        private static bool TestIntegrationForLoop()
        {
            string code = "برنامج فحص ؛ متغير س , مجموع : صحيح ؛ { مجموع = 0 ؛ كرر ( س = 1 الى 4 اضف 1 ) { مجموع = مجموع + س ؛ } ؛ اطبع ( مجموع ) ؛ } .";
            var res = CompilerRunner.Compile(code, false);
            return res.IsSuccess && res.ExecutionOutput.Contains("10");
        }

        private static bool TestIntegrationRepeatUntil()
        {
            string code = "برنامج فحص ؛ متغير س : صحيح ؛ { س = 0 ؛ اعد { س = س + 2 ؛ } حتى ( س >= 6 ) ؛ اطبع ( س ) ؛ } .";
            var res = CompilerRunner.Compile(code, false);
            return res.IsSuccess && res.ExecutionOutput.Contains("6");
        }
    }
}
