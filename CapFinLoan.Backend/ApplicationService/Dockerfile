# Build context: CapFinLoan.Backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG CACHEBUST=1
WORKDIR /src

COPY Shared/CapFinLoan.Messaging.Contracts/CapFinLoan.Messaging.Contracts.csproj \
     Shared/CapFinLoan.Messaging.Contracts/
COPY Shared/CapFinLoan.Api.Shared/CapFinLoan.Api.Shared.csproj \
     Shared/CapFinLoan.Api.Shared/
COPY ApplicationService/CapFinLoan.Application.API/CapFinLoan.Application.API.csproj \
     ApplicationService/CapFinLoan.Application.API/
COPY ApplicationService/CapFinLoan.Application.Application/CapFinLoan.Application.Application.csproj \
     ApplicationService/CapFinLoan.Application.Application/
COPY ApplicationService/CapFinLoan.Application.Domain/CapFinLoan.Application.Domain.csproj \
     ApplicationService/CapFinLoan.Application.Domain/
COPY ApplicationService/CapFinLoan.Application.Infrastructure/CapFinLoan.Application.Infrastructure.csproj \
     ApplicationService/CapFinLoan.Application.Infrastructure/
COPY ApplicationService/CapFinLoan.Application.Persistence/CapFinLoan.Application.Persistence.csproj \
     ApplicationService/CapFinLoan.Application.Persistence/

RUN dotnet restore ApplicationService/CapFinLoan.Application.API/CapFinLoan.Application.API.csproj

COPY . .
RUN dotnet publish ApplicationService/CapFinLoan.Application.API/CapFinLoan.Application.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*

RUN groupadd --system appgroup && useradd --system --gid appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CapFinLoan.Application.API.dll"]
