#include "../../include/IntermediateCode/IntermediateCodeGenerator.h"

namespace CompilerCPP {

    IntermediateCodeGenerator::IntermediateCodeGenerator()
        : _tempCounter(0), _labelCounter(0) {}

    std::string IntermediateCodeGenerator::NewTemp() {
        return "t" + std::to_string(++_tempCounter);
    }

    std::string IntermediateCodeGenerator::NewLabel() {
        return "L" + std::to_string(++_labelCounter);
    }

    std::vector<std::string> IntermediateCodeGenerator::Generate(const std::shared_ptr<Node>& root) {
        _tac.clear();
        _tempCounter = 0;
        _labelCounter = 0;

        if (!root) return _tac;

        _tac.push_back("// ===================================================");
        _tac.push_back("// بداية الكود الوسيط (TAC) للبرنامج: " + root->Name);
        _tac.push_back("// ===================================================");

        if (!root->Children.empty()) {
            auto block = root->Children[0];
            if (!block->Children.empty()) {
                auto decls = block->Children[0];
                bool hasProcs = false;
                for (const auto& sec : decls->Children) {
                    if (sec->Value == "ProcedureDecl") {
                        hasProcs = true;
                        break;
                    }
                }
                std::string mainLabel = "L_MAIN_ENTRY";
                if (hasProcs) {
                    _tac.push_back("goto " + mainLabel);
                }
                GenerateDeclarations(decls);
                if (hasProcs) {
                    _tac.push_back(mainLabel + ":");
                }
                if (block->Children.size() > 1) {
                    auto stmtList = block->Children[1];
                    for (const auto& stmt : stmtList->Children) {
                        GenerateStatement(stmt);
                    }
                }
            }
        }

        _tac.push_back("// نهاية البرنامج");
        return _tac;
    }

    void IntermediateCodeGenerator::GenerateDeclarations(const std::shared_ptr<Node>& node) {
        if (!node) return;

        for (const auto& sec : node->Children) {
            if (sec->Value == "ConstDeclarations") {
                for (const auto& c : sec->Children) {
                    _tac.push_back(c->Name + " = " + c->Val);
                }
            } else if (sec->Value == "ProcedureDecl") {
                std::string procName = sec->Name;
                _tac.push_back("proc_" + procName + ":");
                std::shared_ptr<Node> blockNode = nullptr;
                for (const auto& child : sec->Children) {
                    if (child->Value == "Parameters") {
                        for (const auto& p : child->Children) {
                            _tac.push_back(p->Name + " = pop_param");
                        }
                    } else if (child->Value == "Block") {
                        blockNode = child;
                    }
                }
                if (blockNode && blockNode->Children.size() > 1) {
                    auto stmtList = blockNode->Children[1];
                    for (const auto& s : stmtList->Children) {
                        GenerateStatement(s);
                    }
                }
                _tac.push_back("return");
            }
        }
    }

