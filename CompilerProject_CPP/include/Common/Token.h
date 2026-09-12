#pragma once
#include <string>
#include "TokenType.h"

namespace CompilerCPP {

    struct Token {
        std::string Value;
        TokenType Type;
        int Line;

        Token(std::string value = "", TokenType type = TokenType::Unknown, int line = 1)
            : Value(std::move(value)), Type(type), Line(line) {}

        std::string ToString() const {
            return "[" + TokenTypeToString(Type) + "] '" + Value + "' (السطر " + std::to_string(Line) + ")";
        }
    };

} // namespace CompilerCPP
