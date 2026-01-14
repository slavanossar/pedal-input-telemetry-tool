# Pedal Input Telemetry Tool

A Windows utility for displaying real-time sim racing pedal input telemetry, similar to overlays like Racelab.

## Features

- **Three Vertical Input Bars**: Real-time display of clutch, brake, and throttle inputs
- **Input Trace Graph**: Visual trace showing pedal inputs over time
- **Configurable Settings**:
  - HID device selection for each pedal
  - Adjustable trace duration (1-60 seconds)
  - Customizable colors for each pedal

## Requirements

- Windows 10/11
- .NET 8.0 SDK or Runtime
- Game controller/pedal set connected via USB

## Releases

Pre-built releases are available on the [Releases](https://github.com/YOUR_USERNAME/pedal-input-telemetry-tool/releases) page. Download the zip file for your architecture (x64 or x86), extract it, and run `PedalTelemetry.exe`. No .NET installation required!

## Building

1. Ensure you have the .NET 8.0 SDK installed
2. Restore dependencies and build:
   ```bash
   dotnet restore
   dotnet build -c Release
   ```
3. The executable will be in `bin/Release/net8.0-windows/PedalTelemetry.exe`

## Publishing a Standalone Executable

To create a single executable file that doesn't require .NET to be installed:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable will be in `bin/Release/net8.0-windows/win-x64/publish/PedalTelemetry.exe`

## GitHub Actions

This project includes GitHub Actions workflows for automated building and releasing:

- **Build workflow** (`build.yml`): Runs on pushes and pull requests to build and test the project
- **Release workflow** (`build-and-release.yml`): Creates releases when you:
  - Push a version tag (e.g., `v1.0.0`): `git tag v1.0.0 && git push origin v1.0.0`
  - Manually trigger from the Actions tab with a version number

The release workflow automatically:
- Builds Windows x64 and x86 versions
- Creates zip archives
- Creates a GitHub release with download links

## Usage

1. Connect your sim racing pedals/controller to your computer
2. Run `PedalTelemetry.exe`
3. Click "Settings" to configure:
   - Select HID devices for each pedal (clutch, brake, throttle)
   - Adjust trace duration (1-60 seconds)
   - Customize colors for each pedal
4. The telemetry display will show real-time input values

## Configuration

Settings are saved to `%USERPROFILE%\.pedal_telemetry_config.json` in your home directory. You can edit this file directly or use the Settings dialog in the application.

## Troubleshooting

- **No devices detected**: Make sure your pedals/controller are connected and recognized by Windows. Try clicking "Refresh Devices" in Settings.
- **Wrong axis values**: Some pedals use different axes. The tool tries common axes (0=X, 1=Y, 2=Z) but you may need to adjust the code for your specific hardware.
- **Values not updating**: Ensure the correct HID device is selected for each pedal in Settings.
- **Build errors**: Make sure you have .NET 8.0 SDK installed. Check with `dotnet --version`.

## License

This project is provided as-is for personal use.
