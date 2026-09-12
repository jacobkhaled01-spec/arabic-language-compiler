#pragma once
#include <string>

namespace CompilerCPP {

    enum class TokenType {
        Keyword,
        Identifier,
        Number,
        String,
        Char,
        Operator,
        Symbol,
        EndOfFile,
        Unknown
    };

    inline std::string TokenTypeToString(TokenType type) {
        switch (type) {
            case TokenType::Keyword:    return "Keyword";
            case TokenType::Identifier: return "Identifier";
            case TokenType::Number:     return "Number";
            case TokenType::String:     return "String";
            case TokenType::Char:       return "Char";
            case TokenType::Operator:   return "Operator";
            case TokenType::Symbol:     return "Symbol";
            case TokenType::EndOfFile:  return "EndOfFile";
            default:                    return "Unknown";
        }
    }

} // namespace CompilerCPP
