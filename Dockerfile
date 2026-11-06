# .NET SDK を使用してアプリケーションをビルドします。
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# プロジェクトファイルを先にコピーし、依存関係を復元します。
COPY src/mpcman_bot_v3/mpcman_bot_v3.csproj mpcman_bot_v3/
RUN dotnet restore mpcman_bot_v3/mpcman_bot_v3.csproj

# ソースコードをコピーして、Release 構成で発行します。
COPY src/ .
RUN dotnet publish mpcman_bot_v3/mpcman_bot_v3.csproj -c Release -o /app/publish --no-restore

# 実行用の軽量な .NET ランタイムイメージを使用します。
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
# ビルドステージで発行したアプリケーションをコピーします。
COPY --from=build /app/publish .
# Bot アプリケーションを起動します。
USER $APP_UID
ENTRYPOINT ["dotnet", "mpcman_bot_v3.dll"]
