# ── build stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MonetaCore.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish MonetaCore.csproj -c Release -o /app/publish --no-restore

# ── runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MonetaCore.dll"]
