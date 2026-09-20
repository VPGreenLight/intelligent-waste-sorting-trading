FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY ["Ecolink.Domain/Ecolink.Domain.csproj", "Ecolink.Domain/"]
COPY ["Ecolink.Application/Ecolink.Application.csproj", "Ecolink.Application/"]
COPY ["Ecolink.Infrastructure/Ecolink.Infrastructure.csproj", "Ecolink.Infrastructure/"]
COPY ["Ecolink.WebApi/Ecolink.WebApi.csproj", "Ecolink.WebApi/"]

RUN dotnet restore "Ecolink.WebApi/Ecolink.WebApi.csproj"

# Copy all code and build
COPY . .
WORKDIR "/src/Ecolink.WebApi"
RUN dotnet build "Ecolink.WebApi.csproj" -c Release -o /app/build
RUN dotnet publish "Ecolink.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "Ecolink.WebApi.dll"]
