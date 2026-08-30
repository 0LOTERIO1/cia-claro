FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/Cia.Api.csproj backend/
RUN dotnet restore backend/Cia.Api.csproj

COPY backend/ backend/

RUN dotnet publish backend/Cia.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Cia.Api.dll"]
