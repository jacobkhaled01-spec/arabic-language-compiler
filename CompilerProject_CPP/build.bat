@echo off
chcp 65001 > nul
echo ========================================================
echo جاري بناء مترجم لغة البرمجة العربية بلغة ++C (MSVC / G++)
echo ========================================================

set "SRC_FILES=src\main.cpp src\LexicalAnalysis\Lexer.cpp src\SyntaxAnalysis\Parser.cpp src\SemanticAnalysis\SemanticAnalyzer.cpp src\IntermediateCode\IntermediateCodeGenerator.cpp src\CodeGeneration\AssemblyCodeGenerator.cpp src\CodeGeneration\TACInterpreter.cpp src\CodeGeneration\CompilerRunner.cpp src\Tests\CompilerTestSuite.cpp"

:: 1. البحث عن مترجم MSVC cl.exe
set "CL_PATH="
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.43.34808\bin\Hostx64\x64\cl.exe" (
    set "CL_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.43.34808\bin\Hostx64\x64\cl.exe"
)

:: 2. التحقق من وجود g++ أو cl في الـ PATH
where g++ >nul 2>nul
if %errorlevel% equ 0 (
    echo [1/2] تم العثور على g++، جاري التجميع...
    g++ -std=c++20 -O2 -Iinclude %SRC_FILES% -o CompilerProject_CPP.exe
    goto DONE
)

where cl >nul 2>nul
if %errorlevel% equ 0 (
    echo [1/2] تم العثور على MSVC cl.exe في المسار، جاري التجميع...
    cl /std:c++20 /O2 /EHsc /utf-8 /Iinclude %SRC_FILES% /Fe:CompilerProject_CPP.exe
    goto DONE
)

:: 3. استخدام vcvars64.bat إذا وجد
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" (
    echo [1/2] تهيئة بيئة MSVC x64...
    call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
    cl /std:c++20 /O2 /EHsc /utf-8 /Iinclude %SRC_FILES% /Fe:CompilerProject_CPP.exe
    goto DONE
)

echo ❌ لم يتم العثور على مترجم C++ جاهز في المسار. يرجى تشغيل السكريبت من Developer Command Prompt.
exit /b 1

:DONE
if exist "CompilerProject_CPP.exe" (
    echo.
    echo ✅ تم بناء الملف التنفيذي CompilerProject_CPP.exe بنجاح تام!
    echo ========================================================
)
