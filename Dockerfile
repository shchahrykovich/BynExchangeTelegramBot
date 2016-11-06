FROM microsoft/dotnet:1.0.1-runtime
WORKDIR /app
COPY artifacts/ .
ENTRYPOINT ["dotnet", "BynExchangeTelegramBot.dll"]