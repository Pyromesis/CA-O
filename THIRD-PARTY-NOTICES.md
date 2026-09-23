# Avisos de software de terceros — CA-O

CA-O © 2026 CA-NEXT-PROJECT (licencia privada, ver `LICENSE`).
Este documento enumera los componentes de terceros que CA-O incorpora o
requiere, con su licencia. El uso de cada componente se rige por su propia
licencia además del EULA de CA-O.

## Componentes distribuidos o requeridos en tiempo de ejecución

| Componente | Versión usada | Licencia | Proyecto |
|---|---|---|---|
| .NET Runtime / SDK | 10 | MIT | https://github.com/dotnet/runtime |
| Windows App SDK | 2.4.0 | MIT | https://github.com/microsoft/WindowsAppSDK |
| CommunityToolkit.Mvvm | 8.4.0 | MIT | https://github.com/CommunityToolkit/dotnet |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Hosting (+ WindowsServices) | 10.0.0 | MIT | https://github.com/dotnet/extensions |
| System.Management | 10.0.0 | MIT | https://github.com/dotnet/runtime |
| System.Diagnostics.PerformanceCounter | 10.0.0 | MIT | https://github.com/dotnet/runtime |
| System.ServiceProcess.ServiceController | 8.0.1 | MIT | https://github.com/dotnet/runtime |
| Microsoft Windows (APIs del sistema) | — | Términos de Microsoft | https://www.microsoft.com/windows |

## Solo herramientas de compilación / pruebas (no se distribuyen con la app)

| Componente | Versión usada | Licencia | Proyecto |
|---|---|---|---|
| Inno Setup 6 (empaquetado del instalador) | 6.x | Licencia de Inno Setup (uso libre) | https://jrsoftware.org/isinfo.php |
| CycloneDX (`dotnet-cyclonedx`, SBOM) | 6.x | Apache-2.0 | https://github.com/CycloneDX/cyclonedx-dotnet |
| xUnit (+ runner de VS) | 2.9.2 / 2.8.2 | Apache-2.0 | https://github.com/xunit/xunit |
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT | https://github.com/microsoft/vstest |

## Texto de la licencia MIT (aplica a todos los componentes MIT anteriores)

```
Copyright (c) .NET Foundation and Contributors / Microsoft Corporation /
respective contributors. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Los componentes Apache-2.0 se rigen por la licencia Apache License 2.0
(https://www.apache.org/licenses/LICENSE-2.0). Inno Setup se rige por su
propia licencia (https://jrsoftware.org/files/is/license.txt).

> Nota: las versiones indicadas corresponden a la versión en desarrollo a la
> fecha de este documento. El SBOM CycloneDX (`bom.json`) publicado con cada
> release contiene el inventario exacto de dependencias de esa versión.
