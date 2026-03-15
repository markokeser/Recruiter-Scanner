# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files - koristi navodnike za putanje sa razmacima
COPY *.sln .
COPY "Recriter Scanner/Recriter Scanner.csproj" "Recriter Scanner/"
RUN dotnet restore "Recriter Scanner/Recriter Scanner.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/app/Recriter Scanner"
RUN dotnet publish -c Release -o out

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build "/app/Recriter Scanner/out" .
ENTRYPOINT ["dotnet", "Recriter Scanner.dll"]