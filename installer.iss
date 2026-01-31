; Notepad Installer Script for Inno Setup
; https://jrsoftware.org/isinfo.php

; Version can be overridden via command line: iscc /DMyAppVersion=1.2.3 installer.iss
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "Notepad"
#define MyAppPublisher "Gerard Smit"
#define MyAppURL "https://github.com/GerardSmit/notepad"
#define MyAppExeName "Notepad.exe"

; Windows App SDK Runtime installer URL (1.8.x)
#define WinAppSdkUrl "https://aka.ms/windowsappsdk/1.8/latest/windowsappruntimeinstall-x64.exe"
#define WinAppSdkInstallerName "windowsappruntimeinstall-x64.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
; Require admin rights for Program Files installation
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
; Output settings
OutputDir=.\installer
OutputBaseFilename=NotepadSetup-{#MyAppVersion}
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; Visual settings
SetupIconFile=Notepad\Assets\notebook.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern
; Windows version (Windows 10 1809 or later)
MinVersion=10.0.17763
; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; License
LicenseFile=LICENSE.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associatetxt"; Description: "Associate with .txt files"; GroupDescription: "File associations:"; Flags: unchecked

[Files]
; Main application files
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; License and readme
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; File association for .txt (optional, only if user selects the task)
Root: HKCR; Subkey: ".txt\OpenWithProgids"; ValueType: string; ValueName: "Notepad.TextFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associatetxt
Root: HKCR; Subkey: "Notepad.TextFile"; ValueType: string; ValueName: ""; ValueData: "Text File"; Flags: uninsdeletekey; Tasks: associatetxt
Root: HKCR; Subkey: "Notepad.TextFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associatetxt
Root: HKCR; Subkey: "Notepad.TextFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associatetxt

[Run]
; Install Windows App SDK Runtime silently before launching the app
Filename: "{tmp}\{#WinAppSdkInstallerName}"; Parameters: "--quiet"; StatusMsg: "Installing Windows App SDK Runtime..."; Flags: waituntilterminated; Check: NeedsWinAppSdk
; Launch the application after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DownloadPage: TDownloadWizardPage;

// Check if Windows App SDK Runtime is already installed
function IsWinAppSdkInstalled: Boolean;
var
  RegKey: String;
begin
  // Check for Windows App SDK 1.8 installation in registry
  // The runtime registers itself under this key
  RegKey := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall';
  Result := RegKeyExists(HKLM, RegKey + '\{D1A3B0A5-8C5A-4D0B-9D90-7E8C5A8D0B9D}_is1') or
            RegKeyExists(HKLM, 'SOFTWARE\Microsoft\WindowsAppRuntime');
  
  // Also check if the DLL exists in system32
  if not Result then
    Result := FileExists(ExpandConstant('{sys}\Microsoft.WindowsAppRuntime.Bootstrap.dll'));
end;

function NeedsWinAppSdk: Boolean;
begin
  Result := not IsWinAppSdkInstalled;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log(Format('Successfully downloaded file to {tmp}: %s', [FileName]));
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  
  if CurPageID = wpReady then
  begin
    // Check if we need to download Windows App SDK Runtime
    if NeedsWinAppSdk then
    begin
      DownloadPage.Clear;
      DownloadPage.Add('{#WinAppSdkUrl}', '{#WinAppSdkInstallerName}', '');
      DownloadPage.Show;
      try
        try
          DownloadPage.Download;
        except
          if DownloadPage.AbortedByUser then
            Log('Download aborted by user.')
          else
            SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbCriticalError, MB_OK, IDOK);
          Result := False;
        end;
      finally
        DownloadPage.Hide;
      end;
    end;
  end;
end;
