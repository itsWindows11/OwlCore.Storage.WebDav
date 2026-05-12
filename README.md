# OwlCore.Storage.WebDav

[![NuGet](https://img.shields.io/nuget/v/OwlCore.Storage.WebDav.svg?label=NuGet)](https://www.nuget.org/packages/OwlCore.Storage.WebDav)
[![NuGet Downloads](https://img.shields.io/nuget/dt/OwlCore.Storage.WebDav.svg)](https://www.nuget.org/packages/OwlCore.Storage.WebDav)
[![CI](https://github.com/itsWindows11/OwlCore.Storage.WebDav/actions/workflows/ci.yml/badge.svg)](https://github.com/itsWindows11/OwlCore.Storage.WebDav/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

An [OwlCore.Storage](https://github.com/Arlodotexe/OwlCore.Storage) implementation for WebDAV, built on top of [WebDavClient](https://github.com/skazantsev/WebDavClient).

## Features

- `IModifiableFolder` implementation over WebDAV resources.
- `IChildFile` implementation with read/write/read-write stream support.
- Fast-path copy and move operations when both source and destination use the same WebDAV client.
- Fallback interoperability copy/move path for non-WebDAV-backed sources.
- Static `GetFromWebDavPathAsync` and `TryGetFromWebDavPathAsync` helpers.
- Multi-targeting: `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`.

## Installation

```bash
dotnet add package OwlCore.Storage.WebDav
```

## Usage

```csharp
using System.Net;
using OwlCore.Storage.WebDav;
using WebDav;

var client = new WebDavClient(new WebDavClientParams
{
    BaseAddress = new Uri("https://webdav.example.com/"),
    Credentials = new NetworkCredential("user", "password"),
});

var root = await WebDavFolder.GetFromWebDavPathAsync(client, "/");
var file = await root.CreateFileAsync("hello.txt", overwrite: true);

await using var write = await file.OpenStreamAsync(FileAccess.Write);
await using var writer = new StreamWriter(write);
await writer.WriteAsync("Hello WebDAV!");
```

## Running tests

```bash
dotnet test OwlCore.Storage.WebDav.slnx
```

## License

Licensed under the MIT License. See [LICENSE](LICENSE).
