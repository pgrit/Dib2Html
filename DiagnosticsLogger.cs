using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Events;

namespace Dib2Html;

class DiagnosticsLogger
{
    public List<Diagnostic> Diagnostics { get; } = new();

    public DiagnosticsLogger(Kernel kernel)
    {
        kernel.KernelEvents.Subscribe(x =>
        {
            if (x is DiagnosticsProduced diag)
                Diagnostics.AddRange(diag.Diagnostics);
        });
    }
}