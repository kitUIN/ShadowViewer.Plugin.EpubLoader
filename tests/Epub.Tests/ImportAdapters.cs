using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Dispatching;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Plugin.Local.Models;
using Windows.Storage;

// Adapt WinRT and generated injection only; the importer, entities, EF mappings and SQLite are production code.
namespace Windows.Storage
{
    public interface IStorageItem { string Path { get; } }
    public sealed class StorageFile(string path) : IStorageItem
    {
        public string Path => path;
        public string FileType => System.IO.Path.GetExtension(path);
    }
}
namespace Microsoft.UI.Dispatching { public class DispatcherQueue { } }
namespace ShadowPluginLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CheckAutowiredAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class AutowiredAttribute : Attribute { }
}
namespace ShadowPluginLoader.WinUI.Config
{
    public class BaseSdkConfig { public string TempFolderPath { get; set; } = ""; }
}
namespace ShadowViewer.Plugin.Local.Configs
{
    public class LocalPluginConfig { public string ComicFolderPath { get; set; } = ""; }
}
namespace ShadowViewer.Plugin.Local.Services
{
    public abstract class FolderComicImporter(IDbContextFactory<LocalDbContext> factory, string pluginId, string version)
    {
        protected IDbContextFactory<LocalDbContext> DbFactory => factory;
        public string PluginId => pluginId;
        public string Version => version;
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract string[] SupportTypes { get; }
        public abstract bool Check(IStorageItem item);
        public abstract Task<ComicImportPreview> Preview(IStorageItem item);
        public abstract Task ImportComic(ComicImportPreview preview, long parentId, DispatcherQueue dispatcher,
            CancellationToken token, IProgress<double>? progress = null);
    }
}
namespace ShadowViewer.Plugin.EpubLoader.Services
{
    public partial class EpubComicImporter
    {
        public EpubComicImporter(IDbContextFactory<LocalDbContext> factory, string root,
            string pluginId = "ShadowViewer.Plugin.EpubLoader") : base(factory, pluginId, "0.2.0")
        {
            BaseSdkConfig = new() { TempFolderPath = System.IO.Path.Combine(root, "previews") };
            LocalPluginConfig = new() { ComicFolderPath = System.IO.Path.Combine(root, "comics") };
        }
    }
}
