FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore LinguaDesk.Api/LinguaDesk.Api.csproj
RUN dotnet publish LinguaDesk.Api/LinguaDesk.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5080
ENTRYPOINT ["dotnet", "LinguaDesk.Api.dll"]
