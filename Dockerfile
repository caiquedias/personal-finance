FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PersonalFinance.Api/PersonalFinance.Api.csproj", "src/PersonalFinance.Api/"]
COPY ["src/PersonalFinance.Application/PersonalFinance.Application.csproj", "src/PersonalFinance.Application/"]
COPY ["src/PersonalFinance.Domain/PersonalFinance.Domain.csproj", "src/PersonalFinance.Domain/"]
COPY ["src/PersonalFinance.Infrastructure/PersonalFinance.Infrastructure.csproj", "src/PersonalFinance.Infrastructure/"]
RUN dotnet restore "src/PersonalFinance.Api/PersonalFinance.Api.csproj"
COPY . .
RUN dotnet publish "src/PersonalFinance.Api/PersonalFinance.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PersonalFinance.Api.dll"]
