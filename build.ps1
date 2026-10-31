$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler '/nologo' '/target:winexe' '/platform:x64' "/out:$PSScriptRoot\QuotaWidget.exe" '/reference:System.Windows.Forms.dll' '/reference:System.Drawing.dll' '/reference:System.Web.Extensions.dll' '/reference:System.Security.dll' "/reference:$PSScriptRoot\Microsoft.Web.WebView2.Core.dll" "/reference:$PSScriptRoot\Microsoft.Web.WebView2.WinForms.dll" "$PSScriptRoot\QuotaWidget.cs" "$PSScriptRoot\GrokSession.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
