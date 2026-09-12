using System;
using System.Collections.Generic;
using CompilerProject.Models;

namespace CompilerProject.Common
{
    /// <summary>
    /// يمثل سجل معلومات الرمز المعجمي في جدول الرموز
    /// Reference: قواعد لغة البرمجة العربية - جامعة إب (ص 2, 6, 10)
    /// </summary>
    public class SymbolInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;     // صحيح, حقيقي, منطقي, حرفي, خيط_رمزي, ...
        public string Kind { get; set; } = "متغير";             // متغير, ثابت, اجراء, نوع, حقل
        public string Value { get; set; } = string.Empty;        // القيمة الأولية أو الثابتة
        public int DeclaredLine { get; set; }
        public List<int> ReferencedLines { get; set; } = new List<int>();

        public SymbolInfo() { }

        public SymbolInfo(string name, string dataType, string kind, int declaredLine, string value = "")
        {
            Name = name;
            DataType = dataType;
            Kind = kind;
            DeclaredLine = declaredLine;
            Value = value;
        }
    }

    /// <summary>
    /// جدول الرموز الموحد (Symbol Table) للمترجم العربي
    /// يُستخدم في كافة مراحل التحليل (المعجمي، النحوي، الدلالي، وتوليد الكود)
    /// </summary>
    public class SymbolTable
    {
        private readonly Dictionary<string, SymbolInfo> _symbols = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// البحث عن رمز في الجدول
        /// </summary>
        public SymbolInfo? Lookup(string name)
        {
            return _symbols.TryGetValue(name, out var info) ? info : null;
        }

        /// <summary>
        /// التحقق من وجود الرمز
        /// </summary>
        public bool Contains(string name)
        {
            return _symbols.ContainsKey(name);
        }

        /// <summary>
        /// إضافة رمز جديد إلى الجدول
        /// </summary>
        public bool Add(string name, string dataType, string kind, int declaredLine, string value = "")
        {
            if (_symbols.ContainsKey(name))
            {
                return false; // موجود مسبقاً (خطأ إعادة تعريف)
            }

            _symbols[name] = new SymbolInfo(name, dataType, kind, declaredLine, value);
            return true;
        }

        /// <summary>
        /// إضافة رقم سطر مرجعي لاستخدام الرمز
        /// </summary>
        public void AddReference(string name, int line)
        {
            if (_symbols.TryGetValue(name, out var info))
            {
                if (!info.ReferencedLines.Contains(line))
                {
                    info.ReferencedLines.Add(line);
                }
            }
        }

        /// <summary>
        /// الحصول على كافة الرموز المسجلة
        /// </summary>
        public IEnumerable<SymbolInfo> GetAll() => _symbols.Values;

        /// <summary>
        /// طباعة جدول الرموز بتنسيق أنيق
        /// </summary>
        public void Print()
        {
            Console.WriteLine("╔═════════════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                                 جدول الرموز (Symbol Table)                              ║");
            Console.WriteLine("╠════════════════╦══════════════╦══════════════╦══════════════╦══════════════╦════════════╣");
            Console.WriteLine("║ اسم الرمز      ║ نوع البيانات ║ التصنيف      ║ سطر التعريف  ║ القيمة       ║ أسطر الاستخدام ║");
            Console.WriteLine("╠════════════════╬══════════════╬══════════════╬══════════════╬══════════════╬════════════╣");

            foreach (var sym in _symbols.Values)
            {
                string refs = sym.ReferencedLines.Count > 0 ? string.Join(", ", sym.ReferencedLines) : "-";
                string val = string.IsNullOrEmpty(sym.Value) ? "-" : sym.Value;
                Console.WriteLine($"║ {sym.Name,-14} ║ {sym.DataType,-12} ║ {sym.Kind,-12} ║ {sym.DeclaredLine,-12} ║ {val,-12} ║ {refs,-10} ║");
            }

            Console.WriteLine("╚════════════════╩══════════════╩══════════════╩══════════════╩══════════════╩════════════╝");
        }
    }
}
