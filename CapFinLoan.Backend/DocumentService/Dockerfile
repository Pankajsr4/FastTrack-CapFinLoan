# Build context: CapFinLoan.Backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Shared/CapFinLoan.Messaging.Contracts/CapFinLoan.Messaging.Contracts.csproj \
     Shared/CapFinLoan.Messaging.Contracts/
COPY Shared/CapFinLoan.Api.Shared/CapFinLoan.Api.Shared.csproj \
     Shared/CapFinLoan.Api.Shared/
COPY DocumentService/CapFinLoan.Document.API/CapFinLoan.Document.API.csproj \
     DocumentService/CapFinLoan.Document.API/
COPY DocumentService/CapFinLoan.Document.Application/CapFinLoan.Document.Application.csproj \
     DocumentService/CapFinLoan.Document.Application/
COPY DocumentService/CapFinLoan.Document.Domain/CapFinLoan.Document.Domain.csproj \
     DocumentService/CapFinLoan.Document.Domain/
COPY DocumentService/CapFinLoan.Document.Infrastructure/CapFinLoan.Document.Infrastructure.csproj \
     DocumentService/CapFinLoan.Document.Infrastructure/
COPY DocumentService/CapFinLoan.Document.Persistence/CapFinLoan.Document.Persistence.csproj \
     DocumentService/CapFinLoan.Document.Persistence/

RUN dotnet restore DocumentService/CapFinLoan.Document.API/CapFinLoan.Document.API.csproj

COPY . .
RUN dotnet publish DocumentService/CapFinLoan.Document.API/CapFinLoan.Document.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*

RUN groupadd --system appgroup && useradd --system --gid appgroup appuser

COPY --from=build /app/publish .

# Create uploads dir owned by appuser AFTER copying publish artifacts
RUN mkdir -p /app/wwwroot/uploads && chown -R appuser:appgroup /app/wwwroot

USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CapFinLoan.Document.API.dll"]
