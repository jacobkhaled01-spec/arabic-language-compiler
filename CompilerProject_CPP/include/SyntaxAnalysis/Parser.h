#pragma once
#include <string>
#include <vector>
#include <memory>
#include <stdexcept>
#include "../Common/Token.h"
#include "../Common/Node.h"

namespace CompilerCPP {

    class Parser {
    private:
        std::vector<Token> _tokens;
        size_t _pos = 0;

        const Token& Current() const;
        const Token& Peek(size_t offset = 1) const;
        bool IsAtEnd() const;

        Token Advance();
        Token Expect(const std::string& expectedValue, const std::string& errorMessage);
        Token ExpectType(TokenType expectedType, const std::string& errorMessage);
        bool Match(const std::string& val);
        bool MatchType(TokenType type);

        std::shared_ptr<Node> ParseBlock();
        std::shared_ptr<Node> ParseDeclarations();
        std::shared_ptr<Node> ParseStatementList();
        std::shared_ptr<Node> ParseStatement();
        std::shared_ptr<Node> ParseIfStatement();
        std::shared_ptr<Node> ParseWhileStatement();
        std::shared_ptr<Node> ParseRepeatUntilStatement();
        std::shared_ptr<Node> ParseForStatement();
        std::shared_ptr<Node> ParsePrintStatement();
        std::shared_ptr<Node> ParseReadStatement();

        std::shared_ptr<Node> ParseExpression();
        std::shared_ptr<Node> ParseLogicalOr();
        std::shared_ptr<Node> ParseLogicalAnd();
        std::shared_ptr<Node> ParseEquality();
        std::shared_ptr<Node> ParseRelational();
        std::shared_ptr<Node> ParseSimpleExpression();
        std::shared_ptr<Node> ParseTerm();
        std::shared_ptr<Node> ParseFactor();

    public:
        std::vector<std::string> Errors;
        void Synchronize();

        explicit Parser(std::vector<Token> tokens);
        std::shared_ptr<Node> ParseProgram();
    };

} // namespace CompilerCPP
