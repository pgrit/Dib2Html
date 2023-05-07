// The code in this file was copied from
// https://www.nuget.org/packages/Microsoft.dotnet-interactive
//
// Ideally, this should not be necessary, but currently, adding "Microsoft.dotnet-interactive" fails with
// error: NU1202:
// Package Microsoft.dotnet-interactive 1.0.420501 is not compatible with net7.0 (.NETCoreApp,Version=v7.0).
// Package Microsoft.dotnet-interactive 1.0.420501 supports: net7.0 (.NETCoreApp,Version=v7.0) / any
//
// which is either a ridiculously broken error message or a nuget bug.
//
// TODO FIXME

using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Events;
#nullable disable

public static class KernelExtensionLoader
{
    public static CompositeKernel UseNuGetExtensions(this CompositeKernel kernel)
    {
        var packagesToCheckForExtensions = new ConcurrentQueue<PackageAdded>();

        kernel.AddMiddleware(async (command, context, next) =>
        {
            await next(command, context);

            while (packagesToCheckForExtensions.TryDequeue(out var packageAdded))
            {
                var packageRootDir = packageAdded.PackageReference.PackageRoot;

                var extensionDir =
                    new DirectoryInfo
                    (Path.Combine(
                         packageRootDir,
                         "interactive-extensions",
                         "dotnet"));

                if (extensionDir.Exists)
                {
                    await LoadExtensionsFromDirectoryAsync(kernel, extensionDir, context);
                }
            }
        });

        kernel.RegisterForDisposal(
            kernel.KernelEvents
                  .OfType<PackageAdded>()
                  .Where(pa => pa?.PackageReference.PackageRoot is not null)
                  .Distinct(pa => pa.PackageReference.PackageRoot)
                  .Subscribe(added => packagesToCheckForExtensions.Enqueue(added)));

        return kernel;
    }

    public static async Task LoadExtensionsFromDirectoryAsync(this CompositeKernel kernel, DirectoryInfo extensionDir, KernelInvocationContext context)
    {
        await new PackageDirectoryExtensionLoader().LoadFromDirectoryAsync(
            extensionDir,
            kernel,
            context);
    }

    internal static bool CanBeInstantiated(this Type type)
    {
        return !type.IsAbstract
               && !type.IsGenericTypeDefinition
               && !type.IsInterface;
    }
}

internal class PackageDirectoryExtensionLoader
{
    private const string ExtensionScriptName = "extension.dib";

    private readonly HashSet<AssemblyName> _loadedAssemblies = new();
    private readonly object _lock = new();

    public async Task LoadFromDirectoryAsync(
        DirectoryInfo directory,
        Kernel kernel,
        KernelInvocationContext context)
    {
        if (directory is null)
        {
            throw new ArgumentNullException(nameof(directory));
        }

        if (kernel is null)
        {
            throw new ArgumentNullException(nameof(kernel));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!directory.Exists)
        {
            throw new ArgumentException($"Directory {directory.FullName} doesn't exist", nameof(directory));
        }

        await LoadFromDllsInDirectory(
            directory,
            kernel,
            context);

        await LoadFromExtensionDibScript(
            directory,
            kernel,
            context);
    }

    public async Task LoadFromDllsInDirectory(
        DirectoryInfo directory,
        Kernel kernel,
        KernelInvocationContext context)
    {
        var extensionDlls = directory.GetFiles("*.dll", SearchOption.TopDirectoryOnly);

        foreach (var extensionDll in extensionDlls)
        {
            await LoadFromAssemblyFile(
                extensionDll,
                kernel,
                context);
        }
    }

    private async Task LoadFromAssemblyFile(
        FileInfo assemblyFile,
        Kernel kernel,
        KernelInvocationContext context)
    {
        bool loadExtensions;

        lock (_lock)
        {
            loadExtensions = _loadedAssemblies.Add(AssemblyName.GetAssemblyName(assemblyFile.FullName));
        }

        if (loadExtensions)
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyFile.FullName);

            var extensionTypes = assembly
                                 .ExportedTypes
                                 .Where(t => t.CanBeInstantiated() && typeof(IKernelExtension).IsAssignableFrom(t))
                                 .ToArray();

            foreach (var extensionType in extensionTypes)
            {
                var extension = (IKernelExtension)Activator.CreateInstance(extensionType);

                try
                {
                    await extension.OnLoadAsync(kernel);
                    context.Publish(new KernelExtensionLoaded(extension, context.Command));
                }
                catch (Exception e)
                {
                    context.Publish(new ErrorProduced(
                                        $"Failed to load kernel extension \"{extensionType.Name}\" from assembly {assembly.Location}",
                                        context.Command));

                    context.Fail(context.Command, new KernelExtensionLoadException(e));
                }
            }
        }
    }

    private async Task LoadFromExtensionDibScript(
        DirectoryInfo directory,
        Kernel kernel,
        KernelInvocationContext context)
    {
        var extensionFile = new FileInfo(Path.Combine(directory.FullName, ExtensionScriptName));

        if (extensionFile.Exists)
        {
            var logMessage = $"Loading extension script from `{extensionFile.FullName}`";
            await kernel.LoadAndRunInteractiveDocument(extensionFile);
        }
    }
}