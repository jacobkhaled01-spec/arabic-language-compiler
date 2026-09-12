#pragma once
#include <string>
#include <vector>
#include <unordered_map>
#include <algorithm>
#include <iostream>
#include <iomanip>

namespace CompilerCPP {

    struct SymbolInfo {
        std::string Name;
        std::string DataType;
        std::string Kind;      // متغير, ثابت, اجراء, نوع, حقل
        std::string Value;
        int DeclaredLine;
        std::vector<int> ReferencedLines;

        SymbolInfo(std::string name = "", std::string dataType = "", std::string kind = "متغير", int declaredLine = 0, std::string value = "-")
            : Name(std::move(name)), DataType(std::move(dataType)), Kind(std::move(kind)), Value(std::move(value)), DeclaredLine(declaredLine) {}
    };

    class SymbolTable {
    private:
        std::unordered_map<std::string, SymbolInfo> _symbols;

    public:
        SymbolTable() = default;

        SymbolInfo* Lookup(const std::string& name) {
            auto it = _symbols.find(name);
            return (it != _symbols.end()) ? &it->second : nullptr;
        }

        const SymbolInfo* Lookup(const std::string& name) const {
            auto it = _symbols.find(name);
            return (it != _symbols.end()) ? &it->second : nullptr;
        }

        bool Contains(const std::string& name) const {
            return _symbols.find(name) != _symbols.end();
        }

        bool Add(const std::string& name, const std::string& dataType, const std::string& kind, int declaredLine, const std::string& value = "-") {
            if (Contains(name)) {
                return false;
            }
            _symbols.emplace(name, SymbolInfo(name, dataType, kind, declaredLine, value.empty() ? "-" : value));
            return true;
        }

        bool AddReference(const std::string& name, int line) {
            auto sym = Lookup(name);
            if (!sym) return false;
            if (std::find(sym->ReferencedLines.begin(), sym->ReferencedLines.end(), line) == sym->ReferencedLines.end()) {
                sym->ReferencedLines.push_back(line);
            }
            return true;
        }

        const std::unordered_map<std::string, SymbolInfo>& GetAllSymbols() const {
            return _symbols;
        }

        void Print() const {
            std::cout << "\n╔════════════════╦══════════════╦══════════════╦══════════════╦══════════════╦════════════════════════╗\n";
            std::cout << "║ اسم الرمز      ║ نوع البيانات ║ التصنيف      ║ سطر التعريف  ║ القيمة       ║ أسطر الاستخدام         ║\n";
            std::cout << "╠════════════════╬══════════════╬══════════════╬══════════════╬══════════════╬════════════════════════╣\n";
            for (const auto& pair : _symbols) {
                const auto& s = pair.second;
                std::string refs;
                if (s.ReferencedLines.empty()) {
                    refs = "-";
                } else {
                    for (size_t i = 0; i < s.ReferencedLines.size(); ++i) {
                        if (i > 0) refs += ", ";
                        refs += std::to_string(s.ReferencedLines[i]);
                    }
                }
                std::cout << "║ " << std::left << std::setw(14) << s.Name
                          << " ║ " << std::setw(12) << s.DataType
                          << " ║ " << std::setw(12) << s.Kind
                          << " ║ " << std::setw(12) << s.DeclaredLine
                          << " ║ " << std::setw(12) << s.Value
                          << " ║ " << std::setw(22) << refs << " ║\n";
            }
            std::cout << "╚════════════════╩══════════════╩══════════════╩══════════════╩══════════════╩════════════════════════╝\n";
        }
    };

} // namespace CompilerCPP
