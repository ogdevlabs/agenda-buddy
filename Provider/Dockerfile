FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 80
EXPOSE 881

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Provider/Provider.csproj", "Provider/"]
COPY ["Customer/Customer.csproj", "Customer/"]
RUN dotnet restore "Provider/Provider.csproj"
COPY . .
WORKDIR "/src/Provider"
RUN dotnet build "Provider.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Provider.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# RUN chown -R 200:200 /app
ENTRYPOINT ["dotnet", "Provider.dll"]
