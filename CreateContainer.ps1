rm -Force -ErrorAction Ignore -Recurse $PSScriptRoot/artifacts

cd $PSScriptRoot/src/BynExchangeTelegramBot
dotnet publish -c Release -o $PSScriptRoot/artifacts

docker build -t shchegrikovich/byn-exchange-bot $PSScriptRoot