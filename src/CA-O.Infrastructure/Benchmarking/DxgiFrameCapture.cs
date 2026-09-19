namespace CAO.Infrastructure.Benchmarking;

/// <summary>Captura frame-times del escritorio vía DXGI OutputDuplication (10 s por defecto).
/// Sin dependencias externas: P/Invoke propio mínimo. Cualquier fallo → null (UI: "no disponible").</summary>
public sealed class DxgiFrameCapture : IFrameCapture
{
    public async Task<FrameCaptureResult?> CaptureAsync(TimeSpan duration, CancellationToken ct)
    {
        try
        {
            return await Task.Run(() => CaptureCore(duration, ct), ct);
        }
        catch { return null; }
    }

    private static FrameCaptureResult? CaptureCore(TimeSpan duration, CancellationToken ct)
    {
        // RULING Step 0 (Task 4, Plan 02): DXGI manual NO viable sin CsWin32 → null.
        //
        // Research: Context7 no cubre DXGI (query a /microsoft/directx-headers sin
        // resultados para DuplicateOutput/AcquireNextFrame); firmas confirmadas en
        // MS Learn en su lugar:
        // - IDXGIOutputDuplication::AcquireNextFrame(UINT TimeoutInMilliseconds,
        //   DXGI_OUTDUPL_FRAME_INFO*, IDXGIResource**) → S_OK /
        //   DXGI_ERROR_ACCESS_LOST / DXGI_ERROR_WAIT_TIMEOUT /
        //   DXGI_ERROR_INVALID_CALL (https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/nf-dxgi1_2-idxgioutputduplication-acquirenextframe)
        // - Cadena completa: D3D11CreateDevice → QI IDXGIDevice → GetParent
        //   IDXGIAdapter → EnumOutputs → QI IDXGIOutput1 → DuplicateOutput, con
        //   ReleaseFrame obligatorio por frame (muestras SharpDX/Silk.NET).
        //
        // El P/Invoke manual de esa cadena (vtables COM de IDXGIDevice, IDXGIAdapter,
        // IDXGIOutput, IDXGIOutput1, IDXGIOutputDuplication, IDXGIResource + marshaling
        // de DXGI_OUTDUPL_FRAME_INFO) es demasiado frágil sin CsWin32/SharpDX/Silk.NET,
        // y el brief prohíbe dependencias nuevas. Se degrada a null siempre; la UI
        // muestra "fluidez no disponible en este equipo". Puerta futura: PresentMon
        // (ETW) o CsWin32 detrás de esta misma interfaz IFrameCapture.
        //
        // Nota DPC: una implementación real combinaría los deltas de AcquireNextFrame
        // con DpcLatencySampler.SampleAsync (API pública en SystemInterop, ya usada
        // en este repo: PerformanceCounter "Processor" "% DPC Time"/"% Interrupt Time").
        _ = duration;
        _ = ct;
        return null;
    }
}