    void IntermediateCodeGenerator::GenerateStatement(const std::shared_ptr<Node>& node) {
        if (!node) return;

        if (node->Value == "Assign") {
            std::string exprRes = GenerateExpression(node->Children[1]);
            std::string target = node->Name;
            if (node->Children[0]->Value == "FieldAccess") {
                target = node->Children[0]->Children[0]->Name + "." + node->Children[0]->Name;
            } else if (node->Children[0]->Value == "IndexedAccess") {
                std::string idx = GenerateExpression(node->Children[0]->Children[1]);
                target = node->Children[0]->Children[0]->Name + "[" + idx + "]";
            }
            _tac.push_back(target + " = " + exprRes);
        } else if (node->Value == "PrintStatement") {
            for (size_t i = 0; i < node->Children.size(); ++i) {
                const auto& arg = node->Children[i];
                bool isLast = (i + 1 == node->Children.size());
                std::string cmd = isLast ? "print " : "print_raw ";
                if (arg->Value == "StringLiteral") {
                    _tac.push_back(cmd + "\"" + arg->Val + "\"");
                } else {
                    std::string res = GenerateExpression(arg);
                    _tac.push_back(cmd + res);
                }
            }
        } else if (node->Value == "ReadStatement") {
            if (!node->Children.empty()) {
                for (const auto& child : node->Children) {
                    _tac.push_back("read " + child->Name);
                }
            } else if (!node->Name.empty()) {
                _tac.push_back("read " + node->Name);
            }
        } else if (node->Value == "IfStatement") {
            std::string cond = GenerateExpression(node->Children[0]);
            std::string trueLabel = NewLabel();
            std::string falseLabel = NewLabel();
            std::string endLabel = NewLabel();

            _tac.push_back("if " + cond + " goto " + trueLabel);
            _tac.push_back("goto " + falseLabel);
            _tac.push_back(trueLabel + ":");

            auto trueBlock = node->Children[1];
            if (trueBlock->Value == "StatementList") {
                for (const auto& s : trueBlock->Children) GenerateStatement(s);
            } else {
                GenerateStatement(trueBlock);
            }

            if (node->Children.size() > 2) {
                _tac.push_back("goto " + endLabel);
                _tac.push_back(falseLabel + ":");

                auto elseBlock = node->Children[2]->Children[0];
                if (elseBlock->Value == "StatementList") {
                    for (const auto& s : elseBlock->Children) GenerateStatement(s);
                } else {
                    GenerateStatement(elseBlock);
                }
                _tac.push_back(endLabel + ":");
            } else {
                _tac.push_back(falseLabel + ":");
            }
        } else if (node->Value == "WhileStatement") {
            std::string startLabel = NewLabel();
            std::string bodyLabel = NewLabel();
            std::string endLabel = NewLabel();

            _tac.push_back(startLabel + ":");
            std::string cond = GenerateExpression(node->Children[0]);
            _tac.push_back("if " + cond + " goto " + bodyLabel);
            _tac.push_back("goto " + endLabel);
            _tac.push_back(bodyLabel + ":");

            auto body = node->Children[1];
            if (body->Value == "StatementList") {
                for (const auto& s : body->Children) GenerateStatement(s);
            } else {
                GenerateStatement(body);
            }

            _tac.push_back("goto " + startLabel);
            _tac.push_back(endLabel + ":");
        } else if (node->Value == "RepeatUntilStatement") {
            std::string startLabel = NewLabel();
            std::string exitLabel = NewLabel();
            _tac.push_back(startLabel + ":");

            auto body = node->Children[0];
            if (body->Value == "StatementList") {
                for (const auto& s : body->Children) GenerateStatement(s);
            } else {
                GenerateStatement(body);
            }

            std::string cond = GenerateExpression(node->Children[1]);
            _tac.push_back("if " + cond + " goto " + exitLabel);
            _tac.push_back("goto " + startLabel);
            _tac.push_back(exitLabel + ":");
        } else if (node->Value == "ForStatement") {
            std::string loopVar = node->Name;
            std::string startVal = GenerateExpression(node->Children[0]);
            std::string endVal = GenerateExpression(node->Children[1]);
            std::string stepVal = (node->Children.size() > 3) ? GenerateExpression(node->Children[2]) : "1";

            _tac.push_back(loopVar + " = " + startVal);
            std::string startLabel = NewLabel();
            std::string bodyLabel = NewLabel();
            std::string endLabel = NewLabel();

            _tac.push_back(startLabel + ":");
            std::string condTemp = NewTemp();
            _tac.push_back(condTemp + " = " + loopVar + " <= " + endVal);
            _tac.push_back("if " + condTemp + " goto " + bodyLabel);
            _tac.push_back("goto " + endLabel);
            _tac.push_back(bodyLabel + ":");

            auto body = node->Children.back();
            if (body->Value == "StatementList") {
                for (const auto& s : body->Children) GenerateStatement(s);
            } else {
                GenerateStatement(body);
            }

            std::string incTemp = NewTemp();
            _tac.push_back(incTemp + " = " + loopVar + " + " + stepVal);
            _tac.push_back(loopVar + " = " + incTemp);
            _tac.push_back("goto " + startLabel);
            _tac.push_back(endLabel + ":");
        } else if (node->Value == "CallStatement") {
            for (const auto& arg : node->Children) {
                std::string argVal = GenerateExpression(arg);
                _tac.push_back("param " + argVal);
            }
            _tac.push_back("call proc_" + node->Name);
        }
    }

    std::string IntermediateCodeGenerator::GenerateExpression(const std::shared_ptr<Node>& node) {
        if (!node) return "";

        if (node->Value == "Number" || node->Value == "StringLiteral" || node->Value == "BooleanLiteral" || node->Value == "CharLiteral") {
            return node->Val;
        }

        if (node->Value == "Variable") {
            return node->Name;
        }

        if (node->Value == "FieldAccess") {
            return node->Children[0]->Name + "." + node->Name;
        }

        if (node->Value == "IndexedAccess") {
            std::string idx = GenerateExpression(node->Children[1]);
            return node->Children[0]->Name + "[" + idx + "]";
        }

        if (node->Value == "UnaryExpr") {
            std::string sub = GenerateExpression(node->Children[0]);
            std::string temp = NewTemp();
            _tac.push_back(temp + " = " + node->Name + " " + sub);
            return temp;
        }

        if (node->Value == "BinaryExpr") {
            std::string left = GenerateExpression(node->Children[0]);
            std::string right = GenerateExpression(node->Children[1]);
            std::string temp = NewTemp();
            _tac.push_back(temp + " = " + left + " " + node->Name + " " + right);
            return temp;
        }

        return "";
    }

} // namespace CompilerCPP
