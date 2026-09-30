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

                std::string baseArray = arrayName;
                if (_aliases.find(arrayName) != _aliases.end()) {
                    arrayName = _aliases[arrayName];
                }

                double idxVal = EvaluateExpr(indexStr);
                long long idxInt = static_cast<long long>(std::round(idxVal));

                // فحص حدود المصفوفة التشغيلي العام الصارم (Runtime Array Bounds Checking)
                long long maxBound = -1;
                if (_arrayBounds.find(arrayName) != _arrayBounds.end()) {
                    maxBound = _arrayBounds[arrayName];
                } else if (_arrayBounds.find(baseArray) != _arrayBounds.end()) {
                    maxBound = _arrayBounds[baseArray];
                } else {
                    size_t uPos = arrayName.find('_');
                    if (uPos != std::string::npos) {
                        std::string unq = arrayName.substr(uPos + 1);
                        if (_arrayBounds.find(unq) != _arrayBounds.end()) {
                            maxBound = _arrayBounds[unq];
                        }
                    }
                    if (maxBound <= 0) {
                        size_t buPos = baseArray.find('_');
                        if (buPos != std::string::npos) {
                            std::string bunq = baseArray.substr(buPos + 1);
                            if (_arrayBounds.find(bunq) != _arrayBounds.end()) {
                                maxBound = _arrayBounds[bunq];
                            }
                        }
                    }
                }

                if (maxBound > 0 && (idxInt < 1 || idxInt > maxBound)) {
                    std::string errMsg = "\n❌ [خطأ تشغيلي - Runtime Error]: تجاوز حدود المصفوفة (Index Out of Bounds)!\n"
                                         "   الفهرس [" + std::to_string(idxInt) + "] يقع خارج نطاق المصفوفة '" + baseArray +
                                         "' المصرح بها بحجم [" + std::to_string(maxBound) + "] (النطاق الصالح: 1 .. " + std::to_string(maxBound) + ").\n";
                    if (_isInteractive) {
                        WriteToConsoleUnicode(errMsg);
                    }
                    throw std::runtime_error(errMsg);
                }

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
        if (s == "صواب" || s == "true" || s == "صح") return 1.0;
        if (s == "خطأ" || s == "false" || s == "خطا") return 0.0;

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
                if (op == "/")  return (rightVal != 0.0) ? (leftVal / rightVal) : 0.0;
                if (op == "\\") return (rightVal != 0.0) ? std::trunc(leftVal / rightVal) : 0.0;
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

        // Unary minus
        if (s.front() == '-') {
            return -EvaluateExpr(s.substr(1));
        }

        // Unary plus
        if (s.front() == '+') {
            return EvaluateExpr(s.substr(1));
        }

        // Unary not
        if (s.front() == '!') {
            return (EvaluateExpr(s.substr(1)) == 0.0) ? 1.0 : 0.0;
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
        _arrayBounds.clear();
        while (!_callStack.empty()) _callStack.pop();
        while (!_aliasStack.empty()) _aliasStack.pop();

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
        try {
            size_t pc = 0;
            while (pc < _tac.size()) {
                std::string line = _tac[pc];
                while (!line.empty() && line.front() == ' ') line.erase(line.begin());
                while (!line.empty() && line.back() == ' ') line.pop_back();

                if (line.empty() || line.rfind("//", 0) == 0 || line.back() == ':') {
                    pc++;
                    continue;
                }

                // alloc_array <name> <capacity>
                if (line.rfind("alloc_array ", 0) == 0) {
                    std::string rest = line.substr(12);
                    while (!rest.empty() && rest.front() == ' ') rest.erase(rest.begin());
                    while (!rest.empty() && rest.back() == ' ') rest.pop_back();
                    size_t sp = rest.find(' ');
                    if (sp != std::string::npos) {
                        std::string arrName = rest.substr(0, sp);
                        std::string sizeStr = rest.substr(sp + 1);
                        while (!arrName.empty() && arrName.back() == ' ') arrName.pop_back();
                        while (!sizeStr.empty() && sizeStr.front() == ' ') sizeStr.erase(sizeStr.begin());
                        try {
                            long long sz = std::stoll(sizeStr);
                            _arrayBounds[arrName] = sz;
                            size_t uPos = arrName.find('_');
                            if (uPos != std::string::npos) {
                                _arrayBounds[arrName.substr(uPos + 1)] = sz;
                            }
                        } catch (...) {}
                    }
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
                if ((arg.size() >= 2 && arg.front() == '"' && arg.back() == '"') ||
                    (arg.size() >= 2 && arg.front() == '\'' && arg.back() == '\'')) {
                    lineOut = arg.substr(1, arg.size() - 2);
                } else if (_stringVars.find(resolvedArg) != _stringVars.end()) {
                    lineOut = _stringVars[resolvedArg];
                } else if (_stringVars.find(arg) != _stringVars.end()) {
                    lineOut = _stringVars[arg];
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
                if ((arg.size() >= 2 && arg.front() == '"' && arg.back() == '"') ||
                    (arg.size() >= 2 && arg.front() == '\'' && arg.back() == '\'')) {
                    lineOut = arg.substr(1, arg.size() - 2);
                } else if (_stringVars.find(resolvedArg) != _stringVars.end()) {
                    lineOut = _stringVars[resolvedArg];
                } else if (_stringVars.find(arg) != _stringVars.end()) {
                    lineOut = _stringVars[arg];
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
                _aliasStack.push(_aliases);
                _aliases.clear();
                if (_labels.find(target) != _labels.end()) {
                    pc = _labels[target];
                } else {
                    pc++;
                }
            }
            // Return
            else if (line == "return") {
                if (!_aliasStack.empty()) {
                    _aliases = _aliasStack.top();
                    _aliasStack.pop();
                } else {
                    _aliases.clear();
                }
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
                bool isStringInput = false;
                std::string strVal = "";
                if (_inputIndex < _inputTokens.size()) {
                    std::string tok = _inputTokens[_inputIndex++];
                    if (tok.size() >= 2 && ((tok.front() == '"' && tok.back() == '"') || (tok.front() == '\'' && tok.back() == '\''))) {
                        isStringInput = true;
                        strVal = tok.substr(1, tok.size() - 2);
                    } else if (TryParseAndSanitizeNumber(tok, val)) {
                        isStringInput = false;
                    } else {
                        double evalVal = EvaluateExpr(tok);
                        if (evalVal != 0.0 || tok == "0" || tok == "0.0") {
                            val = evalVal;
                        } else {
                            isStringInput = true;
                            strVal = tok;
                        }
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
                            WriteToConsoleUnicode("⚠️ [تنبيه] لم تقم بإدخال أي قيمة! يرجى إدخال قيمة عددية أو نصية:\n");
                            continue;
                        }

                        if (trimmed.size() >= 2 && ((trimmed.front() == '"' && trimmed.back() == '"') || (trimmed.front() == '\'' && trimmed.back() == '\''))) {
                            isStringInput = true;
                            strVal = trimmed.substr(1, trimmed.size() - 2);
                            inputAccepted = true;
                        } else if (TryParseAndSanitizeNumber(trimmed, val)) {
                            inputAccepted = true;
                        } else {
                            double evalVal = EvaluateExpr(trimmed);
                            if (evalVal != 0.0 || trimmed == "0" || trimmed == "0.0") {
                                val = evalVal;
                                inputAccepted = true;
                            } else {
                                isStringInput = true;
                                strVal = trimmed;
                                inputAccepted = true;
                            }
                        }
                    }
                } else {
                    // في وضع الترجمة المسبقة (Dry-Run) دون إدخال تفاعلي: إنهاء المحاكاة بسلام لعدم توفر مدخلات
                    break;
                }

                if (isStringInput) {
                    _stringVars[dest] = strVal;
                    _variables[dest] = 0.0;
                    if (_aliases.find(dest) != _aliases.end()) {
                        std::string targetVar = ResolveArrayName(_aliases[dest]);
                        if (targetVar != dest) {
                            _stringVars[targetVar] = strVal;
                        }
                    }
                } else {
                    _variables[dest] = val;
                    _stringVars.erase(dest);
                    if (_aliases.find(dest) != _aliases.end()) {
                        std::string targetVar = ResolveArrayName(_aliases[dest]);
                        if (targetVar != dest) {
                            _variables[targetVar] = val;
                            _stringVars.erase(targetVar);
                        }
                    }
                }
                pc++;
            }
            // Param
            else if (line.rfind("param ", 0) == 0) {
                std::string arg = line.substr(6);
                while (!arg.empty() && arg.front() == ' ') arg.erase(arg.begin());
                while (!arg.empty() && arg.back() == ' ') arg.pop_back();
                arg = ResolveArrayName(arg);
                if (_aliases.find(arg) != _aliases.end()) {
                    arg = _aliases[arg];
                }
                _paramQueue.push_back(arg);
                pc++;
            }
            // Pop param by reference: dest = pop_param_ref
            else if (line.find("= pop_param_ref") != std::string::npos) {
                size_t eqPos = line.find("=");
                std::string dest = line.substr(0, eqPos);
                while (!dest.empty() && dest.front() == ' ') dest.erase(dest.begin());
                while (!dest.empty() && dest.back() == ' ') dest.pop_back();
                if (!_paramQueue.empty()) {
                    std::string actual = _paramQueue.front();
                    _paramQueue.pop_front();
                    bool isIdent = !actual.empty() &&
                                   ((unsigned char)actual[0] > 127 || std::isalpha((unsigned char)actual[0]) || actual[0] == '_');
                    if (isIdent) {
                        _aliases[dest] = actual;
                        if (_arrayBounds.find(actual) != _arrayBounds.end()) {
                            _arrayBounds[dest] = _arrayBounds[actual];
                        } else {
                            size_t uPos = actual.find('_');
                            if (uPos != std::string::npos) {
                                std::string unq = actual.substr(uPos + 1);
                                if (_arrayBounds.find(unq) != _arrayBounds.end()) {
                                    _arrayBounds[dest] = _arrayBounds[unq];
                                }
                            }
                        }
                    }
                    _variables[dest] = EvaluateExpr(actual);
                    if (_stringVars.find(actual) != _stringVars.end()) {
                        _stringVars[dest] = _stringVars[actual];
                    }
                }
                pc++;
            }
            // Pop param by value: dest = pop_param
            else if (line.find("= pop_param") != std::string::npos) {
                size_t eqPos = line.find("=");
                std::string dest = line.substr(0, eqPos);
                while (!dest.empty() && dest.front() == ' ') dest.erase(dest.begin());
                while (!dest.empty() && dest.back() == ' ') dest.pop_back();
                if (!_paramQueue.empty()) {
                    std::string actual = _paramQueue.front();
                    _paramQueue.pop_front();
                    // Call-by-value: NO ALIAS is created, isolating the caller's variable
                    if (_arrayBounds.find(actual) != _arrayBounds.end()) {
                        _arrayBounds[dest] = _arrayBounds[actual];
                    } else {
                        size_t uPos = actual.find('_');
                        if (uPos != std::string::npos) {
                            std::string unq = actual.substr(uPos + 1);
                            if (_arrayBounds.find(unq) != _arrayBounds.end()) {
                                _arrayBounds[dest] = _arrayBounds[unq];
                            }
                        }
                    }
                    _variables[dest] = EvaluateExpr(actual);
                    if (_stringVars.find(actual) != _stringVars.end()) {
                        _stringVars[dest] = _stringVars[actual];
                    }

                    // Deep copy array elements locally if actual was an array
                    std::string prefix = actual + "[";
                    std::vector<std::pair<std::string, double>> toCopyNum;
                    for (const auto& kv : _variables) {
                        if (kv.first.rfind(prefix, 0) == 0) {
                            std::string suffix = kv.first.substr(actual.length());
                            toCopyNum.push_back({dest + suffix, kv.second});
                        }
                    }
                    for (const auto& p : toCopyNum) {
                        _variables[p.first] = p.second;
                    }

                    std::vector<std::pair<std::string, std::string>> toCopyStr;
                    for (const auto& kv : _stringVars) {
                        if (kv.first.rfind(prefix, 0) == 0) {
                            std::string suffix = kv.first.substr(actual.length());
                            toCopyStr.push_back({dest + suffix, kv.second});
                        }
                    }
                    for (const auto& p : toCopyStr) {
                        _stringVars[p.first] = p.second;
                    }
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

                std::string expr = line.substr(eqPos + 1);
                while (!expr.empty() && expr.front() == ' ') expr.erase(expr.begin());
                while (!expr.empty() && expr.back() == ' ') expr.pop_back();

                // 1. فحص ما إذا كانت القيمة المسندة سلسلة نصية أو محرفاً
                if ((expr.size() >= 2 && expr.front() == '"' && expr.back() == '"') ||
                    (expr.size() >= 2 && expr.front() == '\'' && expr.back() == '\'')) {
                    std::string strVal = expr.substr(1, expr.size() - 2);
                    _stringVars[dest] = strVal;
                    _variables[dest] = 0.0;
                    if (_aliases.find(dest) != _aliases.end()) {
                        std::string targetVar = ResolveArrayName(_aliases[dest]);
                        if (targetVar != dest) _stringVars[targetVar] = strVal;
                    }
                } else if (_stringVars.find(expr) != _stringVars.end()) {
                    std::string strVal = _stringVars[expr];
                    _stringVars[dest] = strVal;
                    _variables[dest] = 0.0;
                    if (_aliases.find(dest) != _aliases.end()) {
                        std::string targetVar = ResolveArrayName(_aliases[dest]);
                        if (targetVar != dest) _stringVars[targetVar] = strVal;
                    }
                } else {
                    double res = EvaluateExpr(expr);
                    _variables[dest] = res;
                    _stringVars.erase(dest);

                    if (_aliases.find(dest) != _aliases.end()) {
                        std::string targetVar = ResolveArrayName(_aliases[dest]);
                        if (targetVar != dest) {
                            _variables[targetVar] = res;
                            _stringVars.erase(targetVar);
                        }
                    }
                }
                pc++;
            } else {
                pc++;
            }
        }
    } catch (const std::runtime_error& ex) {
        output << ex.what();
    } catch (const std::exception& ex) {
        std::string errStr = std::string("\n❌ [خطأ تشغيلي غير متوقع]: ") + ex.what() + "\n";
        output << errStr;
        if (_isInteractive) {
            WriteToConsoleUnicode(errStr);
        }
    }

    return output.str();
}

} // namespace CompilerCPP
