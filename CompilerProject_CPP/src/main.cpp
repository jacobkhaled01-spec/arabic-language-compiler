#include <iostream>
#include <fstream>
#include <sstream>
#include <string>
#include "../include/CodeGeneration/CompilerRunner.h"
#include "../include/Tests/CompilerTestSuite.h"

#ifdef _WIN32
#include <windows.h>
#endif

int main(int argc, char* argv[]) {
#ifdef _WIN32
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);
#endif

    if (argc > 1) {
        std::string firstArg = argv[1];

        // تشغيل الاختبارات
        if (firstArg == "--test" || firstArg == "-t") {
            CompilerCPP::CompilerTestSuite::RunAllTests();
            return 0;
        }

        // قراءة الملف الممرر
        std::ifstream file(firstArg);
        if (!file.is_open()) {
            std::cerr << "❌ خطأ: تعذر فتح الملف: " << firstArg << "\n";
            return 1;
        }

        std::ostringstream ss;
        ss << file.rdbuf();
        std::string sourceCode = ss.str();

        bool jsonMode = (argc > 2 && (std::string(argv[2]) == "--json" || std::string(argv[2]) == "-j"));

        auto result = CompilerCPP::CompilerRunner::Compile(sourceCode, !jsonMode);

        if (jsonMode) {
            std::cout << result.ToJson() << "\n";
        }

        return result.IsSuccess ? 0 : 1;
    }

    // التشغيل التلقائي الافتراضي: عرض الاختبارات الشاملة ثم تشغيل مثال توضيحي
    CompilerCPP::CompilerTestSuite::RunAllTests();

    std::cout << "\n=========================================================================================\n";
    std::cout << "                  بدء تشغيل المترجم (C++) على برنامج حساب_العمليات التجريبي                 \n";
    std::cout << "=========================================================================================\n";

    std::string sampleProgram = 
        "برنامج حساب_العمليات ؛\n"
        "ثابت\n"
        "    الحد_الاقصى = 100 ؛\n"
        "متغير\n"
        "    س , ص , مجموع : صحيح ؛\n"
        "{\n"
        "    س = 20 ؛\n"
        "    ص = 79 ؛\n"
        "    مجموع = س + ص ؛\n"
        "\n"
        "    اذا ( مجموع > 50 ) فان {\n"
        "        اطبع ( \"المجموع اكبر من خمسين وهو:\" , مجموع ) ؛\n"
        "    } والا {\n"
        "        اطبع ( \"المجموع اقل من او يساوي خمسين\" ) ؛\n"
        "    } ؛\n"
        "\n"
        "    اطبع ( \"نهاية البرنامج بنجاح من مترجم C++\" ) ؛\n"
        "} .\n";

    CompilerCPP::CompilerRunner::Compile(sampleProgram, true);

    return 0;
}
