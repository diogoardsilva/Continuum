@echo off
rem Starts the bundled UxPlay AirPlay receiver as "Continuum".
rem On the iPhone: Control Centre > Screen Mirroring > Continuum.
setlocal
set "DIR=%~dp0"
set "GST_PLUGIN_SYSTEM_PATH_1_0=%DIR%lib\gstreamer-1.0"
set "GST_PLUGIN_SCANNER=%DIR%libexec\gstreamer-1.0\gst-plugin-scanner.exe"
set "GST_REGISTRY_1_0=%DIR%gst-registry.bin"
set "PATH=%DIR%bin;%PATH%"
"%DIR%bin\uxplay.exe" -n Continuum -nh -vs d3d11videosink %*
