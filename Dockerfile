FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS sdk

WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine AS runtime
WORKDIR /app
COPY --from=sdk /app/out .

ENTRYPOINT [ "dotnet", "Operator.dll" ]