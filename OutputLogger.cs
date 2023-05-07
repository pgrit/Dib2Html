using System.Text.RegularExpressions;
using Markdig;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Events;

namespace Dib2Html;

class OutputLogger
{
    public List<string> HtmlOutputs { get; } = new();
    bool logInstallMessages;

    public OutputLogger(Kernel kernel, bool logInstallMessages)
    {
        this.logInstallMessages = logInstallMessages;
        kernel.KernelEvents.Subscribe(x =>
        {
            if (x is DisplayEvent evt)
                LogOutputs(evt);
        });
    }

    static string MarkdownToHtml(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        return Markdown.ToHtml(markdown, pipeline);
    }

    static string PlainTextToHtml(string markdown)
    {
        var pattern = "\u001b\\[([^m]*)m";
        markdown = Regex.Replace(markdown, pattern, "");
        return "<p>" + markdown.ReplaceLineEndings("<br/>") + "</p>";
    }

    void LogScriptOutput(ScriptContent script)
    {
        HtmlOutputs.Add("<script>" + script.ScriptValue + "</script>");
    }

    void LogInstallMessageOutput(DisplayEvent evt, InstallPackagesMessage installMsg)
    {
        if (installMsg.InstalledPackages.Count == 0)
            return; // Don't output installation progress
        if (!logInstallMessages)
            return;
        var htmlOutputs = evt.FormattedValues
            .Where(v => v.MimeType == "text/html")
            .Select(v => v.Value);
        string output = htmlOutputs.Any() ? htmlOutputs.Aggregate((e, s) => e + s) : "";
        HtmlOutputs.Add(output);
    }

    void LogGeneralOutput(DisplayEvent evt)
    {
        string output = "";

        var htmlOutputs = evt.FormattedValues
            .Where(v => v.MimeType == "text/html")
            .Select(v => v.Value);
        output += htmlOutputs.Any() ? htmlOutputs.Aggregate((e, s) => e + s) : "";

        var mdOutputs = evt.FormattedValues
            .Where(v => v.MimeType == "text/markdown")
            .Select(v => MarkdownToHtml(v.Value));
        output += mdOutputs.Any() ? mdOutputs.Aggregate((e, s) => e + s) : "";

        var txtOutputs = evt.FormattedValues
            .Where(v => v.MimeType == "text/plain")
            .Select(v => PlainTextToHtml(v.Value));
        output += txtOutputs.Any() ? txtOutputs.Aggregate((e, s) => e + s) : "";

        HtmlOutputs.Add(output);
    }

    void LogOutputs(DisplayEvent evt)
    {
        if (evt.Value is ScriptContent script)
        {
            LogScriptOutput(script);
        }
        else if (evt.Value is InstallPackagesMessage installMsg)
        {
            LogInstallMessageOutput(evt, installMsg);
        }
        else
        {
            LogGeneralOutput(evt);
        }
    }
}