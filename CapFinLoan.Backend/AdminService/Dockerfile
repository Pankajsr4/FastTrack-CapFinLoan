# Build context: CapFinLoan.Backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Shared/CapFinLoan.Messaging.Contracts/CapFinLoan.Messaging.Contracts.csproj \
     Shared/CapFinLoan.Messaging.Contracts/
COPY Shared/CapFinLoan.Api.Shared/CapFinLoan.Api.Shared.csproj \
     Shared/CapFinLoan.Api.Shared/
COPY AdminService/CapFinLoan.Admin.API/CapFinLoan.Admin.API.csproj \
     AdminService/CapFinLoan.Admin.API/
COPY AdminService/CapFinLoan.Admin.Application/CapFinLoan.Admin.Application.csproj \
     AdminService/CapFinLoan.Admin.Application/
COPY AdminService/CapFinLoan.Admin.Domain/CapFinLoan.Admin.Domain.csproj \
     AdminService/CapFinLoan.Admin.Domain/
COPY AdminService/CapFinLoan.Admin.Infrastructure/CapFinLoan.Admin.Infrastructure.csproj \
     AdminService/CapFinLoan.Admin.Infrastructure/
COPY AdminService/CapFinLoan.Admin.Persistence/CapFinLoan.Admin.Persistence.csproj \
     AdminService/CapFinLoan.Admin.Persistence/

RUN dotnet restore AdminService/CapFinLoan.Admin.API/CapFinLoan.Admin.API.csproj

COPY . .
RUN dotnet publish AdminService/CapFinLoan.Admin.API/CapFinLoan.Admin.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*

RUN groupadd --system appgroup && useradd --system --gid appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CapFinLoan.Admin.API.dll"]
