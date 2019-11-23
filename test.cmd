@ECHO OFF
PUSHD "%~dp0"

if defined TF_BUILD (
    set BuildConfiguration=Release
) else (
    set BuildConfiguration=Debug
)

call dotnet test "%~dp0AzureRelayServer.sln" --no-build --no-restore --blame --logger:trx --configuration %BuildConfiguration% || (
    echo Failed to run tests
    exit /b 1
)

REM ------------------------------------------------------------


REM ------------------------------------------------------------

POPD
EXIT /B 0