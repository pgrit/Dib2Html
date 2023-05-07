using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.CSharp;
using Microsoft.DotNet.Interactive.FSharp;
using Microsoft.DotNet.Interactive.Mermaid;
using Microsoft.DotNet.Interactive.PowerShell;
using System.Reactive.Linq;

namespace Dib2Html;

public static class DibRunner
{
    static CompositeKernel MakeKernel()
    => new CompositeKernel
        {
            new CSharpKernel()
                .UseNugetDirective()
                .UseKernelHelpers()
                .UseWho()
                .UseValueSharing()
                .UseImportMagicCommand(),

            new FSharpKernel()
                .UseNugetDirective()
                .UseKernelHelpers()
                .UseWho()
                .UseValueSharing()
                .UseImportMagicCommand(),

            new HtmlKernel(),

            new JavaScriptKernel()
                .UseWho()
                .UseValueSharing()
                .UseImportMagicCommand(),

            new MermaidKernel()
                .UseWho(),

            new PowerShellKernel()
                .UseImportMagicCommand()
                .UseWho()
                .UseProfiles(),
        }
        .UseLogMagicCommand()
        .UseImportMagicCommand()
        .UseNuGetExtensions();

    public static void RunAndConvert(FileInfo filename, FileInfo? outfile, bool logInstall)
    {
        var kernel = MakeKernel();
        var diagnostics = new DiagnosticsLogger(kernel);
        var outputs = new OutputLogger(kernel, logInstall);

        kernel.LoadAndRunInteractiveDocument(filename).Wait();

        string name = System.IO.Path.GetFileNameWithoutExtension(filename.Name);

        string htmlResult =
        $"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8" />
            <title>{name}</title>
            <script src="https://polyfill.io/v3/polyfill.min.js?features=es6"></script>
            <script id="MathJax-script" async src="https://cdn.jsdelivr.net/npm/mathjax@3/es5/tex-mml-chtml.js"></script>
        </head>
        <body>
        <div style="max-width: 1000px; margin: auto;">
            {outputs.HtmlOutputs.Aggregate((e, s) => e + s)}
        <div>
        </body>
        </html>
        """;

        File.WriteAllText(outfile?.FullName ?? $"{name}.html", htmlResult);

        if (diagnostics.Diagnostics.Any())
        {
            foreach (var diag in diagnostics.Diagnostics)
                Console.WriteLine(diag);
        }
    }
}