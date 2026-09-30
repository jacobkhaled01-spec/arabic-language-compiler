#include "../../include/CodeGeneration/TACInterpreter.h"
#include <iostream>
#include <cmath>
#include <cctype>

#ifdef _WIN32
#include <windows.h>
static void WriteToConsoleUnicode(const std::string& utf8) {
    HANDLE hOut = GetStdHandle(STD_OUTPUT_HANDLE);
    DWORD mode = 0;
    if (GetConsoleMode(hOut, &mode)) {
        int size = MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), (int)utf8.length(), NULL, 0);
        if (size > 0) {
            std::wstring wstr(size, 0);
            MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), (int)utf8.length(), &wstr[0], size);
            DWORD written = 0;
            WriteConsoleW(hOut, wstr.c_str(), (DWORD)wstr.length(), &written, NULL);
            return;
        }
    }
    std::cout << utf8;
    std::cout.flush();
}

static std::string ReadLineFromConsoleUnicode() {
    HANDLE hIn = GetStdHandle(STD_INPUT_HANDLE);
    DWORD mode = 0;
    if (GetConsoleMode(hIn, &mode)) {
        wchar_t wbuf[1024];
        DWORD readChars = 0;
        if (ReadConsoleW(hIn, wbuf, 1023, &readChars, NULL) && readChars > 0) {
            while (readChars > 0 && (wbuf[readChars - 1] == L'\r' || wbuf[readChars - 1] == L'\n')) {
                readChars--;
            }
            if (readChars == 0) return "";
            int u8len = WideCharToMultiByte(CP_UTF8, 0, wbuf, readChars, NULL, 0, NULL, NULL);
            if (u8len > 0) {
                std::string utf8(u8len, 0);
                WideCharToMultiByte(CP_UTF8, 0, wbuf, readChars, &utf8[0], u8len, NULL, NULL);
                return utf8;
            }
        }
    }
    std::string line;
    std::getline(std::cin, line);
    return line;
}
#else
static void WriteToConsoleUnicode(const std::string& utf8) {
    std::cout << utf8;
    std::cout.flush();
}

static std::string ReadLineFromConsoleUnicode() {
    std::string line;
    std::getline(std::cin, line);
    return line;
}
#endif

// تنظيف وتطبيع المدخلات العددية ومعالجة المسافات والنقاط والفواصل الزائدة مثل "..4"
static bool TryParseAndSanitizeNumber(const std::string& rawInput, double& outVal) {
    std::string s = rawInput;
    // 1. إزالة المسافات البيضاء والرموز غير المرئية من البداية والنهاية
    while (!s.empty() && ((unsigned char)s.front() <= 32 || (unsigned char)s.front() == 0xFF || (unsigned char)s.front() == 0xFE)) {
        s.erase(s.begin());
    }
    while (!s.empty() && ((unsigned char)s.back() <= 32 || (unsigned char)s.back() == 0xFF || (unsigned char)s.back() == 0xFE)) {
        s.pop_back();
    }

    if (s.empty()) return false;

    // 2. تطبيع الأرقام المشرقية (٠-٩) والفواصل العربية (، / ٫)
    std::string norm = "";
    for (size_t k = 0; k < s.size(); ) {
        unsigned char b1 = static_cast<unsigned char>(s[k]);
        if (b1 == 0xD9 && k + 1 < s.size()) {
            unsigned char b2 = static_cast<unsigned char>(s[k + 1]);
            if (b2 >= 0xA0 && b2 <= 0xA9) {
                norm += (char)('0' + (b2 - 0xA0));
                k += 2;
                continue;
            }
        } else if (b1 == 0xD8 && k + 1 < s.size()) {
            unsigned char b2 = static_cast<unsigned char>(s[k + 1]);
            if (b2 == 0x8C || b2 == 0x8B) { // ، أو ٫
                norm += '.';
                k += 2;
                continue;
            }
        }
        norm += s[k++];
    }

    // 3. التحقق من وجود أرقام
    bool hasDigits = false;
    for (char c : norm) {
        if (c >= '0' && c <= '9') {
            hasDigits = true;
            break;
        }
    }
    if (!hasDigits) return false;

    // 4. معالجة الإشارة
    std::string sign = "";
    size_t startIdx = 0;
    if (!norm.empty() && (norm[0] == '+' || norm[0] == '-')) {
        sign = norm[0];
        startIdx = 1;
        while (startIdx < norm.size() && norm[startIdx] == ' ') startIdx++;
    }

    // 5. إزالة النقاط والفواصل الزائدة من البداية والنهاية (مثل ..4 أو 4..)
    while (startIdx < norm.size() && (norm[startIdx] == '.' || norm[startIdx] == ',')) {
        startIdx++;
    }
    while (norm.size() > startIdx && (norm.back() == '.' || norm.back() == ',')) {
        norm.pop_back();
    }

    std::string cleaned = norm.substr(startIdx);
    std::string finalNum = "";
    bool dotSeen = false;
    for (char c : cleaned) {
        if (c == '.' || c == ',') {
            if (!dotSeen) {
                finalNum += '.';
                dotSeen = true;
            }
        } else if (c >= '0' && c <= '9') {
            finalNum += c;
        } else if (c == ' ') {
            continue; // تجاهل المسافات داخل الأرقام
        } else {
            return false; // احتواء حروف أو رموز غير مقبولة
        }
    }

    if (finalNum.empty()) return false;
    if (!sign.empty()) finalNum = sign + finalNum;

    char* endPtr = nullptr;
    double parsed = std::strtod(finalNum.c_str(), &endPtr);
    if (endPtr && *endPtr == '\0') {
        outVal = parsed;
        return true;
    }

    return false;
}

