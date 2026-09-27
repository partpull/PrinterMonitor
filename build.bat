@echo off
setlocal

rem ============================================================
rem  打印机状态助手 - 编译脚本
rem  使用 Windows 11 自带的 .NET Framework 编译器，
rem  不需要安装任何 SDK，产物是单个 PrinterMonitor.exe。
rem ============================================================

set "ROOT=%~dp0"
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "CSC=%FW%\csc.exe"

if not exist "%CSC%" (
    echo [错误] 找不到 C# 编译器：%CSC%
    echo 请确认系统为 Windows 10/11 且 .NET Framework 4.x 完好。
    exit /b 1
)

echo 正在编译...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:"%ROOT%PrinterMonitor.exe" ^
  /win32manifest:"%ROOT%app.manifest" ^
  /reference:"%FW%\System.dll" ^
  /reference:"%FW%\System.Core.dll" ^
  /reference:"%FW%\System.Drawing.dll" ^
  /reference:"%FW%\System.Windows.Forms.dll" ^
  /reference:"%FW%\System.Management.dll" ^
  "%ROOT%Program.cs" ^
  "%ROOT%PrinterService.cs" ^
  "%ROOT%Theme.cs" ^
  "%ROOT%Controls.cs" ^
  "%ROOT%MainForm.cs"

if errorlevel 1 (
    echo.
    echo [失败] 编译未通过，请查看上方错误信息。
    exit /b 1
)

echo.
echo [成功] 已生成：%ROOT%PrinterMonitor.exe
for %%F in ("%ROOT%PrinterMonitor.exe") do echo         大小：%%~zF 字节
exit /b 0
