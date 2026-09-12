#pragma once
#include <string>
#include <vector>
#include <unordered_set>
#include "../Common/Token.h"

namespace CompilerCPP {

    class Lexer {
    private:
        std::string _src;
        size_t _pos = 0;
        int _line = 1;

        static const std::unordered_set<std::string> Keywords;

        char Current() const {
            return (_pos < _src.size()) ? _src[_pos] : '\0';
        }

        char Peek(size_t offset = 1) const {
            return (_pos + offset < _src.size()) ? _src[_pos + offset] : '\0';
        }

        void Advance(size_t count = 1) {
            _pos += count;
        }

        static bool IsEasternArabicDigit(const std::string& utf8Char);
        static char ConvertEasternDigit(const std::string& utf8Char);
        static bool IsArabicLetterStart(unsigned char c);

        Token ReadNumber();
        Token ReadIdentifierOrKeyword();
        Token ReadString(char quoteChar);
        Token ReadSmartString(const std::string& startQuote, const std::string& endQuote);
        Token ReadChar();
        Token ReadOperator();

    public:
        explicit Lexer(std::string sourceCode);
        std::vector<Token> Tokenize();
    };

} // namespace CompilerCPP
