#pragma once
#include <string>
#include <vector>
#include <unordered_map>

namespace CompilerCPP {

    class AssemblyCodeGenerator {
    private:
        std::vector<std::string> _tac;
        std::unordered_map<std::string, std::string> _varOffsets;

    public:
        explicit AssemblyCodeGenerator(std::vector<std::string> tac);

        std::string GenerateX86Assembly();
        std::string GenerateCIL();
    };

} // namespace CompilerCPP
