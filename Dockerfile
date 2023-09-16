FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 8089

ENV ASPNETCORE_URLS=http://+:8089

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["PhotoGallery.csproj", "./"]
RUN dotnet restore "PhotoGallery.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "PhotoGallery.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "PhotoGallery.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PhotoGallery.dll"]
