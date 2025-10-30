# ---- BUILD STAGE ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj và restore dependencies
COPY ["CafeWeb.csproj", "./"]
RUN dotnet restore "./CafeWeb.csproj"

# Copy toàn bộ code và build
COPY . .
RUN dotnet publish "CafeWeb.csproj" -c Release -o /app/publish

# ---- RUNTIME STAGE ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CafeWeb.dll"]
