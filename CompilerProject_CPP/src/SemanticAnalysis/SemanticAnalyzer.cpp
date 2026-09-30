#include "../../include/SemanticAnalysis/SemanticAnalyzer.h"

namespace CompilerCPP {

    SemanticAnalyzer::SemanticAnalyzer(SymbolTable& symbolTable)
        : _symbolTable(symbolTable) {}

    void SemanticAnalyzer::AddError(const std::string& err) {
        if (std::find(_errors.begin(), _errors.end(), err) == _errors.end()) {
            _errors.push_back(err);
        }
    }

    bool SemanticAnalyzer::Analyze(const std::shared_ptr<Node>& root) {
        _errors.clear();
        _procedureSignatures.clear();
        _arrayTypeSizes.clear();
        _arrayVarSizes.clear();
        if (!root) return false;

        // تسجيل اسم البرنامج
        _symbolTable.Add(root->Name, "برنامج", "اسم_برنامج", root->Line, "-");

        // 1. جمع وتوثيق كافة الإعلانات في جدول الرموز
        PopulateDeclarations(root);

        // 2. التحقق الدلالي من التعليمات والاستخدامات
        CheckStatementsAndReferences(root);

        return _errors.empty();
    }

    void SemanticAnalyzer::PopulateDeclarations(const std::shared_ptr<Node>& root) {
        if (!root) return;

        std::string currentProc = "";
        auto traverse = [&](auto& self, const std::shared_ptr<Node>& node) -> void {
            if (!node) return;

            std::string prevProc = currentProc;
            if (node->Value == "ConstDecl") {
                if (!_symbolTable.Add(node->Name, "صحيح", "ثابت", node->Line, node->Val)) {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": إعادة تعريف الثابت '" + node->Name + "' في نفس النطاق");
                }
            } else if (node->Value == "TypeDecl_Array") {
                if (!_symbolTable.Add(node->Name, "مصفوفة", "نوع_مصفوفة", node->Line, "حجم=" + node->Val + ", عنصر=" + node->DataType)) {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": إعادة تعريف النوع '" + node->Name + "'");
                }
                try {
                    _arrayTypeSizes[node->Name] = std::stoll(node->Val);
                } catch (...) {}
            } else if (node->Value == "TypeDecl_Record") {
                if (!_symbolTable.Add(node->Name, "سجل", "نوع_سجل", node->Line, "-")) {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": إعادة تعريف النوع '" + node->Name + "'");
                }
            } else if (node->Value == "VarDecl") {
                if (!_symbolTable.Add(node->Name, node->DataType, "متغير", node->Line, "-")) {
                    if (currentProc.empty()) {
                        _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": إعادة تعريف المتغير '" + node->Name + "' في نفس النطاق");
                    }
                }
                if (_arrayTypeSizes.find(node->DataType) != _arrayTypeSizes.end()) {
                    _arrayVarSizes[node->Name] = _arrayTypeSizes[node->DataType];
                }
            } else if (node->Value == "ProcedureDecl") {
                currentProc = node->Name;
                if (!_symbolTable.Add(node->Name, "اجراء", "اجراء", node->Line, "-")) {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": إعادة تعريف الإجراء '" + node->Name + "'");
                }
                std::vector<ParamMetadata> params;
                for (const auto& child : node->Children) {
                    if (child && child->Value == "Parameters") {
                        for (const auto& pNode : child->Children) {
                            if (pNode && pNode->Value == "ParamDecl") {
                                params.push_back({ pNode->Name, pNode->DataType, pNode->Val });
                                _symbolTable.Add(pNode->Name, pNode->DataType, "معامل_" + pNode->Val, pNode->Line, "-");
                                if (_arrayTypeSizes.find(pNode->DataType) != _arrayTypeSizes.end()) {
                                    _arrayVarSizes[pNode->Name] = _arrayTypeSizes[pNode->DataType];
                                }
                            }
                        }
                    }
                }
                _procedureSignatures[node->Name] = params;
            } else if (node->Value == "ParamDecl") {
                _symbolTable.Add(node->Name, node->DataType, "معامل_" + node->Val, node->Line, "معامل إجرائي");
                if (_arrayTypeSizes.find(node->DataType) != _arrayTypeSizes.end()) {
                    _arrayVarSizes[node->Name] = _arrayTypeSizes[node->DataType];
                }
            }

            for (const auto& child : node->Children) {
                self(self, child);
            }
            currentProc = prevProc;
        };

        traverse(traverse, root);
    }

    void SemanticAnalyzer::CheckExpressionVariables(const std::shared_ptr<Node>& exprNode) {
        if (!exprNode) return;

        if (exprNode->Value == "IndexedAccess") {
            if (!exprNode->Children.empty()) {
                std::string rootName = exprNode->Children[0]->Name;
                if (_arrayVarSizes.find(rootName) != _arrayVarSizes.end()) {
                    long long maxSz = _arrayVarSizes[rootName];
                    if (exprNode->Children.size() > 1 && exprNode->Children[1]->Value == "Number") {
                        try {
                            long long idx = std::stoll(exprNode->Children[1]->Val);
                            if (idx < 1 || idx > maxSz) {
                                AddError("خطأ دلالي في السطر " + std::to_string(exprNode->Line) + ": تجاوز حدود المصفوفة '" + rootName + "'! الفهرس [" + std::to_string(idx) + "] خارج النطاق المسموح به [1 .. " + std::to_string(maxSz) + "]");
                            }
                        } catch (...) {}
                    }
                }
            }
        }

        if (exprNode->Value == "Variable") {
            auto sym = _symbolTable.Lookup(exprNode->Name);
            if (!sym) {
                AddError("خطأ دلالي في السطر " + std::to_string(exprNode->Line) + ": استخدام المعرف غير المعرف '" + exprNode->Name + "' في التعبير الحسابي");
            } else {
                _symbolTable.AddReference(exprNode->Name, exprNode->Line);
            }
        }

        for (const auto& child : exprNode->Children) {
            CheckExpressionVariables(child);
        }
    }

    void SemanticAnalyzer::CheckStatementsAndReferences(const std::shared_ptr<Node>& node) {
        if (!node) return;

        if (node->Value == "Assign") {
            auto sym = _symbolTable.Lookup(node->Name);
            if (!sym) {
                AddError("خطأ دلالي في السطر " + std::to_string(node->Line) + ": محاولة إسناد قيمة للمتغير غير المعرف '" + node->Name + "'");
            } else if (sym->Kind == "ثابت") {
                AddError("خطأ دلالي في السطر " + std::to_string(node->Line) + ": لا يمكن تغيير قيمة الثابت '" + node->Name + "'");
            }
            _symbolTable.AddReference(node->Name, node->Line);

            for (const auto& child : node->Children) {
                CheckExpressionVariables(child);
            }
        } else if (node->Value == "Variable") {
            if (!_symbolTable.Contains(node->Name)) {
                AddError("خطأ دلالي في السطر " + std::to_string(node->Line) + ": استخدام المعرف غير المعرف '" + node->Name + "'");
            } else {
                _symbolTable.AddReference(node->Name, node->Line);
            }
        } else if (node->Value == "ReadStatement") {
            auto validateVarNode = [&](const std::shared_ptr<Node>& targetNode) {
                if (!targetNode) return;
                std::string rootName = targetNode->Name;
                if (targetNode->Value == "FieldAccess" || targetNode->Value == "IndexedAccess") {
                    if (!targetNode->Children.empty()) {
                        rootName = targetNode->Children[0]->Name;
                    }
                    if (targetNode->Value == "IndexedAccess" && _arrayVarSizes.find(rootName) != _arrayVarSizes.end()) {
                        long long maxSz = _arrayVarSizes[rootName];
                        if (targetNode->Children.size() > 1 && targetNode->Children[1]->Value == "Number") {
                            try {
                                long long idx = std::stoll(targetNode->Children[1]->Val);
                                if (idx < 1 || idx > maxSz) {
                                    AddError("خطأ دلالي في السطر " + std::to_string(targetNode->Line) + ": تجاوز حدود المصفوفة '" + rootName + "'! الفهرس [" + std::to_string(idx) + "] خارج النطاق المسموح به [1 .. " + std::to_string(maxSz) + "]");
                                }
                            } catch (...) {}
                        }
                    }
                }
                if (rootName.empty()) return;
                auto sym = _symbolTable.Lookup(rootName);
                if (!sym) {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(targetNode->Line) + ": محاولة القراءة إلى متغير غير معرف '" + rootName + "'");
                } else if (sym->Kind == "ثابت") {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(targetNode->Line) + ": لا يمكن القراءة إلى الثابت '" + rootName + "'");
                }
                _symbolTable.AddReference(rootName, targetNode->Line);
            };

            if (!node->Children.empty()) {
                for (const auto& child : node->Children) {
                    validateVarNode(child);
                }
            } else if (!node->Name.empty()) {
                validateVarNode(node);
            }
        } else if (node->Value == "ForStatement") {
            if (!_symbolTable.Contains(node->Name)) {
                _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": متغير العداد '" + node->Name + "' غير معرف في جملة 'كرر'");
            }
            _symbolTable.AddReference(node->Name, node->Line);
        } else if (node->Value == "CallStatement") {
            auto sym = _symbolTable.Lookup(node->Name);
            if (!sym) {
                _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": استدعاء إجراء غير معرف '" + node->Name + "'");
            } else {
                _symbolTable.AddReference(node->Name, node->Line);

                auto sigIt = _procedureSignatures.find(node->Name);
                if (sigIt != _procedureSignatures.end()) {
                    const auto& expectedParams = sigIt->second;
                    if (node->Children.size() != expectedParams.size()) {
                        _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": عدم تطابق عدد المعاملات عند استدعاء الإجراء '" + node->Name + "' (المتوقع " + std::to_string(expectedParams.size()) + " ووُجد " + std::to_string(node->Children.size()) + ")");
                    } else {
                        for (size_t i = 0; i < expectedParams.size(); ++i) {
                            const auto& param = expectedParams[i];
                            const auto& argNode = node->Children[i];

                            if (param.PassMode == "بالمرجع") {
                                if (!argNode || (argNode->Value != "Variable" && argNode->Value != "FieldAccess" && argNode->Value != "IndexedAccess")) {
                                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": المعامل رقم " + std::to_string(i + 1) + " في استدعاء الإجراء '" + node->Name + "' معرف بـ 'بالمرجع' ويجب تمرير متغير وليس تعبيراً أو قيمة ثابتة");
                                } else if (argNode->Value == "Variable") {
                                    auto argSym = _symbolTable.Lookup(argNode->Name);
                                    if (argSym && argSym->Kind == "ثابت") {
                                        _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": لا يمكن تمرير الثابت '" + argSym->Name + "' كمعامل بالمرجع للإجراء '" + node->Name + "'");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        for (const auto& child : node->Children) {
            CheckStatementsAndReferences(child);
        }
    }

} // namespace CompilerCPP
