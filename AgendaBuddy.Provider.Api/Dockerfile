FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AgendaBuddy.Provider.Api/AgendaBuddy.Provider.Api.csproj", "AgendaBuddy.Provider.Api/"]
RUN dotnet restore "AgendaBuddy.Provider.Api/AgendaBuddy.Provider.Api.csproj"
COPY . .
WORKDIR "/src/AgendaBuddy.Provider.Api"
RUN dotnet build "AgendaBuddy.Provider.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AgendaBuddy.Provider.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AgendaBuddy.Provider.Api.dll"]
