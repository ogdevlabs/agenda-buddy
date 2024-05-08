FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Kafka/Kafka.csproj", "Kafka/"]
RUN dotnet restore "Kafka/Kafka.csproj"
COPY . .
WORKDIR "/src/Kafka"
RUN dotnet build "Kafka.csproj" -c Release -o /app/build


FROM build AS pack
RUN dotnet pack --no-build --no-restore --configuration Release --output /app/pack

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=pack /app/pack .