namespace CompilerCPP {

    TACInterpreter::TACInterpreter(std::vector<std::string> tac, const std::string& userInput, bool isInteractive)
        : _tac(std::move(tac)), _isInteractive(isInteractive) {
        PrepareInput(userInput);
    }

    void TACInterpreter::PrepareInput(const std::string& rawInput) {
        _inputTokens.clear();
        _inputIndex = 0;
        if (rawInput.empty()) return;

        // Skip UTF-8 BOM if present
        size_t startIdx = 0;
        if (rawInput.size() >= 3 &&
            static_cast<unsigned char>(rawInput[0]) == 0xEF &&
            static_cast<unsigned char>(rawInput[1]) == 0xBB &&
            static_cast<unsigned char>(rawInput[2]) == 0xBF) {
            startIdx = 3;
        }

        std::string normalized;
        for (size_t i = startIdx; i < rawInput.size(); ) {
            unsigned char b1 = static_cast<unsigned char>(rawInput[i]);
            if (b1 == '\0' || b1 == 0xFF || b1 == 0xFE) {
                i++;
                continue;
            }
            if (b1 == 0xD9 && i + 1 < rawInput.size()) {
                unsigned char b2 = static_cast<unsigned char>(rawInput[i + 1]);
                if (b2 >= 0xA0 && b2 <= 0xA9) {
                    normalized += (char)('0' + (b2 - 0xA0));
                    i += 2;
                    continue;
                }
            }
            if (b1 == 0xD8 && i + 1 < rawInput.size() && static_cast<unsigned char>(rawInput[i + 1]) == 0x8C) {
                normalized += ' ';
                i += 2;
                continue;
            }
            if (rawInput[i] == ',') {
                normalized += ' ';
                i++;
                continue;
            }
            normalized += rawInput[i];
            i++;
        }

        std::istringstream iss(normalized);
        std::string token;
        while (iss >> token) {
            while (!token.empty() && ((unsigned char)token.front() <= 32 || (unsigned char)token.front() == 0xFF || (unsigned char)token.front() == 0xFE)) {
                token.erase(token.begin());
            }
            while (!token.empty() && ((unsigned char)token.back() <= 32 || (unsigned char)token.back() == 0xFF || (unsigned char)token.back() == 0xFE)) {
                token.pop_back();
            }
            if (!token.empty()) {
                _inputTokens.push_back(token);
            }
        }
    }

    std::string TACInterpreter::ResolveArrayName(const std::string& name) {
        if (name.empty()) return name;
        if (name.back() == ']') {
            size_t openBracket = name.find('[');
            size_t closeBracket = name.rfind(']');
            if (openBracket != std::string::npos && closeBracket != std::string::npos && closeBracket > openBracket) {
                std::string arrayName = name.substr(0, openBracket);
                std::string indexStr = name.substr(openBracket + 1, closeBracket - openBracket - 1);
                while (!arrayName.empty() && arrayName.back() == ' ') arrayName.pop_back();
                while (!indexStr.empty() && indexStr.front() == ' ') indexStr.erase(indexStr.begin());
                while (!indexStr.empty() && indexStr.back() == ' ') indexStr.pop_back();

                if (_aliases.find(arrayName) != _aliases.end()) {
                    arrayName = _aliases[arrayName];
                }

                double idxVal = EvaluateExpr(indexStr);
                long long idxInt = static_cast<long long>(std::round(idxVal));
                return arrayName + "[" + std::to_string(idxInt) + "]";
            }
        }
        if (_aliases.find(name) != _aliases.end()) {
            return _aliases[name];
        }
        return name;
    }

