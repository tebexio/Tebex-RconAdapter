# RCON Adapter by Tebex

## Overview
The RCON Adapter is a C# console application designed to serve as an intermediary between the Tebex API and game servers lacking adequate plugin support. 

This utility performs various operations to facilitate Tebex interactions with such game servers, enabling Tebex support on games that would otherwise be difficult or impossible.

## Requirements

- Windows OS or a system with Mono support
- .NET runtime

## Features

- Perform real-time operations such as player banning, granting permissions, etc.
- Seamless integration with game servers lacking plugin/API support
- Single-package deployment: includes all necessary DLLs and the core C# library
- Cross-platform: works on both Windows and Mono systems

## Installation

1. **Download the Latest Release**: Grab the latest version from the releases page.
2. **Extract the Archive**: Unzip the archive to a folder of your choice.
3. **Run the Executable**: Navigate to the folder and run `Tebex-RCON.exe`.

To run in **Linux**, ensure the app is executable with `chmod +x .\Tebex-RCON` and launch with `.\Tebex-RCON`.

The app must remain running in the background as it checks periodically with Tebex to execute needed game server commands.

## Configuration

A configuration file `tebex-config.json` will be created on first startup. Below is a sample configuration. The app will begin a guided wizard to generate the appropriate config for you.

```json
{
  "SecretKey": "YOUR_TEBEX_SECRET_KEY",
  "RconIp": "127.0.0.1",
  "RconPort": 25565,
  "RconPassword": "YOUR_RCON_PASSWORD"
}
```


## Contributions
We welcome contributions from the community. Please refer to the `CONTRIBUTING.md` file for more details. By submitting code to us, you agree to the terms set out in the `CONTRIBUTING.md` file

## Support
This repository is only used for developers to submit bug reports and pull requests. If you have found a bug, please open an issue here.

If you are a user requiring support for Tebex, please contact us at https://www.tebex.io/contact