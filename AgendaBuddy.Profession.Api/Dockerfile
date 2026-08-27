FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AgendaBuddy.Profession.Api/AgendaBuddy.Profession.Api.csproj", "AgendaBuddy.Profession.Api/"]
RUN dotnet restore "AgendaBuddy.Profession.Api/AgendaBuddy.Profession.Api.csproj"
COPY . .
WORKDIR "/src/AgendaBuddy.Profession.Api"
RUN dotnet build "AgendaBuddy.Profession.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AgendaBuddy.Profession.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AgendaBuddy.Profession.Api.dll"]
