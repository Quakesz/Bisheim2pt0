# Bisheim2pt0 installer

Built from the user-approved v1.0 Alpha Windows ZIP (SHA256 `97d0995a2d1a946219e24231ed0ffa4106e2ed248250f4a7544f1821eb5a9ee0`). The application, bundled source, GPL license, and runtime notices are included unchanged.

Run `Bisheim2pt0-Setup-Windows-x64.exe`, accept the GPL license, and choose Install. Setup installs under `%LOCALAPPDATA%\Programs\Bisheim2pt0` and adds Bisheim2pt0 shortcuts to the current user's desktop and Start menu. Open the desktop shortcut, then use the launcher's Play button. Steam and Valheim are required; the .NET runtime is included.

Uninstall from Windows Installed apps. Profiles remain in their existing application-data location. Re-running the installer updates the same installation. Close the launcher first.

## Rebuild

Use Inno Setup 6.7.3 or a compatible version. Extract the approved ZIP, then run PowerShell:

```powershell
& 'C:\path\to\ISCC.exe' '/DPayloadDir=C:\path\to\Bisheim2pt0-Windows-x64' .\Launcher.iss
```

The setup executable is written one directory above this script. Keep build outputs outside the payload directory. The installer is not code-signed.
