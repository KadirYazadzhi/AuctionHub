# Използваме .NET 8 SDK за билдване
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копираме solution файла и всички проектни файлове с правилните относителни пътища
COPY ["AuctionHub.sln", "./"]
COPY ["AuctionHub/AuctionHub.csproj", "AuctionHub/"]
COPY ["AuctionHub.Application/AuctionHub.Application.csproj", "AuctionHub.Application/"]
COPY ["AuctionHub.Domain/AuctionHub.Domain.csproj", "AuctionHub.Domain/"]
COPY ["AuctionHub.Infrastructure/AuctionHub.Infrastructure.csproj", "AuctionHub.Infrastructure/"]

# Ресторваме зависимостите
RUN dotnet restore "AuctionHub.sln"

# Копираме целия останал код
COPY . .
WORKDIR "/src/AuctionHub"

# Билдваме и пъблишваме
RUN dotnet build "AuctionHub.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AuctionHub.csproj" -c Release -o /app/publish

# Създаваме финалния лек рън-тайм имидж
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AuctionHub.dll"]
