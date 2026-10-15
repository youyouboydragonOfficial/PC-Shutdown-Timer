; =====================================================================
; PCシャットダウン予約ツール (PC Shutdown Timer)
; インストーラー定義スクリプト (Inno Setup 6)
; 開発・著作権: © youyoubodragon
; =====================================================================

#define MyAppName "PCシャットダウン予約ツール"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "youyoubodragon"
#define MyAppURL "https://github.com/youyouboydragonOfficial/PC-Shutdown-Timer"
#define MyAppExeName "PCShutdownTimer.exe"
#define MyAppCopyright "© youyoubodragon"

[Setup]
; アプリケーション一意識別子
AppId={{8B8A61D2-921C-4B2E-BE6C-3C6F77B132C1}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
AppCopyright={#MyAppCopyright}
DefaultDirName={autopf}\PCShutdownTimer
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; 管理者権限がなくてもユーザー領域に安全にインストール可能、ダイアログで全ユーザー/現在ユーザー選択可能
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir=dist
OutputBaseFilename=PCShutdownTimer_Setup_v{#MyAppVersion}
SetupIconFile=icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} (PC Shutdown Timer)
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} インストーラー
VersionInfoCopyright={#MyAppCopyright}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion=1.0.0.0

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish_app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[CustomMessages]
japanese.LaunchProgram=PCシャットダウン予約ツールを今すぐ起動する
english.LaunchProgram=Launch PC Shutdown Timer now
