#pragma once
#include <string>
#include "../Common/CompilationResult.h"

namespace CompilerCPP {

    class CompilerRunner {
    public:
        static CompilationResult Compile(const std::string& sourceCode, bool isVerbose = false);
    };

} // namespace CompilerCPP
