# RCON Adapter by Tebex

## Overview
The RCON Adapter is a C# console application designed to serve as an intermediary between the Tebex API and game servers lacking adequate plugin support. 

This utility performs various operations to facilitate Tebex interactions with such game servers, enabling Tebex support on games that would otherwise be difficult or impossible.

## Requirements

- Windows OS or a system with Mono support
- .NET runtime

## Key Features

- Connect your Tebex webstore to your game server without the use of plugins.
- Process Tebex transactions and apply commands to your players using the RCON protocol.
- Deploy and manage at scale using included Dockerfile or build and run an executable.

## Installation

To run in **Linux**, ensure the app is executable with `chmod +x ./Tebex-RCON` and launch with `./Tebex-RCON`.

Example startup command:
```
./TebexRCON --game=7d2d --ip=127.0.0.1 --port=12345 --pass=password --telnet
```

The app must remain running in the background as it checks periodically with Tebex to execute needed game server commands.

Alternatively, you may use the provided **Dockerfile** to build and deploy the app from source. If using Docker, you must configure launch
arguments via command line or environment variables.

## Configuration and Launch

### Command Line Args

You can configure the RCON adapter directly from the command line with these arguments:

```
--key={storeKey}         Your webstore's secret key.

--game={gameName}        The game plugin you wish to run. See available plugins below.

--ip={serverIp}          The game server's IP address

--port={serverPort}      Port for RCON connections on the game server

--pass={password}        Password for your game server's RCON console

--telnet                 Uses telnet protocol instead of RCON

--debug                  Show debug logging while running
```

### Environment Variables

You can also configure the RCON adapter to use environment variables at launch. **Any environment variables that are
set below take precedence** over any value previously set in the config file or via command line.

Available environment variables:
- RCON_ADAPTER_KEY          
- RCON_ADAPTER_GAME         
- RCON_ADAPTER_HOST        
- RCON_ADAPTER_PORT         
- RCON_ADAPTER_PASSWORD

### Config File
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