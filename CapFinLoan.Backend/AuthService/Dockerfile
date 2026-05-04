# Build context: CapFinLoan.Backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Layer 1: restore (cached until .csproj changes)
COPY Shared/CapFinLoan.Messaging.Contracts/CapFinLoan.Messaging.Contracts.csproj \
     Shared/CapFinLoan.Messaging.Contracts/
COPY AuthService/CapFinLoan.Auth.API/CapFinLoan.Auth.API.csproj \
     AuthService/CapFinLoan.Auth.API/
COPY AuthService/CapFinLoan.Auth.Application/CapFinLoan.Auth.Application.csproj \
     AuthService/CapFinLoan.Auth.Application/
COPY AuthService/CapFinLoan.Auth.Domain/CapFinLoan.Auth.Domain.csproj \
     AuthService/CapFinLoan.Auth.Domain/
COPY AuthService/CapFinLoan.Auth.Infrastructure/CapFinLoan.Auth.Infrastructure.csproj \
     AuthService/CapFinLoan.Auth.Infrastructure/
COPY AuthService/CapFinLoan.Auth.Persistence/CapFinLoan.Auth.Persistence.csproj \
     AuthService/CapFinLoan.Auth.Persistence/

RUN dotnet restore AuthService/CapFinLoan.Auth.API/CapFinLoan.Auth.API.csproj

# Layer 2: build + publish
COPY . .
RUN dotnet publish AuthService/CapFinLoan.Auth.API/CapFinLoan.Auth.API.csproj \
    -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# wget for healthcheck (not in aspnet image by default)
RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*

RUN groupadd --system appgroup && useradd --system --gid appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CapFinLoan.Auth.API.dll"]
