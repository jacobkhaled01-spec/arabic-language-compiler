# مشروع المترجم المتكامل (Mini Compiler Project)

مشروع مترجم متكامل مكتوب بلغة C# ينفذ جميع مراحل دورة حياة المترجم (Compiler Lifecycle) من قراءة الكود المصدري حتى توليد كود لغة التجميع (Assembly) وكود .NET CIL.

---

## هيكلية المشروع (Project Architecture)

```text
CompilerProject/
│
├── CompilerProject.sln                  # ملف الـ Solution لفتحه في Visual Studio
├── CompilerProject.csproj               # إعدادات المشروع البرمجي
├── Program.cs                          # نقطة الانطلاق واختبار المترجم
├── README.md                           # التوثيق الشامل للمشروع
│
├── Models/                             # نماذج البيانات الأساسية
│   ├── TokenType.cs                    # أنواع الرموز المعجمية (Tokens Enum)
│   ├── Token.cs                        # كائن التوكن (Value, Type, Line)
│   └── Node.cs                         # عقدة شجرة الإعراب المجردة (AST Node)
│
├── Common/                             # الهياكل والبيانات المشتركة بين المراحل
│   └── SymbolTable.cs                  # إدارة جدول الرموز (SymbolTable & symbolInfo)
│
├── LexicalAnalysis/                    # المرحلة 1: التحليل اللغوي / المعجمي
│   └── Lexer.cs                        # الماسح المعجمي (Lexer)
│
├── SyntaxAnalysis/                     # المرحلة 2: التحليل النحوي
│   └── Parser.cs                       # المحلل النحوي وشجرة الإعراب (Recursive Descent Parser)
│
├── SemanticAnalysis/                   # المرحلة 3: التحليل الدلالي
│   └── SemanticAnalyzer.cs             # المحلل الدلالي وفحص توافق الأنواع وتتبع الرموز
│
├── IntermediateCode/                   # المرحلة 4: توليد الكود الوسيط
│   └── IntermediateCodeGenerator.cs    # توليد كود العناوين الثلاثية (Three-Address Code / TAC)
│
└── CodeGeneration/                     # المرحلة 5: توليد الكود النهائي
    ├── AssemblyCodeGenerator.cs        # توليد كود x86 Assembly وكود .NET CIL
    └── CompilerRunner.cs               # تشغيل وتنسيق خط التجميع المتكامل
```

---

## مراحل المترجم (Compiler Phases)

1. **التحليل المعجمي (Lexical Analysis):** قراءة النص البرمجي وتحويله إلى قائمة متتابعة من الـ Tokens (كلمات محجوزة، معرفات، أرقام، نصوص، عمليات، رموز).
2. **التحليل النحوي (Syntax Analysis):** بناء شجرة الإعراب المجردة (AST) باستخدام خوارزمية النزول المتكرر (Recursive Descent) مع دعم العمليات الحسابية وقواعد الأسبقية وجمل `if-else`, `while`, `print`, `var declaration`, `assignment`.
3. **التحليل الدلالي (Semantic Analysis):** فحص توافق الأنواع، التحقق من عدم تكرار تعريف المتغيرات، التأكد من تعريف المتغيرات قبل استخدامها، وإدارة وتتبع أسطر استخدام المتغيرات داخل جدول الرموز مع تنبيهات للمتغيرات غير المستخدمة.
4. **توليد الكود الوسيط (Intermediate Code Generation):** تحويل شجرة الـ AST إلى كود ثلاثي العناوين (TAC) باستخدام المتغيرات المؤقتة (`t1, t2, ...`) والقفزات وعلامات الانتقال (`L1, L2, ...`).
5. **توليد الكود النهائي (Code Generation):** 
   - توليد كود لغة التجميع x86 وحفظه في `output.asm`.
   - توليد كود لغة .NET Common Intermediate Language (CIL) وحفظه في `output.il`.

---

## طريقة التشغيل (How to Run)

### باستخدام Visual Studio:
1. افتح الملف `CompilerProject.sln`.
2. اضغط `F5` أو زر `Run`.

### باستخدام سطر الأوامر (CLI):
```bash
cd CompilerProject
dotnet run
```

---

## المراجع والتوثيق الأكاديمي (APA 7th References)

- Aho, A. V., Lam, M. S., Sethi, R., & Ullman, J. D. (2006). *Compilers: Principles, Techniques, and Tools* (2nd ed.). Boston, MA: Addison-Wesley.
- Cooper, K. D., & Torczon, L. (2012). *Engineering a Compiler* (2nd ed.). Waltham, MA: Morgan Kaufmann.
