#pragma once
#include <string>
#include <vector>
#include <memory>
#include "../Common/Node.h"

namespace CompilerCPP {

    class IntermediateCodeGenerator {
    private:
        int _tempCounter = 0;
        int _labelCounter = 0;
        std::vector<std::string> _tac;

        std::string NewTemp();
        std::string NewLabel();

        void GenerateDeclarations(const std::shared_ptr<Node>& node);
        void GenerateStatement(const std::shared_ptr<Node>& node);
        std::string GenerateExpression(const std::shared_ptr<Node>& node);

    public:
        IntermediateCodeGenerator();
        std::vector<std::string> Generate(const std::shared_ptr<Node>& root);
    };

} // namespace CompilerCPP
