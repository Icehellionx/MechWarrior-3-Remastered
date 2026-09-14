# NLayer 1.16.0 build input

This directory pins the official `NLayer` 1.16.0 NuGet package used by the CD-audio helper.

- Source: <https://github.com/naudio/NLayer>
- Package: <https://www.nuget.org/packages/NLayer/1.16.0>
- Package SHA-256: `e0e112c3bfc3b4f49e87cc4855b0f3dc6ac28d257a58b6a4f22098f8d0d4a9b9`
- License: MIT (retained in `LICENSE.txt`)

The build verifies the package hash and extracts only `lib/netstandard2.0/NLayer.dll`.
It does not perform a network restore.
