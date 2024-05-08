FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
WORKDIR /src
COPY ["EventAndCommands/EventAndCommands.csproj", "EventAndCommands/"]
RUN dotnet restore "EventAndCommands/EventAndCommands.csproj"
COPY . .
WORKDIR "/src/EventAndCommands"
RUN dotnet build "EventAndCommands.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=publish /app/publish .