# Giai đoạn 1: Build (Dùng SDK để biên dịch code)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy file csproj và restore các thư viện
COPY *.csproj ./
RUN dotnet restore

# Copy toàn bộ code còn lại
COPY . ./
# Build ra bản Release
RUN dotnet publish -c Release -o out

# Giai đoạn 2: Runtime (Chỉ dùng bộ thư viện nhẹ để chạy)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Mở cổng 8080 (Render yêu cầu cổng này hoặc biến môi trường PORT)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Chạy ứng dụng
ENTRYPOINT ["dotnet", "ChillingAddrManagement.dll"]