#include "../../include/Tests/CompilerTestSuite.h"
#include "../../include/LexicalAnalysis/Lexer.h"
#include "../../include/SyntaxAnalysis/Parser.h"
#include "../../include/SemanticAnalysis/SemanticAnalyzer.h"
#include "../../include/IntermediateCode/IntermediateCodeGenerator.h"
#include "../../include/CodeGeneration/AssemblyCodeGenerator.h"
#include "../../include/CodeGeneration/CompilerRunner.h"
#include <iostream>
#include <functional>
#include <algorithm>

namespace CompilerCPP {

    static void RunTest(const std::string& testName, const std::function<bool()>& testFunc, int& total, int& passed) {
        total++;
        try {
            if (testFunc()) {
                passed++;
                std::cout << "   ✔️ [نجح] " << testName << "\n";
            } else {
                std::cout << "   ❌ [فشل] " << testName << "\n";
            }
        } catch (const std::exception& ex) {
            std::cout << "   ❌ [استثناء غير متوقع]: " << testName << " -> " << ex.what() << "\n";
        }
    }

    void CompilerTestSuite::RunAllTests() {
        std::cout << "=========================================================================================\n";
        std::cout << "🔬 [C++] بدء الفحص الشامل لجميع مكونات المترجم العربي على كافة الاحتمالات والأخطاء\n";
        std::cout << "=========================================================================================\n\n";

        int totalTests = 0;
        int passedTests = 0;

        // المجموعة 1
        std::cout << "🔹 [المجموعة 1]: اختبارات المحلل المعجمي (Lexer Tests)\n";
        RunTest("1.1 استخراج الكلمات المحجوزة والمعرفات والرموز", []() {
            std::string code = "برنامج فحص ؛ متغير س : صحيح ؛ { س = 10 ؛ } .";
            auto tokens = Lexer(code).Tokenize();
            return tokens.size() >= 10 && tokens[0].Value == "برنامج" && tokens[0].Type == TokenType::Keyword;
        }, totalTests, passedTests);

        RunTest("1.2 تطبيع الأرقام المشرقية (٠-٩) وتحويلها لأرقام نظامية", []() {
            std::string code = "س = ٢٥ + ٥ ؛";
            auto tokens = Lexer(code).Tokenize();
            int numCount = 0;
            for (const auto& t : tokens) {
                if (t.Type == TokenType::Number) {
                    if (numCount == 0 && t.Value != "25") return false;
                    if (numCount == 1 && t.Value != "5") return false;
                    numCount++;
                }
            }
            return numCount == 2;
        }, totalTests, passedTests);

        RunTest("1.3 قراءة السلاسل النصية بعلامات التنصيص المنحنية الذكية “”", []() {
            std::string code = "اطبع ( “مرحبا بكم” ) ؛";
            auto tokens = Lexer(code).Tokenize();
            for (const auto& t : tokens) {
                if (t.Type == TokenType::String && t.Value == "مرحبا بكم") return true;
            }
            return false;
        }, totalTests, passedTests);

        RunTest("1.4 تجاهل التعليقات الفردية والممتدة (// و /* */)", []() {
            std::string code = "// تعليق\nبرنامج تجربة ؛ /* تعليق ممتد */ { } .";
            auto tokens = Lexer(code).Tokenize();
            bool hasProg = false;
            for (const auto& t : tokens) {
                if (t.Value == "تجربة") hasProg = true;
                if (t.Value.find("تعليق") != std::string::npos) return false;
            }
            return hasProg;
        }, totalTests, passedTests);

        RunTest("1.5 فحص الأعداد العشرية الحقيقية (Real Numbers)", []() {
            std::string code = "س = 12.75 ؛";
            auto tokens = Lexer(code).Tokenize();
            for (const auto& t : tokens) {
                if (t.Type == TokenType::Number && t.Value == "12.75") return true;
            }
            return false;
        }, totalTests, passedTests);

        std::cout << "\n";

        // المجموعة 2
        std::cout << "🔹 [المجموعة 2]: اختبارات المحلل النحوي وشجرة الإعراب (Parser & AST Tests)\n";
        RunTest("2.1 إعراب رأس البرنامج والكتلة البرمجية الكاملة", []() {
            std::string code = "برنامج هيكل ؛ { } .";
            auto ast = Parser(Lexer(code).Tokenize()).ParseProgram();
            return ast && ast->Value == "ProgramRoot" && ast->Name == "هيكل";
        }, totalTests, passedTests);

        RunTest("2.2 إعراب تعريفات الثوابت والمتغيرات والأنواع (قائمة وسجل)", []() {
            std::string code = "برنامج تعريفات ؛ \nثابت الحد = 100 ؛ \nنوع درجات = قائمة [ 5 ] من صحيح ؛\nنوع طالب = سجل { رقم : صحيح ؛ معدل : حقيقي } ؛\nمتغير س , ص : صحيح ؛\n{ } .";
            auto ast = Parser(Lexer(code).Tokenize()).ParseProgram();
            return ast && !ast->Children.empty();
        }, totalTests, passedTests);

        RunTest("2.3 إعراب الجمل الشرطية والحلقات الثلاث (اذا، طالما، اعد، كرر)", []() {
            std::string code = "برنامج شروط ؛\nمتغير س : صحيح ؛\n{\n    اذا ( س > 0 ) فان { س = 1 ؛ } والا { س = 0 ؛ } ؛\n    طالما ( س < 10 ) استمر { س = س + 1 ؛ } ؛\n    اعد { س = س - 1 ؛ } حتى ( س == 0 ) ؛\n    كرر ( س = 1 الى 5 اضف 1 ) { اطبع ( س ) ؛ } ؛\n} .";
            auto ast = Parser(Lexer(code).Tokenize()).ParseProgram();
            return ast != nullptr;
        }, totalTests, passedTests);

        RunTest("2.4 إعراب التعابير الحسابية والمنطقية المركبة وأسبقية العمليات", []() {
            std::string code = "برنامج تعابير ؛ متغير س : صحيح ؛ { س = 10 + 5 * 2 - ( 4 / 2 ) ؛ } .";
            auto ast = Parser(Lexer(code).Tokenize()).ParseProgram();
            return ast != nullptr;
        }, totalTests, passedTests);

        std::cout << "\n";

        // المجموعة 3
        std::cout << "🔹 [المجموعة 3]: اختبارات كشف الأخطاء النحوية (Syntax Error Negative Tests)\n";
        RunTest("3.1 كشف خطأ غياب كلمة 'برنامج' في البداية", []() {
            try {
                std::string code = "بدء_بدون_كلمة_برنامج ؛ { } .";
                Parser(Lexer(code).Tokenize()).ParseProgram();
                return false;
            } catch (const std::exception& ex) {
                return std::string(ex.what()).find("برنامج") != std::string::npos;
            }
        }, totalTests, passedTests);

        RunTest("3.2 كشف خطأ نسيان الفاصلة المنقوطة '؛' بعد اسم البرنامج", []() {
            try {
                std::string code = "برنامج اسم_بدون_فاصلة { } .";
                Parser(Lexer(code).Tokenize()).ParseProgram();
                return false;
            } catch (const std::exception& ex) {
                return std::string(ex.what()).find("فاصلة منقوطة") != std::string::npos;
            }
        }, totalTests, passedTests);

        RunTest("3.3 كشف خطأ تعليمة غير مكتملة (معرف بمفرده 'س' بدون '=' أو '(')", []() {
            try {
                std::string code = "برنامج فحص ؛ متغير س , ص : صحيح ؛ { س ص = 10 ؛ } .";
                Parser(Lexer(code).Tokenize()).ParseProgram();
                return false;
            } catch (const std::exception& ex) {
                return std::string(ex.what()).find("تعليمة غير مكتملة") != std::string::npos;
            }
        }, totalTests, passedTests);

        RunTest("3.4 كشف خطأ غياب كلمة 'فان' في الجملة الشرطية", []() {
            try {
                std::string code = "برنامج فحص ؛ متغير س : صحيح ؛ { اذا ( س > 0 ) { س = 1 ؛ } } .";
                Parser(Lexer(code).Tokenize()).ParseProgram();
                return false;
            } catch (const std::exception& ex) {
                return std::string(ex.what()).find("فان") != std::string::npos;
            }
        }, totalTests, passedTests);

        RunTest("3.5 كشف خطأ غياب كلمة 'استمر' في حلقة طالما", []() {
            try {
                std::string code = "برنامج فحص ؛ متغير س : صحيح ؛ { طالما ( س > 0 ) { س = 1 ؛ } } .";
                Parser(Lexer(code).Tokenize()).ParseProgram();
                return false;
            } catch (const std::exception& ex) {
                return std::string(ex.what()).find("استمر") != std::string::npos;
            }
        }, totalTests, passedTests);

        RunTest("3.6 كشف خطأ غياب كلمة 'حتى' في حلقة أعد", []() {
            try {
                std::string code = "برنامج فحص ؛ متغير س : صحيح ؛ { اعد { س = 1 ؛ } ( س > 0 ) ؛ } .";
                Parser(Lexer(code).Tokenize()).ParseProgram();
                return false;
            } catch (const std::exception& ex) {
                return std::string(ex.what()).find("حتى") != std::string::npos;
            }
        }, totalTests, passedTests);

        RunTest("3.7 كشف خطأ تعبير حسابي غير سليم (10 + * 5)", []() {
            try {
                std::string code = "برنامج فحص ؛ متغير س : صحيح ؛ { س = 10 + * 5 ؛ } .";
                Parser(Lexer(code).Tokenize()).ParseProgram();
                return false;
            } catch (...) {
                return true;
            }
        }, totalTests, passedTests);

        RunTest("3.8 كشف خطأ نسيان النقطة '.' في نهاية البرنامج", []() {
            try {
                std::string code = "برنامج فحص ؛ { } ";
                Parser(Lexer(code).Tokenize()).ParseProgram();
                return false;
            } catch (const std::exception& ex) {
                return std::string(ex.what()).find("نقطة") != std::string::npos;
            }
        }, totalTests, passedTests);

        std::cout << "\n";

        // المجموعة 4
        std::cout << "🔹 [المجموعة 4]: اختبارات جدول الرموز والتحليل الدلالي (Semantic Tests)\n";
        RunTest("4.1 إضافة واسترجاع الرموز وتتبع أسطر التعريف والاستخدام", []() {
            SymbolTable st;
            st.Add("س", "صحيح", "متغير", 5);
            st.AddReference("س", 7);
            st.AddReference("س", 9);

            auto sym = st.Lookup("س");
            return sym && sym->ReferencedLines.size() == 2;
        }, totalTests, passedTests);

        RunTest("4.2 كشف خطأ استخدام متغير غير معرف في الكود", []() {
            std::string code = "برنامج فحص ؛ متغير س : صحيح ؛ { س = متغير_مجهول + 5 ؛ } .";
            auto ast = Parser(Lexer(code).Tokenize()).ParseProgram();
            SymbolTable st;
            SemanticAnalyzer sa(st);
            bool ok = sa.Analyze(ast);
            return !ok && sa.GetErrors()[0].find("متغير_مجهول") != std::string::npos;
        }, totalTests, passedTests);

        RunTest("4.3 كشف خطأ محاولة تعديل قيمة ثابت برمجياً", []() {
            std::string code = "برنامج فحص ؛ ثابت الحد = 50 ؛ { الحد = 70 ؛ } .";
            auto ast = Parser(Lexer(code).Tokenize()).ParseProgram();
            SymbolTable st;
            SemanticAnalyzer sa(st);
            bool ok = sa.Analyze(ast);
            return !ok && sa.GetErrors()[0].find("الثابت") != std::string::npos;
        }, totalTests, passedTests);

        RunTest("4.4 كشف خطأ إعادة تعريف نفس المتغير في نفس النطاق", []() {
            std::string code = "برنامج فحص ؛ متغير س : صحيح ؛ س : حقيقي ؛ { س = 10 ؛ } .";
            auto ast = Parser(Lexer(code).Tokenize()).ParseProgram();
            SymbolTable st;
            SemanticAnalyzer sa(st);
            bool ok = sa.Analyze(ast);
            return !ok && sa.GetErrors()[0].find("إعادة تعريف") != std::string::npos;
        }, totalTests, passedTests);

        std::cout << "\n";

        // المجموعة 5
        std::cout << "🔹 [المجموعة 5]: اختبارات الكود الوسيط والتجميع والتكامل (TAC & Execution Tests)\n";
        RunTest("5.1 توليد كود ثلاثي العناوين (TAC) للشروط والعمليات", []() {
            std::string code = "برنامج فحص ؛ متغير س : صحيح ؛ { اذا ( س > 5 ) فان { س = 10 ؛ } ؛ } .";
            auto ast = Parser(Lexer(code).Tokenize()).ParseProgram();
            auto tac = IntermediateCodeGenerator().Generate(ast);
            for (const auto& line : tac) {
                if (line.find("if") != std::string::npos && line.find("goto") != std::string::npos) return true;
            }
            return false;
        }, totalTests, passedTests);

        RunTest("5.2 توليد كود أسمبلي x86 و .NET CIL سليم", []() {
            std::vector<std::string> tacList = { "س = 10", "ص = 20", "t1 = س + ص", "print t1" };
            AssemblyCodeGenerator asmGen(tacList);
            std::string x86 = asmGen.GenerateX86Assembly();
            std::string cil = asmGen.GenerateCIL();
            return !x86.empty() && !cil.empty() && x86.find(".386") != std::string::npos;
        }, totalTests, passedTests);

        RunTest("5.3 تكامل كامل: حساب أسبقية العمليات (10 + 5 * 2 = 20)", []() {
            std::string code = "برنامج فحص ؛ متغير س : صحيح ؛ { س = 10 + 5 * 2 ؛ اطبع ( س ) ؛ } .";
            auto res = CompilerRunner::Compile(code, false);
            return res.IsSuccess && res.ExecutionOutput.find("20") != std::string::npos;
        }, totalTests, passedTests);

        RunTest("5.4 تكامل كامل: حلقة العداد التراكمية كرر (المجموع = 10)", []() {
            std::string code = "برنامج فحص ؛ متغير س , مجموع : صحيح ؛ { مجموع = 0 ؛ كرر ( س = 1 الى 4 اضف 1 ) { مجموع = مجموع + س ؛ } ؛ اطبع ( مجموع ) ؛ } .";
            auto res = CompilerRunner::Compile(code, false);
            return res.IsSuccess && res.ExecutionOutput.find("10") != std::string::npos;
        }, totalTests, passedTests);

        RunTest("5.5 تكامل كامل: حلقة أعد حتى ومضاعفة القيمة (الناتج = 6)", []() {
            std::string code = "برنامج فحص ؛ متغير س : صحيح ؛ { س = 0 ؛ اعد { س = س + 2 ؛ } حتى ( س >= 6 ) ؛ اطبع ( س ) ؛ } .";
            auto res = CompilerRunner::Compile(code, false);
            return res.IsSuccess && res.ExecutionOutput.find("6") != std::string::npos;
        }, totalTests, passedTests);

        std::cout << "\n=========================================================================================\n";
        double percentage = (static_cast<double>(passedTests) / totalTests) * 100.0;
        std::cout << "📊 التقرير النهائي للاختبارات (C++): نجح " << passedTests << " من أصل " << totalTests << " اختباراً (بنسبة " << percentage << "%)\n";
        std::cout << "=========================================================================================\n\n";
    }

} // namespace CompilerCPP
