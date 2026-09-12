#pragma once
#include <string>
#include <vector>
#include <memory>
#include "../Common/Node.h"
#include "../Common/SymbolTable.h"

namespace CompilerCPP {

    class SemanticAnalyzer {
    private:
        SymbolTable& _symbolTable;
        std::vector<std::string> _errors;

        void PopulateDeclarations(const std::shared_ptr<Node>& root);
        void CheckStatementsAndReferences(const std::shared_ptr<Node>& node);

    public:
        explicit SemanticAnalyzer(SymbolTable& symbolTable);

        bool Analyze(const std::shared_ptr<Node>& root);
        const std::vector<std::string>& GetErrors() const { return _errors; }
    };

} // namespace CompilerCPP
