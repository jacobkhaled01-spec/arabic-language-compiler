using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Linq;
using CompilerProject.CodeGeneration;
using CompilerProject.Models;

namespace LanguageEditor
{
    /// <summary>
    /// بيئة التطوير والمحرر الرسومي لمترجم لغة البرمجة العربية
    /// - يدعم التحديث والتحليل اللحظي المباشر أثناء الكتابة (Live Real-Time Analysis)
    /// - يظهر الأخطاء النحوية والدلالية فوراً في شاشة التشغيل وجدول الأخطاء مع تحديد السطر
    /// - يراعي اتجاه النصوص بدقة (RTL للعربية و LTR للغات التجميع)
    /// </summary>
    public class MainForm : Form
    {
        private MenuStrip _menuStrip = null!;
        private ToolStrip _toolStrip = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _statusLabel = null!;
        private ToolStripStatusLabel _fileStatusLabel = null!;
        private ToolStripStatusLabel _lineColStatusLabel = null!;

        private SplitContainer _mainSplitContainer = null!;
        private Panel _editorContainer = null!;
        private RichTextBox _codeEditor = null!;
        private Panel _lineNumbersPanel = null!;
        private TabControl _outputTabControl = null!;

        // تبويبات النتائج
        private DataGridView _tokensGrid = null!;
        private TabPage _tabCst = null!;
        private TreeView _cstTreeView = null!;
        private TabPage _tabAst = null!;
        private TreeView _astTreeView = null!;
        private DataGridView _symbolTableGrid = null!;
        private RichTextBox _tacTextBox = null!;
        private RichTextBox _asmTextBox = null!;
        private RichTextBox _cilTextBox = null!;
        private DataGridView _errorsGrid = null!;
        private TabPage _tabErrors = null!;
        private RichTextBox _consoleOutputTextBox = null!;
        private CompilationResult? _lastResult;

        // مؤقت التحليل اللحظي التلقائي
        private System.Windows.Forms.Timer _liveAnalysisTimer = null!;
        private string? _currentFilePath = null;
        private bool _isLiveAnalysisEnabled = true;
        private readonly List<Process> _spawnedProcesses = new();

        public MainForm()
        {
            InitializeComponent();
            this.FormClosing += (s, e) =>
            {
                foreach (var p in _spawnedProcesses)
                {
                    try { if (!p.HasExited) p.Kill(true); } catch { }
                }
            };
            try { EnsureCompilerCppExtracted(); } catch { }
            SetupLiveTimer();
            LoadDefaultArabicCode();
            UpdateLineNumbers();
            TriggerLiveAnalysis();
        }

        private void SetupLiveTimer()
        {
            _liveAnalysisTimer = new System.Windows.Forms.Timer
            {
                Interval = 350 // تأخير 350 ميلي ثانية بعد توقف الكتابة
            };
            _liveAnalysisTimer.Tick += (s, e) =>
            {
                _liveAnalysisTimer.Stop();
                if (_isLiveAnalysisEnabled)
                {
                    RunLiveAnalysis();
                }
            };
        }

        private void InitializeComponent()
        {
            this.Text = "بيئة تطوير ومحرر لغة البرمجة العربية - (Arabic Language IDE)";
            this.Size = new Size(1300, 850);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            
            // ضبط الاتجاه العام للنافذة: من اليمين لليسار (RTL)
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;

            // 1. شريط القوائم (Menu Bar)
            _menuStrip = new MenuStrip
            {
                RightToLeft = RightToLeft.Yes,
                Font = new Font("Segoe UI", 10F)
            };

            var fileMenu = new ToolStripMenuItem("ملف (File)");
            fileMenu.DropDownItems.Add("📄 ملف جديد (New)", null, (s, e) => NewFile());
            fileMenu.DropDownItems.Add("📂 فتح ملف... (Open)", null, (s, e) => OpenFile());
            fileMenu.DropDownItems.Add("💾 حفظ (Save)", null, (s, e) => SaveFile());
            fileMenu.DropDownItems.Add("💾 حفظ باسم... (Save As)", null, (s, e) => SaveFileAs());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("🚪 خروج (Exit)", null, (s, e) => Close());

            var editMenu = new ToolStripMenuItem("تحرير (Edit)");
            editMenu.DropDownItems.Add("↩️ تراجع (Undo)", null, (s, e) => _codeEditor.Undo());
            editMenu.DropDownItems.Add("↪️ إعادة (Redo)", null, (s, e) => _codeEditor.Redo());
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add("✂️ قص (Cut)", null, (s, e) => _codeEditor.Cut());
            editMenu.DropDownItems.Add("📋 نسخ (Copy)", null, (s, e) => _codeEditor.Copy());
            editMenu.DropDownItems.Add("📥 لصق (Paste)", null, (s, e) => _codeEditor.Paste());
            editMenu.DropDownItems.Add("🔘 تحديد الكل (Select All)", null, (s, e) => _codeEditor.SelectAll());

            var buildMenu = new ToolStripMenuItem("ترجمة وتشغيل (Build & Run)");
            buildMenu.DropDownItems.Add("▶️ ترجمة وتشغيل الكود بالكامل (F5)", null, (s, e) => CompileAndRun());
            buildMenu.DropDownItems.Add("⚙️ فحص القواعد والتحليل اللحظي (F6)", null, (s, e) => RunLiveAnalysis());

            var examplesMenu = new ToolStripMenuItem("أمثلة اللغة (10 أمثلة معتمدة)");
            examplesMenu.DropDownOpening += (s, e) => PopulateExamplesMenu(examplesMenu);
            PopulateExamplesMenu(examplesMenu);

            var viewMenu = new ToolStripMenuItem("عرض (View)");
            viewMenu.DropDownItems.Add("🔄 تحديث الواجهة والتحليل الفوري", null, (s, e) => RefreshInterface());
            viewMenu.DropDownItems.Add("🔤 تبديل اتجاه المحرر (RTL/LTR)", null, (s, e) => ToggleEditorDirection());
            viewMenu.DropDownItems.Add("🧹 مسح كافة المخرجات", null, (s, e) => ClearOutputs());

            var helpMenu = new ToolStripMenuItem("مساعدة (Help)");
            helpMenu.DropDownItems.Add("ℹ️ عن المشروع والمترجم", null, (s, e) => ShowAboutDialog());

            _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, buildMenu, examplesMenu, viewMenu, helpMenu });

            // 2. شريط الأدوات السريع (Tool Strip)
            _toolStrip = new ToolStrip
            {
                RightToLeft = RightToLeft.Yes,
                ImageScalingSize = new Size(22, 22),
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(5)
            };

            var btnNew = new ToolStripButton("📄 جديد", null, (s, e) => NewFile());
            var btnOpen = new ToolStripButton("📂 فتح", null, (s, e) => OpenFile());
            var btnSave = new ToolStripButton("💾 حفظ", null, (s, e) => SaveFile());
            
            var btnRun = new ToolStripButton("▶️ ترجمة وتشغيل (F5)", null, (s, e) => CompileAndRun())
            {
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(8, 2, 4, 2)
            };

            var btnRunCmd = new ToolStripButton("💻 موجه الأوامر CMD (Ctrl+F5)", null, (s, e) => RunInExternalCmd())
            {
                BackColor = Color.FromArgb(37, 37, 38),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Margin = new Padding(2, 2, 8, 2)
            };

