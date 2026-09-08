FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["OrderManagementApp/OrderManagementApp.csproj", "OrderManagementApp/"]
RUN dotnet restore "OrderManagementApp/OrderManagementApp.csproj"

COPY . .
WORKDIR "/src/OrderManagementApp"
RUN dotnet publish "OrderManagementApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
COPY --from=build /app/publish/wwwroot .