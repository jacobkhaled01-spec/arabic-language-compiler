#pragma once
#include <string>
#include <vector>
#include <unordered_map>
#include <sstream>
#include <stack>
#include <deque>

namespace CompilerCPP {

    class TACInterpreter {
    private:
        std::vector<std::string> _tac;
        std::unordered_map<std::string, double> _variables;
        std::unordered_map<std::string, std::string> _stringVars;
        std::unordered_map<std::string, size_t> _labels;
        std::stack<size_t> _callStack;
        std::deque<std::string> _paramQueue;
        std::unordered_map<std::string, std::string> _aliases;
        std::stack<std::unordered_map<std::string, std::string>> _aliasStack;
        std::vector<std::string> _inputTokens;
        size_t _inputIndex = 0;
        bool _isInteractive = false;
        std::unordered_map<std::string, long long> _arrayBounds;

        double EvaluateExpr(const std::string& expr);
        std::string ResolveArrayName(const std::string& name);
        void PrepareInput(const std::string& rawInput);

    public:
        explicit TACInterpreter(std::vector<std::string> tac, const std::string& userInput = "", bool isInteractive = false);
        std::string Execute();
    };

} // namespace CompilerCPP
