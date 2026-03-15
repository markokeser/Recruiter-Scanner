# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files
COPY *.sln .
COPY ["Recruiter Scanner/Recruiter Scanner.csproj", "Recruiter Scanner/"]
RUN dotnet restore "Recruiter Scanner/Recruiter Scanner.csproj"

# Copy everything else and build
COPY . .
WORKDIR /app/Recruiter Scanner
RUN dotnet publish "Recruiter Scanner.csproj" -c Release -o out

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build ["/app/Recruiter Scanner/out", "."]
ENTRYPOINT ["dotnet", "Recruiter Scanner.dll"]