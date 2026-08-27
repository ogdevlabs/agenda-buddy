FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AgendaBuddy.Calendar.Api/AgendaBuddy.Calendar.Api.csproj", "AgendaBuddy.Calendar.Api/"]
RUN dotnet restore "AgendaBuddy.Calendar.Api/AgendaBuddy.Calendar.Api.csproj"
COPY . .
WORKDIR "/src/AgendaBuddy.Calendar.Api"
RUN dotnet build "AgendaBuddy.Calendar.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AgendaBuddy.Calendar.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AgendaBuddy.Calendar.Api.dll"]
