@if exist dist rmdir /s /q dist
dotnet publish -p:PublishSingleFile=true /p:DebugType=None -r win-x64 -c Release --self-contained false -o dist
