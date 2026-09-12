#pragma once
#include <string>
#include <vector>
#include <memory>
#include <iostream>

namespace CompilerCPP {

    class Node {
    public:
        std::string Value;
        std::string Name;
        std::string DataType;
        std::string Val;
        int Line;
        std::vector<std::shared_ptr<Node>> Children;

        Node(std::string value = "", int line = 1)
            : Value(std::move(value)), Line(line) {}

        Node(std::string value, std::string name, int line)
            : Value(std::move(value)), Name(std::move(name)), Line(line) {}

        Node(std::string value, std::string name, std::string dataType, int line)
            : Value(std::move(value)), Name(std::move(name)), DataType(std::move(dataType)), Line(line) {}

        void AddChild(std::shared_ptr<Node> child) {
            if (child) {
                Children.push_back(std::move(child));
            }
        }

        std::string ToString() const {
            std::string s = Value;
            if (!Name.empty()) s += " [اسم: " + Name + "]";
            if (!DataType.empty()) s += " [نوع: " + DataType + "]";
            if (!Val.empty()) s += " [قيمة: " + Val + "]";
            s += " (السطر " + std::to_string(Line) + ")";
            return s;
        }

        void Print(const std::string& indent = "", bool isLast = true) const {
            std::cout << indent << (isLast ? "└── " : "├── ") << ToString() << "\n";
            std::string newIndent = indent + (isLast ? "    " : "│   ");
            for (size_t i = 0; i < Children.size(); ++i) {
                Children[i]->Print(newIndent, i == Children.size() - 1);
            }
        }
    };

} // namespace CompilerCPP
