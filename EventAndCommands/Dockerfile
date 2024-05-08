FROM mcr.microsoft.com/dotnet/sdk:8.0 as build-env
WORKDIR /src

COPY ["EventAndCommands/EventAndCommands.csproj", "EventAndCommands/"]
RUN dotnet restore "EventAndCommands/EventAndCommands.csproj"

COPY . .
WORKDIR "/src/EventAndCommnands"
RUN dotnet pack "EventAndCommands.csproj" -c Release -o /app/pack

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build-env /app/pack .
