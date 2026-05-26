FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Packages.props", "."]
COPY ["NuGet.Config", "."]
COPY ["src/Shipping.Api/Shipping.Api.csproj", "src/Shipping.Api/"]
RUN dotnet restore "src/Shipping.Api/Shipping.Api.csproj"

COPY . .
RUN dotnet publish "src/Shipping.Api/Shipping.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Shipping.Api.dll"]
