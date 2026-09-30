#pragma once
#include <string>
#include <vector>
#include <memory>
#include "../Common/Node.h"
#include "../Common/SymbolTable.h"

namespace CompilerCPP {

    struct ParamMetadata {
        std::string Name;
        std::string DataType;
        std::string PassMode;
    };

    class SemanticAnalyzer {
    private:
        SymbolTable& _symbolTable;
        std::vector<std::string> _errors;
        std::unordered_map<std::string, std::vector<ParamMetadata>> _procedureSignatures;
        std::unordered_map<std::string, long long> _arrayTypeSizes;
        std::unordered_map<std::string, long long> _arrayVarSizes;

        void AddError(const std::string& err);
        void PopulateDeclarations(const std::shared_ptr<Node>& root);
        void CheckStatementsAndReferences(const std::shared_ptr<Node>& node);
        void CheckExpressionVariables(const std::shared_ptr<Node>& exprNode);

    public:
        explicit SemanticAnalyzer(SymbolTable& symbolTable);

        bool Analyze(const std::shared_ptr<Node>& root);
        const std::vector<std::string>& GetErrors() const { return _errors; }
    };

} // namespace CompilerCPP
