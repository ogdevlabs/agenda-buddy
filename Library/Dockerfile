FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Library/Library.csproj", "Library/"]
RUN dotnet restore "Library/Library.csproj"
COPY . .
WORKDIR "/src/Library"
RUN dotnet build "Library.csproj" -c Release -o /app/build


FROM build AS publish
RUN dotnet publish --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=publish /app/publish .