    double TACInterpreter::EvaluateExpr(const std::string& expr) {
        std::string s = expr;
        while (!s.empty() && ((unsigned char)s.front() <= 32 || (unsigned char)s.front() == 0xFF || (unsigned char)s.front() == 0xFE)) s.erase(s.begin());
        while (!s.empty() && ((unsigned char)s.back() <= 32 || (unsigned char)s.back() == 0xFF || (unsigned char)s.back() == 0xFE)) s.pop_back();

        if (s.empty()) return 0.0;

        // Try direct number
        char* endPtr = nullptr;
        double val = std::strtod(s.c_str(), &endPtr);
        if (endPtr && *endPtr == '\0') {
            return val;
        }

        // Try boolean
        if (s == "صواب" || s == "true") return 1.0;
        if (s == "خطأ" || s == "false") return 0.0;

        // Binary operators check (checked first so comparisons like a[i] > a[j] evaluate correctly)
        static const std::vector<std::string> ops = { "==", "!=", "<=", ">=", "<", ">", "&&", "||", "+", "-", "*", "/", "\\", "%", "^" };
        for (const auto& op : ops) {
            std::string opPattern = " " + op + " ";
            size_t pos = s.find(opPattern);
            if (pos != std::string::npos) {
                std::string leftStr = s.substr(0, pos);
                std::string rightStr = s.substr(pos + opPattern.size());
                double leftVal = EvaluateExpr(leftStr);
                double rightVal = EvaluateExpr(rightStr);

                if (op == "+")  return leftVal + rightVal;
                if (op == "-")  return leftVal - rightVal;
                if (op == "*")  return leftVal * rightVal;
                if (op == "/" || op == "\\") return (rightVal != 0.0) ? (leftVal / rightVal) : 0.0;
                if (op == "%")  return std::fmod(leftVal, rightVal != 0.0 ? rightVal : 1.0);
                if (op == "^")  return std::pow(leftVal, rightVal);
                if (op == "==") return (leftVal == rightVal) ? 1.0 : 0.0;
                if (op == "!=") return (leftVal != rightVal) ? 1.0 : 0.0;
                if (op == "<")  return (leftVal < rightVal) ? 1.0 : 0.0;
                if (op == "<=") return (leftVal <= rightVal) ? 1.0 : 0.0;
                if (op == ">")  return (leftVal > rightVal) ? 1.0 : 0.0;
                if (op == ">=") return (leftVal >= rightVal) ? 1.0 : 0.0;
                if (op == "&&") return (leftVal != 0.0 && rightVal != 0.0) ? 1.0 : 0.0;
                if (op == "||") return (leftVal != 0.0 || rightVal != 0.0) ? 1.0 : 0.0;
            }
        }

        // Try variable (with dynamic array indexing)
        // Unary minus
        if (s.front() == '-') {
            return -EvaluateExpr(s.substr(1));
        }

        // Try variable (with dynamic array indexing)
        std::string resolved = ResolveArrayName(s);
        if (_aliases.find(resolved) != _aliases.end()) {
            resolved = _aliases[resolved];
        }
        if (_variables.find(resolved) != _variables.end()) {
            return _variables[resolved];
        }
        if (_variables.find(s) != _variables.end()) {
            return _variables[s];
        }
        if (_aliases.find(s) != _aliases.end()) {
            std::string aliased = _aliases[s];
            if (_variables.find(aliased) != _variables.end()) return _variables[aliased];
        }

        return 0.0;
    }

