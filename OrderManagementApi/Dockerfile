FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrderManagementApi/OrderManagementApi.csproj", "OrderManagementApi/"]
RUN dotnet restore "OrderManagementApi/OrderManagementApi.csproj"
COPY . .
WORKDIR "/src/OrderManagementApi"
RUN dotnet build "OrderManagementApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderManagementApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 80
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderManagementApi.dll"]