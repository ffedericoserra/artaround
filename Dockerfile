# Build the navigator
FROM node:22-slim AS navigator
WORKDIR /repo
COPY frontend-marketplace/assets frontend-marketplace/assets
COPY frontend-navigator frontend-navigator
WORKDIR /repo/frontend-navigator
RUN npm ci && npm run build

# Build the API and a self-contained migration tool
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /repo/backend
COPY backend .
RUN dotnet tool restore \
    && dotnet publish ArtAround.Api -c Release -o /out \
    && dotnet ef migrations bundle --project ArtAround.Api -o /out/efbundle

# Run it
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /out .
COPY --from=navigator /repo/frontend-navigator/dist site/frontend-navigator/dist
COPY frontend-marketplace site/frontend-marketplace
COPY uploads site/uploads
COPY data site/data
ENV SiteRoot=site ASPNETCORE_URLS=http://+:8000
EXPOSE 8000
ENTRYPOINT ["dotnet", "ArtAround.Api.dll"]
