; سكريبت إعداد برنامج التثبيت (Inno Setup Installer Script)
; مشروع مقرر بناء المترجمات (CS 351) - جامعة إب 2025/2026

[Setup]
AppName=محرر ومترجم لغة البرمجة العربية المتكامل
AppVersion=1.0
DefaultDirName={autopf}\ArabicLanguageIDE
DefaultGroupName=Arabic Language IDE
OutputDir=.\Installer_Output
OutputBaseFilename=PREXE-GN_Setup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: ".\PREXE_SingleFile\PREXE-GN.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\PREXE_SingleFile\CompilerProject_CPP.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\PREXE_SingleFile\CompilerProject.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\PREXE_SingleFile\CompilerProject.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\PREXE_SingleFile\Examples\*"; DestDir: "{app}\Examples"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\محرر لغة البرمجة العربية"; Filename: "{app}\PREXE-GN.exe"
Name: "{autodesktop}\محرر لغة البرمجة العربية"; Filename: "{app}\PREXE-GN.exe"

[Run]
Filename: "{app}\PREXE-GN.exe"; Description: "تشغيل محرر ومترجم لغة البرمجة العربية الآن"; Flags: nowait postinstall skipifsilent
