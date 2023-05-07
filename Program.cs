using System.CommandLine;
using Dib2Html;

var inFileArg = new Argument<FileInfo>(
    name: "dibfile",
    description: "The .dib file to run and convert."
);

var outFileOption = new Option<FileInfo?>(
    name: "--out",
    description: "Name of the output .html file. Default: <dibfile>.html"
);

var logInstallOption = new Option<bool>(
    name: "--log-install",
    description: "If true, the list of installed nuget packages is included in the output."
);

var rootCommand = new RootCommand(".dib to .html converter - Executes a Polyglot notebook and writes all outputs to .html");
rootCommand.AddArgument(inFileArg);
rootCommand.AddOption(outFileOption);
rootCommand.AddOption(logInstallOption);

rootCommand.SetHandler((file, outfile, logInstall) =>
{
    DibRunner.RunAndConvert(file, outfile, logInstall);
},
inFileArg, outFileOption, logInstallOption);

return await rootCommand.InvokeAsync(args);

