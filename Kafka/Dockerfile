FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Kafka/Kafka.csproj", "Kafka/"]
RUN dotnet restore "Kafka/Kafka.csproj"
COPY . .
WORKDIR "/src/Kafka"
RUN dotnet build "Kafka.csproj" -c Release -o /app/build


FROM build AS publish
RUN dotnet publish --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=publish /app/publish .