    std::string TACInterpreter::Execute() {
        _variables.clear();
        _stringVars.clear();
        _labels.clear();
        _paramQueue.clear();
        _aliases.clear();

        // 1. جمع الملصقات Labels
        for (size_t i = 0; i < _tac.size(); ++i) {
            std::string line = _tac[i];
            while (!line.empty() && line.front() == ' ') line.erase(line.begin());
            while (!line.empty() && line.back() == ' ') line.pop_back();

            if (!line.empty() && line.back() == ':') {
                std::string labelName = line.substr(0, line.size() - 1);
                _labels[labelName] = i;
            }
        }

        std::ostringstream output;
        size_t pc = 0;
        while (pc < _tac.size()) {
            std::string line = _tac[pc];
            while (!line.empty() && line.front() == ' ') line.erase(line.begin());
            while (!line.empty() && line.back() == ' ') line.pop_back();

            if (line.empty() || line.rfind("//", 0) == 0 || line.back() == ':') {
                pc++;
                continue;
            }

            // Print Raw (no newline)
            if (line.rfind("print_raw ", 0) == 0) {
                std::string arg = line.substr(10);
                while (!arg.empty() && arg.front() == ' ') arg.erase(arg.begin());
                while (!arg.empty() && arg.back() == ' ') arg.pop_back();

                std::string lineOut;
                std::string resolvedArg = ResolveArrayName(arg);
                if (arg.size() >= 2 && arg.front() == '"' && arg.back() == '"') {
                    lineOut = arg.substr(1, arg.size() - 2);
                } else if (_variables.find(resolvedArg) != _variables.end()) {
                    double v = _variables[resolvedArg];
                    if (v == std::floor(v)) {
                        lineOut = std::to_string(static_cast<long long>(v));
                    } else {
                        std::ostringstream ss; ss << v; lineOut = ss.str();
                    }
                } else {
                    double v = EvaluateExpr(arg);
                    if (v == std::floor(v)) {
                        lineOut = std::to_string(static_cast<long long>(v));
                    } else {
                        std::ostringstream ss; ss << v; lineOut = ss.str();
                    }
                }
                output << lineOut;
                if (_isInteractive) {
                    WriteToConsoleUnicode(lineOut);
                }
                pc++;
            }
            // Print
            else if (line.rfind("print ", 0) == 0) {
                std::string arg = line.substr(6);
                while (!arg.empty() && arg.front() == ' ') arg.erase(arg.begin());
                while (!arg.empty() && arg.back() == ' ') arg.pop_back();

                std::string lineOut;
                std::string resolvedArg = ResolveArrayName(arg);
                if (arg.size() >= 2 && arg.front() == '"' && arg.back() == '"') {
                    lineOut = arg.substr(1, arg.size() - 2);
                } else if (_variables.find(resolvedArg) != _variables.end()) {
                    double v = _variables[resolvedArg];
                    if (v == std::floor(v)) {
                        lineOut = std::to_string(static_cast<long long>(v));
                    } else {
                        std::ostringstream ss; ss << v; lineOut = ss.str();
                    }
                } else {
                    double v = EvaluateExpr(arg);
                    if (v == std::floor(v)) {
                        lineOut = std::to_string(static_cast<long long>(v));
                    } else {
                        std::ostringstream ss; ss << v; lineOut = ss.str();
                    }
                }
                output << lineOut << "\n";
                if (_isInteractive) {
                    WriteToConsoleUnicode(lineOut + "\n");
                }
                pc++;
            }
            // Call
            else if (line.rfind("call ", 0) == 0) {
                std::string target = line.substr(5);
                while (!target.empty() && target.front() == ' ') target.erase(target.begin());
                while (!target.empty() && target.back() == ' ') target.pop_back();
                _callStack.push(pc + 1);
                if (_labels.find(target) != _labels.end()) {
                    pc = _labels[target];
                } else {
                    pc++;
                }
            }
            // Return
            else if (line == "return") {
                _aliases.clear();
                if (!_callStack.empty()) {
                    pc = _callStack.top();
                    _callStack.pop();
                } else {
                    pc++;
                }
            }
            // Goto
            else if (line.rfind("goto ", 0) == 0) {
                std::string target = line.substr(5);
                while (!target.empty() && target.front() == ' ') target.erase(target.begin());
                if (_labels.find(target) != _labels.end()) {
                    pc = _labels[target];
                } else {
                    pc++;
                }
            }
            // If ... goto
            else if (line.rfind("if ", 0) == 0) {
                size_t gPos = line.find(" goto ");
                std::string cond = line.substr(3, gPos - 3);
                std::string target = line.substr(gPos + 6);
                while (!target.empty() && target.front() == ' ') target.erase(target.begin());

                double condVal = EvaluateExpr(cond);
                if (condVal != 0.0) {
                    if (_labels.find(target) != _labels.end()) {
                        pc = _labels[target];
                    } else {
                        pc++;
                    }
                } else {
                    pc++;
                }
            }
            // Read
            else if (line.rfind("read ", 0) == 0) {
                std::string dest = line.substr(5);
                while (!dest.empty() && dest.front() == ' ') dest.erase(dest.begin());
                while (!dest.empty() && dest.back() == ' ') dest.pop_back();
                dest = ResolveArrayName(dest);

                double val = 0.0;
                if (_inputIndex < _inputTokens.size()) {
                    std::string tok = _inputTokens[_inputIndex++];
                    if (!TryParseAndSanitizeNumber(tok, val)) {
                        val = EvaluateExpr(tok);
                    }
                } else if (_isInteractive) {
                    std::string prompt = ">> [اقرا] ادخل قيمة لـ (" + dest + "): ";
                    bool inputAccepted = false;
                    while (!inputAccepted) {
                        WriteToConsoleUnicode(prompt);
                        std::string rawLine = ReadLineFromConsoleUnicode();

                        std::string trimmed = rawLine;
                        while (!trimmed.empty() && ((unsigned char)trimmed.front() <= 32)) trimmed.erase(trimmed.begin());
                        while (!trimmed.empty() && ((unsigned char)trimmed.back() <= 32)) trimmed.pop_back();

                        if (trimmed.empty()) {
                            WriteToConsoleUnicode("⚠️ [تنبيه] لم تقم بإدخال أي قيمة! يرجى إدخال قيمة عددية:\n");
                            continue;
                        }

                        if (TryParseAndSanitizeNumber(trimmed, val)) {
                            inputAccepted = true;
                        } else {
                            double evalVal = EvaluateExpr(trimmed);
                            if (evalVal != 0.0 || trimmed == "0" || trimmed == "0.0") {
                                val = evalVal;
                                inputAccepted = true;
                            } else {
                                WriteToConsoleUnicode("⚠️ [خطأ في الإدخال] القيمة المدخلة (\"" + trimmed + "\") غير صحيحة! يرجى إدخال رقم صحيح:\n");
                            }
                        }
                    }
                } else {
                    // في وضع الترجمة المسبقة (Dry-Run) دون إدخال تفاعلي: إنهاء المحاكاة بسلام لعدم توفر مدخلات
                    break;
                }
                _variables[dest] = val;
                pc++;
            }
            // Param
            else if (line.rfind("param ", 0) == 0) {
                std::string arg = line.substr(6);
                while (!arg.empty() && arg.front() == ' ') arg.erase(arg.begin());
                while (!arg.empty() && arg.back() == ' ') arg.pop_back();
                _paramQueue.push_back(arg);
                pc++;
            }
            // Pop param: dest = pop_param
            else if (line.find("= pop_param") != std::string::npos) {
                size_t eqPos = line.find("=");
                std::string dest = line.substr(0, eqPos);
                while (!dest.empty() && dest.front() == ' ') dest.erase(dest.begin());
                while (!dest.empty() && dest.back() == ' ') dest.pop_back();
                if (!_paramQueue.empty()) {
                    std::string actual = _paramQueue.front();
                    _paramQueue.pop_front();
                    _aliases[dest] = actual;
                    _variables[dest] = EvaluateExpr(actual);
                }
                pc++;
            }
            // Assignment
            else if (line.find("=") != std::string::npos) {
                size_t eqPos = line.find("=");
                std::string dest = line.substr(0, eqPos);
                while (!dest.empty() && dest.back() == ' ') dest.pop_back();
                while (!dest.empty() && dest.front() == ' ') dest.erase(dest.begin());
                dest = ResolveArrayName(dest);
                if (_aliases.find(dest) != _aliases.end()) {
                    dest = _aliases[dest];
                }

                std::string expr = line.substr(eqPos + 1);
                while (!expr.empty() && expr.front() == ' ') expr.erase(expr.begin());
                while (!expr.empty() && expr.back() == ' ') expr.pop_back();

                double res = EvaluateExpr(expr);
                _variables[dest] = res;
                pc++;
            } else {
                pc++;
            }
        }

        return output.str();
    }

} // namespace CompilerCPP
