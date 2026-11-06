DarkColony.Media corresponding source and replacement

This directory contains the complete modified library's C# source, project and
shared compiler settings used by this export. See ../../Notices/MEDIA-NOTICE.txt
and ../../Notices/COPYING.LGPLv2.1. Cinepak.cs identifies the original source and
changes. No original game assets or external converter binaries are present.

Build-time only: .NET 8 SDK or a compatible newer SDK.
  dotnet build DarkColony.Media.csproj -c Release
Replace ../../data_DarkColony.Viewer_windows_x86_64/DarkColony.Media.dll with
bin/Release/net8.0/DarkColony.Media.dll while OpenDC is closed. Keep the public
API compatible. No signing or tamper checks prevent replacing this library.
Players running the unchanged engine do not need the SDK or source build tools.
