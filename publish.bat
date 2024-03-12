set config=%1

cd %~dp0
:: Publish DebugServer
cd VSRAD.DebugServer
dotnet publish -r win-x64 -c %config% --self-contained=false /p:PublishSingleFile=true /p:IncludeSymbolsInSingleFile=true
dotnet publish -r linux-x64 -c %config% --self-contained=false /p:PublishSingleFile=true /p:IncludeSymbolsInSingleFile=true
cd ..
:: DebugServer
mkdir %config%
xcopy /E /Y "VSRAD.DebugServer\bin\%config%\netcoreapp3.1\win-x64\publish" "%config%\DebugServerW64\"
xcopy /E /Y "VSRAD.DebugServer\bin\%config%\netcoreapp3.1\linux-x64\publish" "%config%\DebugServerLinux64\"
:: RadeonAsmDebugger.vsix
copy /Y "VSRAD.Package\bin\%config%\RadeonAsmDebugger.vsix" "%config%\"
:: RadeonAsmSyntax.vsix
copy /Y "VSRAD.Syntax\bin\%config%\RadeonAsmSyntax.vsix" "%config%\"
:: Readme
copy /Y "README.md" "%config%\"