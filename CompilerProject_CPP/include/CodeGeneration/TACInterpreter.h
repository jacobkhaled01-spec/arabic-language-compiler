#pragma once
#include <string>
#include <vector>
#include <unordered_map>
#include <sstream>

namespace CompilerCPP {

    class TACInterpreter {
    private:
        std::vector<std::string> _tac;
        std::unordered_map<std::string, double> _variables;
        std::unordered_map<std::string, std::string> _stringVars;
        std::unordered_map<std::string, size_t> _labels;

        double EvaluateExpr(const std::string& expr);

    public:
        explicit TACInterpreter(std::vector<std::string> tac);
        std::string Execute();
    };

} // namespace CompilerCPP