            var btnCompile = new ToolStripButton("⚙️ فحص وترجمة (F6)", null, (s, e) => RunLiveAnalysis())
            {
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(2, 2, 8, 2)
            };

            var btnDirToggle = new ToolStripButton("🔤 تبديل الاتجاه", null, (s, e) => ToggleEditorDirection());
            var btnClear = new ToolStripButton("🧹 مسح المخرجات", null, (s, e) => ClearOutputs());

            _toolStrip.Items.AddRange(new ToolStripItem[]
            {
                btnNew, btnOpen, btnSave,
                new ToolStripSeparator(),
                btnRun, btnRunCmd, btnCompile,
                new ToolStripSeparator(),
                btnDirToggle, btnClear
            });

            // 3. شريط الحالة (Status Bar)
            _statusStrip = new StatusStrip { RightToLeft = RightToLeft.Yes };
            _statusLabel = new ToolStripStatusLabel("جاهز") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
            _lineColStatusLabel = new ToolStripStatusLabel("السطر: 1 | العمود: 1");
            _fileStatusLabel = new ToolStripStatusLabel("ملف جديد");
            _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _lineColStatusLabel, _fileStatusLabel });

            // 4. الحاوية المقسمة الرئيسية (Split Container)
            _mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 420,
                RightToLeft = RightToLeft.Yes
            };

            // أ. حاوية المحرر مع أرقام الأسطر
            _editorContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            _codeEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 13F, FontStyle.Regular),
                RightToLeft = RightToLeft.Yes,
                AcceptsTab = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(235, 235, 235),
                BorderStyle = BorderStyle.None
            };

            _lineNumbersPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 50,
                BackColor = Color.FromArgb(37, 37, 38)
            };
            _lineNumbersPanel.Paint += LineNumbersPanel_Paint;

            _codeEditor.VScroll += (s, e) => _lineNumbersPanel.Invalidate();
            _codeEditor.TextChanged += (s, e) =>
            {
                _fileStatusLabel.Text = "تم التعديل *";
                _lineNumbersPanel.Invalidate();
                TriggerLiveAnalysis(); // تشغيل المؤقت للتحليل اللحظي المباشر
            };
            _codeEditor.SelectionChanged += (s, e) => UpdateCaretPosition();

            var editorMarginSpacer = new Panel
            {
                Dock = DockStyle.Right,
                Width = 25, // هامش أمان 25 بكسل يفصل تماماً بين أرقام الأسطر وبداية الكود لمنع أي تداخل
                BackColor = Color.FromArgb(30, 30, 30)
            };

            _editorContainer.Controls.Add(_codeEditor);
            _editorContainer.Controls.Add(editorMarginSpacer);
            _editorContainer.Controls.Add(_lineNumbersPanel);
            _lineNumbersPanel.SendToBack();
            editorMarginSpacer.SendToBack();
            _mainSplitContainer.Panel1.Controls.Add(_editorContainer);

            // ب. تبويبات النتائج (Tab Control)
            _outputTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular)
            };

            // 1. تبويب مخرجات البناء والتشغيل بنمط Visual Studio Build Output
            var tabConsole = new TabPage("🖥️ مخرجات البناء (Build Output)");
            _consoleOutputTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 10.5F, FontStyle.Regular),
                RightToLeft = RightToLeft.No,
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            var consoleHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10, 4, 10, 4)
            };

            var lblConsoleTitle = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                Text = "🖥️ سجل بناء وترجمة الكود المصدري (Build & Diagnostics Output)",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnClearConsole = new Button
            {
                Dock = DockStyle.Right,
                Width = 95,
                Text = "🧹 مسح المخرجات",
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnClearConsole.FlatAppearance.BorderSize = 0;
            btnClearConsole.Click += (s, e) => _consoleOutputTextBox.Clear();

            consoleHeaderPanel.Controls.Add(lblConsoleTitle);
            consoleHeaderPanel.Controls.Add(btnClearConsole);

            tabConsole.Controls.Add(_consoleOutputTextBox);
            tabConsole.Controls.Add(consoleHeaderPanel);

            // 2. تبويب الرموز المعجمية
            var tabTokens = new TabPage("📑 الرموز المعجمية (Tokens)");
            _tokensGrid = CreateArabicDataGrid();
            _tokensGrid.Columns.Add("Num", "#");
            _tokensGrid.Columns.Add("Value", "قيمة الرمز (Token Value)");
            _tokensGrid.Columns.Add("Type", "النوع المعجمي (Token Type)");
            _tokensGrid.Columns.Add("Line", "رقم السطر");
            _tokensGrid.Columns[0].Width = 60;
            _tokensGrid.Columns[1].Width = 300;
            _tokensGrid.Columns[2].Width = 250;
            _tokensGrid.Columns[3].Width = 100;
            tabTokens.Controls.Add(_tokensGrid);

            // 3. تبويب شجرة الإعراب (Parse Tree / CST)
            _tabCst = new TabPage("🌲 شجرة الإعراب (Parse Tree)");

            var cstHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(8, 4, 8, 4)
            };

            var lblCstTitle = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                Text = "🌲 شجرة الإعراب النحوية وقواعد الاشتقاق (Parse Tree / CST)",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnCopyCst = new Button
            {
                Dock = DockStyle.Right,
                Width = 90,
                Text = "📋 نسخ نصياً",
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnCopyCst.FlatAppearance.BorderSize = 0;

            var btnToggleCstDir = new Button
            {
                Dock = DockStyle.Right,
                Width = 105,
                Text = "🔤 تبديل الاتجاه",
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnToggleCstDir.FlatAppearance.BorderSize = 0;

            var btnCollapseCst = new Button
            {
                Dock = DockStyle.Right,
                Width = 75,
                Text = "➖ طي الكل",
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnCollapseCst.FlatAppearance.BorderSize = 0;

            var btnExpandCst = new Button
            {
                Dock = DockStyle.Right,
                Width = 85,
                Text = "➕ توسيع الكل",
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnExpandCst.FlatAppearance.BorderSize = 0;

            _cstTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                BackColor = Color.FromArgb(250, 252, 255),
                ForeColor = Color.FromArgb(20, 20, 20),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true
            };

            btnExpandCst.Click += (s, e) => _cstTreeView.ExpandAll();
            btnCollapseCst.Click += (s, e) => _cstTreeView.CollapseAll();
            btnToggleCstDir.Click += (s, e) =>
            {
                if (_cstTreeView.RightToLeft == RightToLeft.Yes)
                {
                    _cstTreeView.RightToLeft = RightToLeft.No;
                    _cstTreeView.RightToLeftLayout = false;
                }
                else
                {
                    _cstTreeView.RightToLeft = RightToLeft.Yes;
                    _cstTreeView.RightToLeftLayout = true;
                }
            };
            btnCopyCst.Click += (s, e) =>
            {
                if (_cstTreeView.Nodes.Count > 0)
                {
                    var sb = new StringBuilder();
                    foreach (TreeNode node in _cstTreeView.Nodes)
                    {
                        FormatTreeNodeText(node, sb, "");
                    }
                    Clipboard.SetText(sb.ToString());
                    _statusLabel.Text = "تم نسخ شجرة الإعراب (Parse Tree) إلى الحافظة بنجاح ✔️";
                }
            };

            cstHeaderPanel.Controls.Add(lblCstTitle);
            cstHeaderPanel.Controls.Add(btnCopyCst);
            cstHeaderPanel.Controls.Add(btnToggleCstDir);
            cstHeaderPanel.Controls.Add(btnCollapseCst);
            cstHeaderPanel.Controls.Add(btnExpandCst);

            _tabCst.Controls.Add(_cstTreeView);
            _tabCst.Controls.Add(cstHeaderPanel);

            // 4. تبويب الشجرة المجردة (AST)
            _tabAst = new TabPage("🌳 الشجرة المجردة (AST)");

            var astHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(8, 4, 8, 4)
            };

            var lblAstTitle = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                Text = "🌳 الشجرة النحوية المجردة (Abstract Syntax Tree - AST)",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnCopyAst = new Button
            {
                Dock = DockStyle.Right,
                Width = 90,
                Text = "📋 نسخ نصياً",
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnCopyAst.FlatAppearance.BorderSize = 0;

            var btnToggleAstDir = new Button
            {
                Dock = DockStyle.Right,
                Width = 105,
                Text = "🔤 تبديل الاتجاه",
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnToggleAstDir.FlatAppearance.BorderSize = 0;

            var btnCollapseAst = new Button
            {
                Dock = DockStyle.Right,
                Width = 75,
                Text = "➖ طي الكل",
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnCollapseAst.FlatAppearance.BorderSize = 0;

            var btnExpandAst = new Button
            {
                Dock = DockStyle.Right,
                Width = 85,
                Text = "➕ توسيع الكل",
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnExpandAst.FlatAppearance.BorderSize = 0;

            _astTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                BackColor = Color.FromArgb(250, 252, 255),
                ForeColor = Color.FromArgb(20, 20, 20),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true
            };

            btnExpandAst.Click += (s, e) => _astTreeView.ExpandAll();
            btnCollapseAst.Click += (s, e) => _astTreeView.CollapseAll();
            btnToggleAstDir.Click += (s, e) =>
            {
                if (_astTreeView.RightToLeft == RightToLeft.Yes)
                {
                    _astTreeView.RightToLeft = RightToLeft.No;
                    _astTreeView.RightToLeftLayout = false;
                }
                else
                {
                    _astTreeView.RightToLeft = RightToLeft.Yes;
                    _astTreeView.RightToLeftLayout = true;
                }
            };
            btnCopyAst.Click += (s, e) =>
            {
                if (_lastResult?.AST != null)
                {
                    string textTree = FormatAstToString(_lastResult.AST);
                    Clipboard.SetText(textTree);
                    MessageBox.Show("تم نسخ هيكل الشجرة المجردة إلى الحافظة بنجاح ✔️", "نسخ الشجرة", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("لا توجد شجرة مجردة متاحة للنسخ حالياً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            astHeaderPanel.Controls.Add(lblAstTitle);
            astHeaderPanel.Controls.Add(btnExpandAst);
            astHeaderPanel.Controls.Add(btnCollapseAst);
            astHeaderPanel.Controls.Add(btnToggleAstDir);
            astHeaderPanel.Controls.Add(btnCopyAst);

            _tabAst.Controls.Add(_astTreeView);
            _tabAst.Controls.Add(astHeaderPanel);

            // 4. تبويب جدول الرموز
            var tabSymbols = new TabPage("📋 جدول الرموز (Symbol Table)");
            _symbolTableGrid = CreateArabicDataGrid();
            _symbolTableGrid.Columns.Add("Name", "اسم الرمز");
            _symbolTableGrid.Columns.Add("DataType", "نوع البيانات");
            _symbolTableGrid.Columns.Add("Kind", "التصنيف");
            _symbolTableGrid.Columns.Add("Value", "القيمة");
            _symbolTableGrid.Columns.Add("DeclaredLine", "سطر التعريف");
            _symbolTableGrid.Columns.Add("Refs", "أسطر الاستخدام");
            tabSymbols.Controls.Add(_symbolTableGrid);

            // 5. تبويب الكود الوسيط (TAC) - اتجاه LTR
            var tabTac = new TabPage("⚙️ الكود الوسيط (TAC)");
            _tacTextBox = CreateTechnicalViewer();
            tabTac.Controls.Add(_tacTextBox);

            // 6. تبويب لغة التجميع x86 - اتجاه LTR
            var tabAsm = new TabPage("💻 لغة التجميع (x86 Assembly)");
            _asmTextBox = CreateTechnicalViewer();
            tabAsm.Controls.Add(_asmTextBox);

            // 7. تبويب .NET CIL - اتجاه LTR
            var tabCil = new TabPage("📜 .NET CIL");
            _cilTextBox = CreateTechnicalViewer();
            tabCil.Controls.Add(_cilTextBox);

            // 8. تبويب قائمة الأخطاء - RTL
            _tabErrors = new TabPage("⚠️ قائمة الأخطاء (Error List)");
            _errorsGrid = CreateArabicDataGrid();
            _errorsGrid.Columns.Add("Index", "#");
            _errorsGrid.Columns.Add("Line", "السطر");
            _errorsGrid.Columns.Add("Type", "نوع الخطأ");
            _errorsGrid.Columns.Add("Message", "نص وتفاصيل الخطأ وموقعه في الكود");
            _errorsGrid.Columns[0].Width = 50;
            _errorsGrid.Columns[1].Width = 80;
            _errorsGrid.Columns[2].Width = 180;
            _errorsGrid.Columns[3].Width = 800;
            
            // عند النقر على سطر الخطأ: الانتقال إلى السطر في المحرر وتظليله
            _errorsGrid.CellDoubleClick += (s, e) => NavigateToErrorLine();
            _errorsGrid.CellClick += (s, e) => NavigateToErrorLine();

            _tabErrors.Controls.Add(_errorsGrid);

            _outputTabControl.TabPages.AddRange(new TabPage[]
            {
                tabConsole, tabTokens, _tabCst, _tabAst, tabSymbols, tabTac, tabAsm, tabCil, _tabErrors
            });

            _mainSplitContainer.Panel2.Controls.Add(_outputTabControl);

            // تجميع العناصر على النموذج
            this.Controls.Add(_mainSplitContainer);
            this.Controls.Add(_toolStrip);
            this.Controls.Add(_menuStrip);
            this.Controls.Add(_statusStrip);
            this.MainMenuStrip = _menuStrip;

            // اختصارات لوحة المفاتيح
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.F5) { RunInExternalCmd(); e.Handled = true; }
                else if (e.KeyCode == Keys.F5) { CompileAndRun(); e.Handled = true; }
                if (e.KeyCode == Keys.F6) { RunLiveAnalysis(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.S) { SaveFile(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.O) { OpenFile(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.N) { NewFile(); e.Handled = true; }
            };
        }

        private DataGridView CreateArabicDataGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RightToLeft = RightToLeft.Yes,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9.5F)
            };

            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 243, 246);
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.EnableHeadersVisualStyles = false;

            return grid;
        }

        private RichTextBox CreateTechnicalViewer()
        {
            return new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10.5F, FontStyle.Regular),
                RightToLeft = RightToLeft.No,
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 249, 250),
                ForeColor = Color.FromArgb(33, 37, 41),
                BorderStyle = BorderStyle.None
            };
        }

        private void LineNumbersPanel_Paint(object? sender, PaintEventArgs e)
        {
            int firstIndex = _codeEditor.GetCharIndexFromPosition(new Point(0, 0));
            int firstLine = _codeEditor.GetLineFromCharIndex(firstIndex);

            int lastIndex = _codeEditor.GetCharIndexFromPosition(new Point(0, _codeEditor.Height));
            int lastLine = _codeEditor.GetLineFromCharIndex(lastIndex);

            using var brush = new SolidBrush(Color.FromArgb(150, 150, 150));
            using var font = new Font("Consolas", 10F, FontStyle.Bold);
            using var borderPen = new Pen(Color.FromArgb(65, 65, 65), 1);

            // خط فاصل عمودي على يسار لوحة الأرقام ليفصلها عن محرر الأكواد
            e.Graphics.DrawLine(borderPen, 0, 0, 0, _lineNumbersPanel.Height);

            for (int i = firstLine; i <= lastLine + 1; i++)
            {
                int charIndex = _codeEditor.GetFirstCharIndexFromLine(i);
                if (charIndex < 0 && i > 0) break;

                Point pos = _codeEditor.GetPositionFromCharIndex(charIndex >= 0 ? charIndex : 0);
                string numStr = (i + 1).ToString();
                var size = e.Graphics.MeasureString(numStr, font);
                float x = _lineNumbersPanel.Width - size.Width - 8;
                e.Graphics.DrawString(numStr, font, brush, x, pos.Y + 2);
            }
        }

        private void UpdateLineNumbers()
        {
            int totalLines = Math.Max(1, _codeEditor.Lines.Length);
            int digits = Math.Max(3, totalLines.ToString().Length);
            int requiredWidth = 24 + digits * 9;
            if (_lineNumbersPanel.Width != requiredWidth)
            {
                _lineNumbersPanel.Width = requiredWidth;
            }
            _lineNumbersPanel.Invalidate();
        }

        private void UpdateCaretPosition()
        {
            int index = _codeEditor.SelectionStart;
            int line = _codeEditor.GetLineFromCharIndex(index) + 1;
            int col = index - _codeEditor.GetFirstCharIndexOfCurrentLine() + 1;
            _lineColStatusLabel.Text = $"السطر: {line} | العمود: {col}";
        }

        private void ToggleEditorDirection()
        {
            if (_codeEditor.RightToLeft == RightToLeft.Yes)
            {
                _codeEditor.RightToLeft = RightToLeft.No;
                _statusLabel.Text = "تم تغيير اتجاه المحرر إلى: يسار إلى يمين (LTR)";
            }
            else
            {
                _codeEditor.RightToLeft = RightToLeft.Yes;
                _statusLabel.Text = "تم تغيير اتجاه المحرر إلى: يمين إلى يسار (RTL)";
            }
        }

        private void TriggerLiveAnalysis()
        {
            _liveAnalysisTimer.Stop();
            _liveAnalysisTimer.Start();
        }

        /// <summary>
        /// التحليل اللحظي المباشر الذي يعمل فور كتابة أو تعديل الكود
        /// </summary>
        private void RunLiveAnalysis()
        {
            string code = _codeEditor.Text;
            if (string.IsNullOrWhiteSpace(code))
            {
                ClearOutputs();
                _statusLabel.Text = "المحرر فارغ";
                return;
            }

            try
            {
                CompilationResult? result = null;
                string compilerExe = FindCompilerExe();
                if (!string.IsNullOrEmpty(compilerExe) && File.Exists(compilerExe))
                {
                    string tempSourceFile = Path.Combine(Path.GetTempPath(), $"live_{Guid.NewGuid():N}.arb");
                    try
                    {
                        File.WriteAllText(tempSourceFile, code, new UTF8Encoding(false));
                        var psi = new ProcessStartInfo
                        {
                            FileName = compilerExe,
                            Arguments = $"\"{tempSourceFile}\" --json",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8
                        };
                        using var proc = Process.Start(psi);
                        if (proc != null)
                        {
                            string json = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit(3000);
                            if (!string.IsNullOrWhiteSpace(json) && json.Contains("IsSuccess"))
                            {
                                result = JsonSerializer.Deserialize<CompilationResult>(json);
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        try { if (File.Exists(tempSourceFile)) File.Delete(tempSourceFile); } catch { }
                    }
                }

                if (result == null)
                {
                    result = CompilerRunner.Compile(code, isVerbose: false);
                }

                DisplayCompilationResult(result, isExecutionRun: false);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"خطأ في التحليل: {ex.Message}";
            }
        }

        private void LoadDefaultArabicCode()
        {
            _codeEditor.Text =
@"برنامج حساب_العمليات ؛
ثابت
    الحد_الاقصى = 100 ؛
متغير
    س , ص , مجموع : صحيح ؛
{
    س = 20 ؛
    ص = 79 ؛
    مجموع = س + ص ؛

    اذا ( مجموع > 50 ) فان {
        اطبع ( ""المجموع اكبر من خمسين وهو:"" , مجموع ) ؛
    } والا {
        اطبع ( ""المجموع اقل من او يساوي خمسين"" ) ؛
    } ؛

    اطبع ( ""نهاية البرنامج بنجاح"" ) ؛
} .";
        }

        private void PopulateExamplesMenu(ToolStripMenuItem menu)
        {
            menu.DropDownItems.Clear();

            string appDir = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? AppDomain.CurrentDomain.BaseDirectory;

            string[] searchPaths = new[]
            {
                Path.Combine(appDir, "Examples"),
                Path.Combine(Directory.GetCurrentDirectory(), "Examples"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "Examples"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Examples"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Examples"),
                @"d:\IT FILES\level 4\مترجمات\نظري\مشروع\Examples",
                @"d:\IT FILES\level 4\مترجمات\نظري\مشروع\تسليم_المشروع_Final_Delivery\PREXE_SingleFile\Examples"
            };

            string? foundDir = null;
            foreach (var path in searchPaths)
            {
                if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*.arb");
                    if (files.Length > 0)
                    {
                        foundDir = path;
                        break;
                    }
                }
            }

            if (foundDir != null)
            {
                var files = Directory.GetFiles(foundDir, "*.arb");
                Array.Sort(files);

                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    menu.DropDownItems.Add(fileName, null, (s, e) =>
                    {
                        _isLiveAnalysisEnabled = false;
                        _codeEditor.Text = File.ReadAllText(file, Encoding.UTF8);
                        _currentFilePath = file;
                        _fileStatusLabel.Text = Path.GetFileName(file);
                        _statusLabel.Text = $"تم فتح المثال: {fileName}";
                        UpdateLineNumbers();
                        _isLiveAnalysisEnabled = true;
                        RunLiveAnalysis();
                    });
                }
            }
            else
            {
                menu.DropDownItems.Add("⚠️ تعذر العثور على المجلد، انقر هنا لفتح ملف", null, (s, e) => OpenFile());
            }
        }

        private void NewFile()
        {
            _isLiveAnalysisEnabled = false;
            _codeEditor.Clear();
            _currentFilePath = null;
            _fileStatusLabel.Text = "ملف جديد";
            _statusLabel.Text = "تم إنشاء ملف جديد";
            ClearOutputs();
            UpdateLineNumbers();
            _isLiveAnalysisEnabled = true;
        }

        private void OpenFile()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "ملفات لغة البرمجة العربية (*.arb;*.txt)|*.arb;*.txt|جميع الملفات (*.*)|*.*",
                Title = "فتح ملف كود عربي"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _isLiveAnalysisEnabled = false;
                _codeEditor.Text = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                _currentFilePath = ofd.FileName;
                _fileStatusLabel.Text = Path.GetFileName(ofd.FileName);
                _statusLabel.Text = $"تم فتح: {ofd.FileName}";
                UpdateLineNumbers();
                _isLiveAnalysisEnabled = true;
                RunLiveAnalysis();
            }
        }

        private void SaveFile()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveFileAs();
            }
            else
            {
                File.WriteAllText(_currentFilePath, _codeEditor.Text, Encoding.UTF8);
                _fileStatusLabel.Text = Path.GetFileName(_currentFilePath);
                _statusLabel.Text = "تم الحفظ بنجاح";
            }
        }

        private void SaveFileAs()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "ملفات لغة البرمجة العربية (*.arb)|*.arb|ملفات نصية (*.txt)|*.txt",
                Title = "حفظ الكود المصدري"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                _currentFilePath = sfd.FileName;
                SaveFile();
            }
        }

        private void ClearOutputs()
        {
            _tokensGrid.Rows.Clear();
            _astTreeView.Nodes.Clear();
            _symbolTableGrid.Rows.Clear();
            _tacTextBox.Clear();
            _asmTextBox.Clear();
            _cilTextBox.Clear();
            _errorsGrid.Rows.Clear();
            _consoleOutputTextBox.Clear();
        }

        private void CompileAndRun()
        {
            _statusLabel.Text = "جاري الترجمة والتحقق من سلامة الكود...";

            string tempSourceFile = Path.Combine(Path.GetTempPath(), $"source_{Guid.NewGuid():N}.arb");
            File.WriteAllText(tempSourceFile, _codeEditor.Text, new UTF8Encoding(false));

            string compilerExe = FindCompilerExe();
            CompilationResult? result = null;

            // 1. محاولة استدعاء المترجم الخارجي للحصول على شجرة الإعراب والرموز بصيغة JSON
            if (!string.IsNullOrEmpty(compilerExe) && File.Exists(compilerExe))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = compilerExe,
                        Arguments = $"\"{tempSourceFile}\" --json",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        string jsonOutput = process.StandardOutput.ReadToEnd();
                        process.WaitForExit(5000);

                        if (!string.IsNullOrWhiteSpace(jsonOutput) && jsonOutput.Contains("IsSuccess"))
                        {
                            result = JsonSerializer.Deserialize<CompilationResult>(jsonOutput);
                        }
                    }
                }
                catch
                {
                    // Fallback to internal engine if process call encountered an OS issue
                }
                finally
                {
                    try { if (File.Exists(tempSourceFile)) File.Delete(tempSourceFile); } catch { }
                }
            }

            // 2. استخدام المترجم المدمج مباشرة لضمان أعلى دقة وتزامن لحظي
            if (result == null)
            {
                result = CompilerRunner.Compile(_codeEditor.Text, isVerbose: false);
            }

            DisplayCompilationResult(result, isExecutionRun: true);

            // 3. تشغيل البرنامج في نافذة كونسول مستقلة (Visual Studio Console)
            if (result.IsSuccess || (result.SyntaxErrors.Count == 0 && result.TAC.Count > 0))
            {
                var consoleForm = new VsConsoleForm(result.TAC, _currentFilePath ?? "arabic_program.arb");
                consoleForm.Show(this);
            }
        }

        private void DisplayCompilationResult(CompilationResult result, bool isExecutionRun)
        {
            _lastResult = result;
            ClearOutputs();

            // 1. عرض الرموز المعجمية (Tokens)
            int tokenIdx = 1;
            foreach (var t in result.Tokens)
            {
                _tokensGrid.Rows.Add(tokenIdx++, t.Value, t.Type, t.Line);
            }

            // 2. عرض شجرة الإعراب (Parse Tree) والشجرة المجردة (AST)
            if (result.AST != null)
            {
                // أ. شجرة الإعراب النحوية (Parse Tree / CST)
                _cstTreeView.Nodes.Clear();
                var cstRootNode = BuildParseTreeFromDto(result.AST, result.Tokens);
                _cstTreeView.Nodes.Add(cstRootNode);
                _cstTreeView.ExpandAll();

                // ب. الشجرة المجردة الدلالية (AST)
                _astTreeView.Nodes.Clear();
                var astRootNode = BuildTreeNodeFromDto(result.AST);
                _astTreeView.Nodes.Add(astRootNode);
                _astTreeView.ExpandAll();
            }

            // 3. عرض جدول الرموز (Symbol Table)
            foreach (var s in result.SymbolTable)
            {
                _symbolTableGrid.Rows.Add(s.Name, s.DataType, s.Kind, s.Value, s.DeclaredLine, s.ReferencedLines);
            }

            // 4. عرض الكود الوسيط (TAC)
            var tacSb = new StringBuilder();
            foreach (var line in result.TAC)
            {
                tacSb.AppendLine(line);
            }
            _tacTextBox.Text = tacSb.ToString();

            // 5. عرض لغة التجميع (x86 Assembly)
            _asmTextBox.Text = result.AssemblyCode;

            // 6. عرض .NET CIL
            _cilTextBox.Text = result.CILCode;

            // 7. عرض الأخطاء (Errors)
            bool hasErrors = false;
            var consoleSb = new StringBuilder();
            int errorNumber = 1;

            if (result.SyntaxErrors.Count > 0)
            {
                hasErrors = true;
                foreach (var err in result.SyntaxErrors)
                {
                    int line = ExtractLineNumber(err);
                    _errorsGrid.Rows.Add(errorNumber++, line > 0 ? line.ToString() : "-", "خطأ نحوي (Syntax)", err);
                    consoleSb.AppendLine($"⚠️ [خطأ نحوي]: {err}");
                }
            }

            if (result.SemanticErrors.Count > 0)
            {
                hasErrors = true;
                foreach (var err in result.SemanticErrors)
                {
                    int line = ExtractLineNumber(err);
                    _errorsGrid.Rows.Add(errorNumber++, line > 0 ? line.ToString() : "-", "خطأ دلالي (Semantic)", err);
                    consoleSb.AppendLine($"⚠️ [خطأ دلالي]: {err}");
                }
            }

            int totalErrors = result.SyntaxErrors.Count + result.SemanticErrors.Count;
            _tabErrors.Text = hasErrors ? $"⚠️ قائمة الأخطاء ({totalErrors})" : "⚠️ قائمة الأخطاء";

            // 8. التعامل مع شاشة التشغيل (Visual Studio Command Prompt Theme)
            string sourceDisplayName = string.IsNullOrEmpty(_currentFilePath) ? "source.arb" : Path.GetFileName(_currentFilePath);

            if (hasErrors)
            {
                var errorBanner = new StringBuilder();
                errorBanner.AppendLine("========== بدء البناء: فحص واكتشاف الأخطاء ==========");
                errorBanner.AppendLine($"❌ فشلت الترجمة: تم اكتشاف ({totalErrors}) خطأ في الكود المصدري.");
                errorBanner.AppendLine();
                errorBanner.Append(consoleSb);
                errorBanner.AppendLine();
                errorBanner.AppendLine("========== انتهى البناء بفشل: 0 نجاح، 1 فشل ==========");

                _consoleOutputTextBox.ForeColor = Color.FromArgb(255, 120, 120);
                _consoleOutputTextBox.Text = errorBanner.ToString();

                _statusLabel.Text = $"❌ تم اكتشاف ({totalErrors}) خطأ في الكود (انظر تبويب قائمة الأخطاء)";

                if (isExecutionRun)
                {
                    _outputTabControl.SelectedTab = _tabErrors; // الانتقال التلقائي لتبويب قائمة الأخطاء
                }
            }
            else
            {
                var successSb = new StringBuilder();
                successSb.AppendLine("========== بدء البناء والترجمة: مشروع لغة البرمجة العربية ==========");
                successSb.AppendLine("1> المحلل المعجمي (Lexer): تم استخراج كافة الرموز بنجاح.");
                successSb.AppendLine("1> المحلل النحوي (Parser): تم بناء شجرة الإعراب (AST) بنجاح.");
                successSb.AppendLine("1> المحلل الدلالي (Semantic): تم فحص جدول الرموز والأنواع بنجاح بدون أخطاء.");
                successSb.AppendLine($"1> الكود الوسيط (TAC): تم توليد ({result.TAC.Count}) تعليمة ثلاثية العناوين.");
                successSb.AppendLine("1> مولد كود التجميع (Assembly): تم توليد كود x86 و .NET CIL بنجاح.");
                successSb.AppendLine("========== البناء: نجح 1، فشل 0، تم التحديث 0 ==========");
                


                if (isExecutionRun)
                {
                    successSb.AppendLine();
                    successSb.AppendLine("🚀 تم إطلاق البرنامج في نافذة تنفيذ أوامر مستقلة منفصلة (Visual Studio Debug Console)...");
                    successSb.AppendLine("💡 يمكنك إدخال القيم لتعليمة 'اقرا' مباشرة عبر لوحة المفاتيح والضغط على Enter.");
                    _statusLabel.Text = "✔️ تم البناء بنجاح وإطلاق البرنامج في نافذة CMD خارجية مستقلة";
                }
                else
                {
                    _statusLabel.Text = "✔️ الكود البرمجي سليم نحوياً ودلالياً 100%";
                }

                _consoleOutputTextBox.ForeColor = Color.FromArgb(180, 230, 180);
                _consoleOutputTextBox.Text = successSb.ToString();
            }
        }

        private void NavigateToErrorLine()
        {
            if (_errorsGrid.SelectedRows.Count == 0) return;

            var row = _errorsGrid.SelectedRows[0];
            string lineStr = row.Cells[1].Value?.ToString() ?? "";
            string msg = row.Cells[3].Value?.ToString() ?? "";

            int line = int.TryParse(lineStr, out int parsedLine) ? parsedLine : ExtractLineNumber(msg);
            if (line > 0 && line <= _codeEditor.Lines.Length)
            {
                int charIndex = _codeEditor.GetFirstCharIndexFromLine(line - 1);
                if (charIndex >= 0)
                {
                    _codeEditor.Focus();
                    _codeEditor.Select(charIndex, _codeEditor.Lines[line - 1].Length);
                    _codeEditor.ScrollToCaret();
                }
            }
        }

        private static int ExtractLineNumber(string text)
        {
            // البحث عن نمط "السطر X"
            int idx = text.IndexOf("السطر");
            if (idx >= 0)
            {
                var sb = new StringBuilder();
                for (int i = idx + 5; i < text.Length; i++)
                {
                    if (char.IsDigit(text[i])) sb.Append(text[i]);
                    else if (sb.Length > 0) break;
                }
                if (int.TryParse(sb.ToString(), out int line)) return line;
            }
            return -1;
        }

        private TreeNode BuildTreeNodeFromDto(NodeDto nodeDto)
        {
            string label = nodeDto.Value;
            if (!string.IsNullOrEmpty(nodeDto.Name)) label += $": {nodeDto.Name}";
            if (!string.IsNullOrEmpty(nodeDto.DataType)) label += $" [{nodeDto.DataType}]";
            if (!string.IsNullOrEmpty(nodeDto.Val)) label += $" = {nodeDto.Val}";

            var treeNode = new TreeNode(label);
            foreach (var child in nodeDto.Children)
            {
                treeNode.Nodes.Add(BuildTreeNodeFromDto(child));
            }
            return treeNode;
        }

        private TreeNode BuildParseTreeFromDto(NodeDto? astRoot, List<TokenDto> tokens)
        {
            var root = new TreeNode("<البرنامج: Program>");
            if (astRoot == null) return root;

            // 1. ترويسة البرنامج
            string progName = !string.IsNullOrEmpty(astRoot.Name) ? astRoot.Name : "برنامج_حساب";
            var headerNode = new TreeNode("<ترويسة_البرنامج: ProgramHeader>");
            headerNode.Nodes.Add(new TreeNode("كلمة_مفتاحية: برنامج"));
            headerNode.Nodes.Add(new TreeNode($"معرف_البرنامج: {progName}"));
            headerNode.Nodes.Add(new TreeNode("رمز_طرفي: ؛"));
            root.Nodes.Add(headerNode);

            // 2. قسم التصريحات
            var declDto = astRoot.Children.Find(c => c.Value == "Declarations");
            if (declDto != null)
            {
                var declCst = new TreeNode("<قسم_التصريحات: Declarations>");

                var constDto = declDto.Children.Find(c => c.Value == "ConstDeclarations");
                if (constDto != null && constDto.Children.Count > 0)
                {
                    var constCst = new TreeNode("<تصريح_الثوابت: ConstDeclarations>");
                    constCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: ثابت"));
                    foreach (var c in constDto.Children)
                    {
                        var cDef = new TreeNode($"<تعريف_ثابت>: {c.Name} = {c.Val}");
                        cDef.Nodes.Add(new TreeNode($"معرف_الثابت: {c.Name}"));
                        cDef.Nodes.Add(new TreeNode("معامل_الإسناد: ="));
                        cDef.Nodes.Add(new TreeNode($"قيمة_الثابت: {c.Val}"));
                        cDef.Nodes.Add(new TreeNode("رمز_طرفي: ؛"));
                        constCst.Nodes.Add(cDef);
                    }
                    declCst.Nodes.Add(constCst);
                }

                var varDto = declDto.Children.Find(c => c.Value == "VarDeclarations");
                if (varDto != null && varDto.Children.Count > 0)
                {
                    var varCst = new TreeNode("<تصريح_المتغيرات: VarDeclarations>");
                    varCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: متغير"));
                    foreach (var v in varDto.Children)
                    {
                        var vDef = new TreeNode($"<تصريح_متغير>: {v.Name} : {v.DataType}");
                        vDef.Nodes.Add(new TreeNode($"معرف_المتغير: {v.Name}"));
                        vDef.Nodes.Add(new TreeNode("رمز_طرفي: :"));
                        vDef.Nodes.Add(new TreeNode($"نوع_البيانات: {v.DataType}"));
                        vDef.Nodes.Add(new TreeNode("رمز_طرفي: ؛"));
                        varCst.Nodes.Add(vDef);
                    }
                    declCst.Nodes.Add(varCst);
                }

                root.Nodes.Add(declCst);
            }

            // 3. كتلة التعليمات
            var blockDto = astRoot.Children.Find(c => c.Value == "Block");
            if (blockDto != null)
            {
                var blockCst = new TreeNode("<كتلة_التعليمات: Block>");
                blockCst.Nodes.Add(new TreeNode("قوس_بداية: {"));

                var stmtsCst = new TreeNode("<قائمة_التعليمات: StatementList>");
                foreach (var stmt in blockDto.Children)
                {
                    stmtsCst.Nodes.Add(BuildCstStatementNode(stmt));
                }
                blockCst.Nodes.Add(stmtsCst);

                blockCst.Nodes.Add(new TreeNode("قوس_نهاية: }"));
                root.Nodes.Add(blockCst);
            }

            // 4. نهاية البرنامج
            root.Nodes.Add(new TreeNode("رمز_طرفي (نقطة_النهاية): ."));
            return root;
        }

        private TreeNode BuildCstStatementNode(NodeDto node)
        {
            if (node.Value == "Assign" || node.Value == "Assignment")
            {
                var nodeCst = new TreeNode($"<تعليمة_إسناد>: {node.Name} = ...");
                nodeCst.Nodes.Add(new TreeNode($"معرف_المتغير: {node.Name}"));
                nodeCst.Nodes.Add(new TreeNode("معامل_الإسناد: ="));
                if (node.Children.Count > 0)
                {
                    var exprCst = new TreeNode("<تعبير_حسابي>");
                    foreach (var c in node.Children) exprCst.Nodes.Add(BuildCstExpressionNode(c));
                    nodeCst.Nodes.Add(exprCst);
                }
                else if (!string.IsNullOrEmpty(node.Val))
                {
                    nodeCst.Nodes.Add(new TreeNode($"قيمة: {node.Val}"));
                }
                nodeCst.Nodes.Add(new TreeNode("رمز_طرفي: ؛"));
                return nodeCst;
            }
            if (node.Value.StartsWith("If") || node.Value.Contains("اذا"))
            {
                var ifCst = new TreeNode("<تعليمة_شرطية: IfStatement>");
                ifCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: اذا"));
                ifCst.Nodes.Add(new TreeNode("قوس_شرط: ("));
                if (node.Children.Count > 0)
                {
                    var condCst = new TreeNode("<تعبير_منطقي_للشرط>");
                    condCst.Nodes.Add(BuildCstExpressionNode(node.Children[0]));
                    ifCst.Nodes.Add(condCst);
                }
                ifCst.Nodes.Add(new TreeNode("قوس_شرط: )"));
                ifCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: فان"));
                if (node.Children.Count > 1)
                {
                    var thenCst = new TreeNode("<كتلة_التحقق: ThenBlock>");
                    thenCst.Nodes.Add(new TreeNode("قوس: {"));
                    foreach (var stmt in node.Children[1].Children) thenCst.Nodes.Add(BuildCstStatementNode(stmt));
                    thenCst.Nodes.Add(new TreeNode("قوس: }"));
                    ifCst.Nodes.Add(thenCst);
                }
                if (node.Children.Count > 2)
                {
                    ifCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: والا"));
                    var elseCst = new TreeNode("<كتلة_البديل: ElseBlock>");
                    elseCst.Nodes.Add(new TreeNode("قوس: {"));
                    foreach (var stmt in node.Children[2].Children) elseCst.Nodes.Add(BuildCstStatementNode(stmt));
                    elseCst.Nodes.Add(new TreeNode("قوس: }"));
                    ifCst.Nodes.Add(elseCst);
                }
                ifCst.Nodes.Add(new TreeNode("رمز_طرفي: ؛"));
                return ifCst;
            }
            if (node.Value.StartsWith("While") || node.Value.Contains("طالما"))
            {
                var loopCst = new TreeNode("<حلقة_طالما_التكرارية: WhileStatement>");
                loopCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: طالما"));
                loopCst.Nodes.Add(new TreeNode("قوس: ("));
                if (node.Children.Count > 0) loopCst.Nodes.Add(BuildCstExpressionNode(node.Children[0]));
                loopCst.Nodes.Add(new TreeNode("قوس: )"));
                loopCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: كرر"));
                if (node.Children.Count > 1)
                {
                    var body = new TreeNode("<جسم_الحلقة: LoopBody>");
                    foreach (var s in node.Children[1].Children) body.Nodes.Add(BuildCstStatementNode(s));
                    loopCst.Nodes.Add(body);
                }
                loopCst.Nodes.Add(new TreeNode("رمز_طرفي: ؛"));
                return loopCst;
            }
            if (node.Value.StartsWith("Repeat") || node.Value.Contains("اعد"))
            {
                var loopCst = new TreeNode("<حلقة_أعد_حتى: RepeatUntilStatement>");
                loopCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: اعد"));
                if (node.Children.Count > 0)
                {
                    var body = new TreeNode("<جسم_الحلقة: LoopBody>");
                    foreach (var s in node.Children[0].Children) body.Nodes.Add(BuildCstStatementNode(s));
                    loopCst.Nodes.Add(body);
                }
                loopCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: حتى"));
                loopCst.Nodes.Add(new TreeNode("قوس: ("));
                if (node.Children.Count > 1) loopCst.Nodes.Add(BuildCstExpressionNode(node.Children[1]));
                loopCst.Nodes.Add(new TreeNode("قوس: )"));
                loopCst.Nodes.Add(new TreeNode("رمز_طرفي: ؛"));
                return loopCst;
            }
            if (node.Value.StartsWith("For") || node.Value.Contains("كرر"))
            {
                var forCst = new TreeNode("<حلقة_العداد_كرر: ForStatement>");
                forCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: كرر"));
                forCst.Nodes.Add(new TreeNode($"معرف_العداد: {node.Name}"));
                forCst.Nodes.Add(new TreeNode("معامل_الإسناد: ="));
                forCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: الى"));
                if (node.Children.Count > 0)
                {
                    var body = new TreeNode("<جسم_الحلقة: LoopBody>");
                    foreach (var s in node.Children[0].Children) body.Nodes.Add(BuildCstStatementNode(s));
                    forCst.Nodes.Add(body);
                }
                forCst.Nodes.Add(new TreeNode("رمز_طرفي: ؛"));
                return forCst;
            }
            if (node.Value == "Print" || node.Value.Contains("اطبع"))
            {
                var prCst = new TreeNode("<تعليمة_طباعة: PrintStatement>");
                prCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: اطبع"));
                prCst.Nodes.Add(new TreeNode("قوس: ("));
                foreach (var arg in node.Children) prCst.Nodes.Add(BuildCstExpressionNode(arg));
                prCst.Nodes.Add(new TreeNode("قوس: )"));
                prCst.Nodes.Add(new TreeNode("رمز_طرفي: ؛"));
                return prCst;
            }
            if (node.Value == "Read" || node.Value.Contains("اقرا"))
            {
                var rdCst = new TreeNode("<تعليمة_قراءة: ReadStatement>");
                rdCst.Nodes.Add(new TreeNode("كلمة_مفتاحية: اقرا"));
                rdCst.Nodes.Add(new TreeNode("قوس: ("));
                if (!string.IsNullOrEmpty(node.Name)) rdCst.Nodes.Add(new TreeNode($"معرف_المتغير: {node.Name}"));
                rdCst.Nodes.Add(new TreeNode("قوس: )"));
                rdCst.Nodes.Add(new TreeNode("رمز_طرفي: ؛"));
                return rdCst;
            }

            var genCst = new TreeNode($"<{node.Value}>");
            foreach (var c in node.Children) genCst.Nodes.Add(BuildCstStatementNode(c));
            return genCst;
        }

        private TreeNode BuildCstExpressionNode(NodeDto node)
        {
            if (node.Children.Count > 0)
            {
                var opNode = new TreeNode($"<عملية: {node.Value}>");
                foreach (var c in node.Children) opNode.Nodes.Add(BuildCstExpressionNode(c));
                return opNode;
            }
            string val = !string.IsNullOrEmpty(node.Val) ? node.Val : (!string.IsNullOrEmpty(node.Name) ? node.Name : node.Value);
            return new TreeNode($"رمز_طرفي: {val}");
        }

        private static void FormatTreeNodeText(TreeNode node, StringBuilder sb, string indent)
        {
            sb.AppendLine(indent + (node.Parent == null ? "• " : "├── ") + node.Text);
            foreach (TreeNode child in node.Nodes)
            {
                FormatTreeNodeText(child, sb, indent + "    ");
            }
        }

        private static string FormatAstToString(NodeDto? node, string indent = "", bool isLast = true)
        {
            if (node == null) return "";
            var sb = new StringBuilder();
            sb.Append(indent);
            sb.Append(isLast ? "└── " : "├── ");

            string details = node.Value;
            if (!string.IsNullOrEmpty(node.Name)) details += $" [اسم: {node.Name}]";
            if (!string.IsNullOrEmpty(node.DataType)) details += $" [نوع: {node.DataType}]";
            if (!string.IsNullOrEmpty(node.Val)) details += $" [قيمة: {node.Val}]";
            if (node.Line > 0) details += $" (السطر {node.Line})";

            sb.AppendLine(details);

            indent += isLast ? "    " : "│   ";
            for (int i = 0; i < node.Children.Count; i++)
            {
                sb.Append(FormatAstToString(node.Children[i], indent, i == node.Children.Count - 1));
            }
            return sb.ToString();
        }

        private void RefreshInterface()
        {
            _statusLabel.Text = "جاري تحديث الواجهة والتحليل اللحظي...";
            UpdateLineNumbers();
            _lineNumbersPanel.Invalidate();
            _codeEditor.Invalidate();
            RunLiveAnalysis();
            _statusLabel.Text = "تم تحديث الواجهة والتحليل اللحظي بنجاح ✔️";
        }

        private void RunInExternalCmd()
        {
            string compilerExe = FindCompilerExe();
            if (string.IsNullOrEmpty(compilerExe) || !File.Exists(compilerExe))
            {
                MessageBox.Show("تعذر العثور على الملف التنفيذي للمترجم CompilerProject_CPP.exe", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string tempDir = Path.GetTempPath();
            string tempSourceFile = Path.Combine(tempDir, "arabic_program.arb");
            File.WriteAllText(tempSourceFile, _codeEditor.Text, new UTF8Encoding(false));

            string batchFile = Path.Combine(tempDir, "run_vs_cmd.bat");
            var batchContent = new StringBuilder();
            batchContent.AppendLine("@echo off");
            batchContent.AppendLine("chcp 65001 >nul");
            batchContent.AppendLine("title Visual Studio Debug Console");
            batchContent.AppendLine($"\"{compilerExe}\" \"{tempSourceFile}\" --run");

            File.WriteAllText(batchFile, batchContent.ToString(), new UTF8Encoding(false));

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchFile}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(compilerExe) ?? AppDomain.CurrentDomain.BaseDirectory
                };
                var proc = Process.Start(psi);
                if (proc != null) _spawnedProcesses.Add(proc);
                _statusLabel.Text = "تم إطلاق البرنامج في نافذة CMD خارجية مستقلة (Visual Studio Console) بنجاح 💻";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر فتح نافذة موجه الأوامر: {ex.Message}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string FindCompilerExe()
        {
            string extracted = EnsureCompilerCppExtracted();
            if (!string.IsNullOrEmpty(extracted) && File.Exists(extracted))
                return extracted;

            string targetExe = "CompilerProject_CPP.exe";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string currDir = Directory.GetCurrentDirectory();

            string[] possiblePaths = new[]
            {
                Path.Combine(baseDir, targetExe),
                Path.Combine(Path.GetTempPath(), targetExe),
                Path.Combine(baseDir, "..", "..", "..", "..", "CompilerProject_CPP", targetExe),
                Path.Combine(baseDir, "..", "..", "CompilerProject_CPP", targetExe),
                Path.Combine(currDir, targetExe),
                Path.Combine(currDir, "CompilerProject_CPP", targetExe),
                @"d:\IT FILES\level 4\مترجمات\نظري\project\Project_Compiler\CompilerProject_CPP\CompilerProject_CPP.exe",
                @"d:\IT FILES\level 4\مترجمات\نظري\project\Project_Compiler\تسليم_المشروع_Final_Delivery\CompilerProject_CPP.exe"
            };

            foreach (var p in possiblePaths)
            {
                try
                {
                    if (File.Exists(p)) return Path.GetFullPath(p);
                }
                catch { }
            }

            return "";
        }

        private static string EnsureCompilerCppExtracted()
        {
            string targetExe = "CompilerProject_CPP.exe";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localPath = Path.Combine(baseDir, targetExe);
            if (File.Exists(localPath) && new FileInfo(localPath).Length > 10000) return localPath;

            string tempPath = Path.Combine(Path.GetTempPath(), targetExe);
            if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 10000) return tempPath;

            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                string? resName = null;
                foreach (var n in asm.GetManifestResourceNames())
                {
                    if (n.EndsWith("CompilerProject_CPP.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        resName = n;
                        break;
                    }
                }

                if (resName != null)
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        try
                        {
                            using (var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                stream.CopyTo(fs);
                            }
                            if (File.Exists(localPath) && new FileInfo(localPath).Length > 10000) return localPath;
                        }
                        catch { }

                        stream.Position = 0;
                        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            stream.CopyTo(fs);
                        }
                        if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 10000) return tempPath;
                    }
                }
            }
            catch { }

            return "";
        }

        private void ShowAboutDialog()
        {
            MessageBox.Show(
                "مشروع مقرر بناء المترجمات (CS 351)\n" +
                "بيئة التطوير ومترجم لغة البرمجة العربية المتكامل\n\n" +
                "إشراف: أ/ عقيل الشرفي & د. خالد الكحصة\n" +
                "جامعة إب - كلية الحاسوب وتكنولوجيا المعلومات (2025/2026)\n\n" +
                "المميزات:\n" +
                "- تحديث وتحليل لحظي مباشر أثناء الكتابة (Live Analysis)\n" +
                "- محرر كود عربي بالكامل (RTL) مع شريط أرقام الأسطر\n" +
                "- مترجم منفصل بـ 6 مراحل للترجمة\n" +
                "- دعم 10 أمثلة اختبارية متنوعة\n" +
                "- كشف فوري للأخطاء النحوية والدلالية والانتقال التلقائي للسطر\n" +
                "- توليد كود تنفيذي حقيقي x86 و .NET",
                "عن المشروع",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
