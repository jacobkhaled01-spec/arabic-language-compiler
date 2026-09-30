#include <iostream>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>
#include <filesystem>
#include "../include/CodeGeneration/CompilerRunner.h"
#include "../include/CodeGeneration/TACInterpreter.h"
#include "../include/Tests/CompilerTestSuite.h"

#ifdef _WIN32
#include <windows.h>
#include <shellapi.h>
#pragma comment(lib, "shell32.lib")

static std::string WideToUtf8(const std::wstring& wstr) {
    if (wstr.empty()) return "";
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), NULL, 0, NULL, NULL);
    std::string strTo(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &strTo[0], size_needed, NULL, NULL);
    return strTo;
}
static void ConsolePrint(const std::string& utf8Text) {
    HANDLE hOut = GetStdHandle(STD_OUTPUT_HANDLE);
    DWORD mode = 0;
    if (GetConsoleMode(hOut, &mode)) {
        int size = MultiByteToWideChar(CP_UTF8, 0, utf8Text.c_str(), (int)utf8Text.length(), NULL, 0);
        if (size > 0) {
            std::wstring wstr(size, 0);
            MultiByteToWideChar(CP_UTF8, 0, utf8Text.c_str(), (int)utf8Text.length(), &wstr[0], size);
            DWORD written = 0;
            WriteConsoleW(hOut, wstr.c_str(), (DWORD)wstr.length(), &written, NULL);
            return;
        }
    }
    std::cout << utf8Text;
    std::cout.flush();
}
#else
static void ConsolePrint(const std::string& utf8Text) {
    std::cout << utf8Text;
    std::cout.flush();
}
#endif

int main(int argc, char* argv[]) {
#ifdef _WIN32
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);
    HANDLE hStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
    DWORD consoleMode = 0;
    if (GetConsoleMode(hStdOut, &consoleMode)) {
        consoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        SetConsoleMode(hStdOut, consoleMode);
    }
#endif

    std::vector<std::string> args;
    std::filesystem::path inputFilePath;

#ifdef _WIN32
    int wargc = 0;
    LPWSTR* wargv = CommandLineToArgvW(GetCommandLineW(), &wargc);
    if (wargv) {
        for (int i = 0; i < wargc; ++i) {
            args.push_back(WideToUtf8(wargv[i]));
        }
        if (wargc > 1) {
            inputFilePath = std::filesystem::path(wargv[1]);
        }
        LocalFree(wargv);
    } else {
        for (int i = 0; i < argc; ++i) {
            args.push_back(argv[i]);
        }
        if (argc > 1) {
            inputFilePath = std::filesystem::u8path(argv[1]);
        }
    }
#else
    for (int i = 0; i < argc; ++i) {
        args.push_back(argv[i]);
    }
    if (argc > 1) {
        inputFilePath = std::filesystem::u8path(argv[1]);
    }
#endif

    if (args.size() > 1) {
        std::string firstArg = args[1];

        // تشغيل الاختبارات
        if (firstArg == "--test" || firstArg == "-t") {
            CompilerCPP::CompilerTestSuite::RunAllTests();
            return 0;
        }

        // قراءة الملف الممرر
        std::ifstream file(inputFilePath);
        if (!file.is_open()) {
            std::cerr << "❌ خطأ: تعذر فتح الملف: " << firstArg << "\n";
            return 1;
        }

        std::ostringstream ss;
        ss << file.rdbuf();
        std::string sourceCode = ss.str();

        bool jsonMode = false;
        bool isRunMode = false;
        for (size_t i = 1; i < args.size(); ++i) {
            if (args[i] == "--json" || args[i] == "-j") jsonMode = true;
            if (args[i] == "--run" || args[i] == "-r") isRunMode = true;
        }

        std::string userInput = "";
#ifdef _WIN32
        HANDLE hStdin = GetStdHandle(STD_INPUT_HANDLE);
        DWORD fileType = GetFileType(hStdin);
        bool hasPipedInput = (fileType == FILE_TYPE_PIPE || fileType == FILE_TYPE_DISK);
        if (hasPipedInput) {
            if (fileType == FILE_TYPE_DISK) {
                char buffer[2048];
                DWORD bytesRead = 0;
                while (ReadFile(hStdin, buffer, sizeof(buffer), &bytesRead, NULL) && bytesRead > 0) {
                    userInput.append(buffer, bytesRead);
                }
            } else if (fileType == FILE_TYPE_PIPE) {
                DWORD bytesAvail = 0;
                while (PeekNamedPipe(hStdin, NULL, 0, NULL, &bytesAvail, NULL) && bytesAvail > 0) {
                    std::vector<char> buffer(bytesAvail);
                    DWORD bytesRead = 0;
                    if (ReadFile(hStdin, buffer.data(), bytesAvail, &bytesRead, NULL) && bytesRead > 0) {
                        userInput.append(buffer.data(), bytesRead);
                    } else {
                        break;
                    }
                }
            }
        }
#endif

        if (isRunMode) {
            auto result = CompilerCPP::CompilerRunner::Compile(sourceCode, false, "");
            ConsolePrint("Microsoft Windows [Version 10.0]\n(c) Microsoft Corporation. All rights reserved.\n\n");
            ConsolePrint("> CompilerProject_CPP.exe \"" + inputFilePath.filename().string() + "\"\n");
            ConsolePrint("--------------------------------------------------------------------------------\n");

            if (!result.IsSuccess) {
                ConsolePrint("Build FAILED: فشلت الترجمة نظراً لوجود أخطاء:\n\n");
                for (const auto& err : result.SyntaxErrors) ConsolePrint("  ❌ " + err + "\n");
                for (const auto& err : result.SemanticErrors) ConsolePrint("  ❌ " + err + "\n");
                ConsolePrint("\n--------------------------------------------------------------------------------\n");
                ConsolePrint("Process exited with code 1 (0x1).\n");
                if (!hasPipedInput) {
                    ConsolePrint("Press any key to close this window . . .\n");
                    system("pause >nul");
                }
                return 1;
            }

            CompilerCPP::TACInterpreter runner(result.TAC, userInput, true);
            runner.Execute();

            ConsolePrint("\n--------------------------------------------------------------------------------\n");
            ConsolePrint("Process exited with code 0 (0x0).\n");
            if (!hasPipedInput) {
                ConsolePrint("Press any key to close this window . . .\n");
                system("pause >nul");
            }
            return 0;
        }

        auto result = CompilerCPP::CompilerRunner::Compile(sourceCode, !jsonMode, userInput);

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
