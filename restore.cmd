@ECHO OFF
PUSHD "%~dp0"

echo dotnet restore "%~dp0AzureRelayServer.sln"
call dotnet restore "%~dp0AzureRelayServer.sln" || (
    echo ERROR: couldn't restore packages
    exit /B 1
)

POPD
EXIT /B 0