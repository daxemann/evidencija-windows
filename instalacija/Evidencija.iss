; Lovačka Evidencija za Windows – instalacija (Inno Setup 6)
; Gradi se skriptom alati\izgradi.ps1 (verzija se predaje izvana: /DVerzija=1.0.0)

#ifndef Verzija
  #define Verzija "0.0.0"
#endif

[Setup]
AppId={{6D3B1C52-7A4E-4F0B-9E43-3E2B5F8C1A77}
AppName=Lovačka Evidencija
AppVersion={#Verzija}
AppVerName=Lovačka Evidencija {#Verzija}
AppPublisher=Lovačka Evidencija (besplatno, AGPL-3.0)
AppPublisherURL=https://github.com/daxemann/evidencija-windows
VersionInfoVersion={#Verzija}
; bez administratorskih prava (instalira se za trenutnog korisnika) – stariji korisnici ne dobiju upit za lozinku
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={autopf}\Lovacka Evidencija
DefaultGroupName=Lovačka Evidencija
DisableProgramGroupPage=yes
; jednostavno za starije korisnike: bez izbora mape (zadana mapa uvijek odgovara)
DisableDirPage=yes
OutputDir=..\izlaz
OutputBaseFilename=Evidencija-Setup-{#Verzija}
SetupIconFile=..\src\Evidencija\evidencija.ico
UninstallDisplayIcon={app}\Evidencija.exe
UninstallDisplayName=Lovačka Evidencija
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 verzija 2004 ili novija
MinVersion=10.0.19041
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "hr"; MessagesFile: "compiler:Default.isl,Croatian.isl"

[Tasks]
Name: "radnapovrsina"; Description: "Ikona na radnoj površini"; GroupDescription: "Prečaci:"

[Files]
Source: "..\izlaz\program\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; WebView2 (prikaz programa) – na Windowsima 11 već postoji; instalira se samo ako nedostaje
Source: "MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NemaWebView2

[Icons]
Name: "{autoprograms}\Lovačka Evidencija"; Filename: "{app}\Evidencija.exe"
Name: "{autodesktop}\Lovačka Evidencija"; Filename: "{app}\Evidencija.exe"; Tasks: radnapovrsina

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Instalira se Microsoft WebView2 …"; Check: NemaWebView2; Flags: waituntilterminated
Filename: "{app}\Evidencija.exe"; Description: "Pokreni Lovačku Evidenciju"; Flags: nowait postinstall skipifsilent

; Podaci (%LOCALAPPDATA%\LovackaEvidencija) se pri deinstalaciji NAMJERNO ne brišu.

[Code]
const
  WebView2Kljuc = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function ImaVerziju(Korijen: Integer; Kljuc: String): Boolean;
var
  V: String;
begin
  Result := RegQueryStringValue(Korijen, Kljuc, 'pv', V) and (V <> '') and (V <> '0.0.0.0');
end;

function NemaWebView2: Boolean;
begin
  Result := not (ImaVerziju(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}')
              or ImaVerziju(HKLM, WebView2Kljuc)
              or ImaVerziju(HKCU, WebView2Kljuc));
end;
