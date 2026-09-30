#pragma once
#include <string>
#include <vector>
#include <unordered_set>
#include <unordered_map>
#include <memory>
#include "../Common/Node.h"

namespace CompilerCPP {

    class IntermediateCodeGenerator {
    private:
        int _tempCounter = 0;
        int _labelCounter = 0;
        std::vector<std::string> _tac;
        std::string _currentProc;
        std::unordered_set<std::string> _localVars;
        std::unordered_map<std::string, long long> _arrayTypeSizes;

        std::string NewTemp();
        std::string NewLabel();
        std::string ScopeName(const std::string& varName);

        void GenerateDeclarations(const std::shared_ptr<Node>& node);
        void GenerateStatement(const std::shared_ptr<Node>& node);
        std::string GenerateExpression(const std::shared_ptr<Node>& node);

    public:
        IntermediateCodeGenerator();
        std::vector<std::string> Generate(const std::shared_ptr<Node>& root);
    };

} // namespace CompilerCPP
