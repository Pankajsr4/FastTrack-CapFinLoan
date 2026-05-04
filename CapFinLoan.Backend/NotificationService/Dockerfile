# Build context: CapFinLoan.Backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Shared/CapFinLoan.Messaging.Contracts/CapFinLoan.Messaging.Contracts.csproj \
     Shared/CapFinLoan.Messaging.Contracts/
COPY NotificationService/CapFinLoan.Notification.API/CapFinLoan.Notification.API.csproj \
     NotificationService/CapFinLoan.Notification.API/
COPY NotificationService/CapFinLoan.Notification.Application/CapFinLoan.Notification.Application.csproj \
     NotificationService/CapFinLoan.Notification.Application/
COPY NotificationService/CapFinLoan.Notification.Infrastructure/CapFinLoan.Notification.Infrastructure.csproj \
     NotificationService/CapFinLoan.Notification.Infrastructure/

RUN dotnet restore NotificationService/CapFinLoan.Notification.API/CapFinLoan.Notification.API.csproj

COPY . .
RUN dotnet publish NotificationService/CapFinLoan.Notification.API/CapFinLoan.Notification.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*
RUN groupadd --system appgroup && useradd --system --gid appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CapFinLoan.Notification.API.dll"]
