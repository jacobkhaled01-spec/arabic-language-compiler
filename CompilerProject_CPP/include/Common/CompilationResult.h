#pragma once
#include <string>
#include <vector>
#include <memory>
#include <sstream>
#include "Token.h"
#include "Node.h"
#include "SymbolTable.h"

namespace CompilerCPP {

    struct CompilationResult {
        bool IsSuccess = false;
        std::string SourceCode;
        std::vector<Token> Tokens;
        std::shared_ptr<Node> AST = nullptr;
        std::vector<SymbolInfo> Symbols;
        std::vector<std::string> SyntaxErrors;
        std::vector<std::string> SemanticErrors;
        std::vector<std::string> TAC;
        std::string AssemblyCode;
        std::string CILCode;
        std::string ExecutionOutput;

        static std::string EscapeJson(const std::string& s) {
            std::ostringstream o;
            for (auto c : s) {
                switch (c) {
                    case '"':  o << "\\\""; break;
                    case '\\': o << "\\\\"; break;
                    case '\b': o << "\\b";  break;
                    case '\f': o << "\\f";  break;
                    case '\n': o << "\\n";  break;
                    case '\r': o << "\\r";  break;
                    case '\t': o << "\\t";  break;
                    default:
                        o << c;
                        break;
                }
            }
            return o.str();
        }

        static std::string NodeToJson(const std::shared_ptr<Node>& node) {
            if (!node) return "null";
            std::ostringstream ss;
            ss << "{\n";
            ss << "    \"Value\": \"" << EscapeJson(node->Value) << "\",\n";
            ss << "    \"Name\": \"" << EscapeJson(node->Name) << "\",\n";
            ss << "    \"DataType\": \"" << EscapeJson(node->DataType) << "\",\n";
            ss << "    \"Val\": \"" << EscapeJson(node->Val) << "\",\n";
            ss << "    \"Line\": " << node->Line << ",\n";
            ss << "    \"Children\": [";
            for (size_t i = 0; i < node->Children.size(); ++i) {
                if (i > 0) ss << ", ";
                ss << NodeToJson(node->Children[i]);
            }
            ss << "]\n}";
            return ss.str();
        }

        std::string ToJson() const {
            std::ostringstream ss;
            ss << "{\n";
            ss << "  \"IsSuccess\": " << (IsSuccess ? "true" : "false") << ",\n";
            ss << "  \"SourceCode\": \"" << EscapeJson(SourceCode) << "\",\n";
            
            // Tokens
            ss << "  \"Tokens\": [\n";
            for (size_t i = 0; i < Tokens.size(); ++i) {
                ss << "    {\n";
                ss << "      \"Value\": \"" << EscapeJson(Tokens[i].Value) << "\",\n";
                ss << "      \"Type\": \"" << TokenTypeToString(Tokens[i].Type) << "\",\n";
                ss << "      \"Line\": " << Tokens[i].Line << "\n";
                ss << "    }" << (i + 1 < Tokens.size() ? "," : "") << "\n";
            }
            ss << "  ],\n";

            // AST
            ss << "  \"AST\": " << NodeToJson(AST) << ",\n";

            // SymbolTable
            ss << "  \"SymbolTable\": [\n";
            for (size_t i = 0; i < Symbols.size(); ++i) {
                const auto& s = Symbols[i];
                std::string refs;
                if (s.ReferencedLines.empty()) refs = "-";
                else {
                    for (size_t k = 0; k < s.ReferencedLines.size(); ++k) {
                        if (k > 0) refs += ", ";
                        refs += std::to_string(s.ReferencedLines[k]);
                    }
                }
                ss << "    {\n";
                ss << "      \"Name\": \"" << EscapeJson(s.Name) << "\",\n";
                ss << "      \"DataType\": \"" << EscapeJson(s.DataType) << "\",\n";
                ss << "      \"Kind\": \"" << EscapeJson(s.Kind) << "\",\n";
                ss << "      \"Value\": \"" << EscapeJson(s.Value) << "\",\n";
                ss << "      \"DeclaredLine\": " << s.DeclaredLine << ",\n";
                ss << "      \"ReferencedLines\": \"" << EscapeJson(refs) << "\"\n";
                ss << "    }" << (i + 1 < Symbols.size() ? "," : "") << "\n";
            }
            ss << "  ],\n";

            // SyntaxErrors
            ss << "  \"SyntaxErrors\": [\n";
            for (size_t i = 0; i < SyntaxErrors.size(); ++i) {
                ss << "    \"" << EscapeJson(SyntaxErrors[i]) << "\"" << (i + 1 < SyntaxErrors.size() ? "," : "") << "\n";
            }
            ss << "  ],\n";

            // SemanticErrors
            ss << "  \"SemanticErrors\": [\n";
            for (size_t i = 0; i < SemanticErrors.size(); ++i) {
                ss << "    \"" << EscapeJson(SemanticErrors[i]) << "\"" << (i + 1 < SemanticErrors.size() ? "," : "") << "\n";
            }
            ss << "  ],\n";

            // TAC
            ss << "  \"TAC\": [\n";
            for (size_t i = 0; i < TAC.size(); ++i) {
                ss << "    \"" << EscapeJson(TAC[i]) << "\"" << (i + 1 < TAC.size() ? "," : "") << "\n";
            }
            ss << "  ],\n";

            // Code
            ss << "  \"AssemblyCode\": \"" << EscapeJson(AssemblyCode) << "\",\n";
            ss << "  \"CILCode\": \"" << EscapeJson(CILCode) << "\",\n";
            ss << "  \"ExecutionOutput\": \"" << EscapeJson(ExecutionOutput) << "\"\n";
            ss << "}\n";

            return ss.str();
        }
    };

} // namespace CompilerCPP
