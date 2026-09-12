#include "../../include/SemanticAnalysis/SemanticAnalyzer.h"

namespace CompilerCPP {

    SemanticAnalyzer::SemanticAnalyzer(SymbolTable& symbolTable)
        : _symbolTable(symbolTable) {}

    bool SemanticAnalyzer::Analyze(const std::shared_ptr<Node>& root) {
        _errors.clear();
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

        auto traverse = [&](auto& self, const std::shared_ptr<Node>& node) -> void {
            if (!node) return;

            if (node->Value == "ConstDecl") {
                if (!_symbolTable.Add(node->Name, "صحيح", "ثابت", node->Line, node->Val)) {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": إعادة تعريف الثابت '" + node->Name + "' في نفس النطاق");
                }
            } else if (node->Value == "TypeDecl_Array") {
                if (!_symbolTable.Add(node->Name, "مصفوفة", "نوع_مصفوفة", node->Line, "حجم=" + node->Val + ", عنصر=" + node->DataType)) {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": إعادة تعريف النوع '" + node->Name + "'");
                }
            } else if (node->Value == "TypeDecl_Record") {
                if (!_symbolTable.Add(node->Name, "سجل", "نوع_سجل", node->Line, "-")) {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": إعادة تعريف النوع '" + node->Name + "'");
                }
            } else if (node->Value == "VarDecl") {
                if (!_symbolTable.Add(node->Name, node->DataType, "متغير", node->Line, "-")) {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": إعادة تعريف المتغير '" + node->Name + "' في نفس النطاق");
                }
            } else if (node->Value == "ProcedureDecl") {
                if (!_symbolTable.Add(node->Name, "اجراء", "اجراء", node->Line, "-")) {
                    _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": إعادة تعريف الإجراء '" + node->Name + "'");
                }
            }

            for (const auto& child : node->Children) {
                self(self, child);
            }
        };

        traverse(traverse, root);
    }

    void SemanticAnalyzer::CheckStatementsAndReferences(const std::shared_ptr<Node>& node) {
        if (!node) return;

        if (node->Value == "Assign") {
            auto sym = _symbolTable.Lookup(node->Name);
            if (!sym) {
                _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": محاولة إسناد قيمة للمتغير غير المعرف '" + node->Name + "'");
            } else if (sym->Kind == "ثابت") {
                _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": لا يمكن تغيير قيمة الثابت '" + node->Name + "'");
            }
            _symbolTable.AddReference(node->Name, node->Line);
        } else if (node->Value == "Variable") {
            if (!_symbolTable.Contains(node->Name)) {
                _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": استخدام المعرف غير المعرف '" + node->Name + "'");
            } else {
                _symbolTable.AddReference(node->Name, node->Line);
            }
        } else if (node->Value == "ReadStatement") {
            auto sym = _symbolTable.Lookup(node->Name);
            if (!sym) {
                _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": محاولة القراءة إلى متغير غير معرف '" + node->Name + "'");
            } else if (sym->Kind == "ثابت") {
                _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": لا يمكن القراءة إلى الثابت '" + node->Name + "'");
            }
            _symbolTable.AddReference(node->Name, node->Line);
        } else if (node->Value == "ForStatement") {
            if (!_symbolTable.Contains(node->Name)) {
                _errors.push_back("خطأ دلالي في السطر " + std::to_string(node->Line) + ": متغير العداد '" + node->Name + "' غير معرف في جملة 'كرر'");
            }
            _symbolTable.AddReference(node->Name, node->Line);
        }

        for (const auto& child : node->Children) {
            CheckStatementsAndReferences(child);
        }
    }

} // namespace CompilerCPP
