using DryIoc;
using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Plugin.Local.Entities;
using ShadowPluginLoader.Attributes;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Sdk.Plugins;
using ShadowViewer.Plugin.EpubLoader.I18n;
using ShadowViewer.Plugin.EpubLoader.Services;
using ShadowViewer.Plugin.Local.Services.Interfaces;


namespace ShadowViewer.Plugin.EpubLoader;

[MainPlugin]
[CheckAutowired]
public partial class EpubLoaderPlugin : AShadowViewerPlugin
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    partial void ConstructorInit()
    {
        DiFactory.Services.Register<IComicImporter, EpubComicImporter>(Reuse.Singleton,
            made: Parameters.Of
                .Name("pluginId", _ => MetaData.Id)
                .Name("version", _ => MetaData.Version.ToString()));
        using var db = DiFactory.Services.Resolve<IDbContextFactory<LocalDbContext>>().CreateDbContext();
        var source = new SourcePluginData(MetaData.Id, MetaData.Version.ToString(), MetaData.Name,
            "#ffd657", "#000000");
        var existing = db.Set<SourcePluginData>().Find(source.Id);
        if (existing == null) db.Add(source);
        else db.Entry(existing).CurrentValues.SetValues(source);
        db.SaveChanges();
    }
    /// <inheritdoc/>
    public override string DisplayName => I18N.DisplayName;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    protected override void Enabled()
    {
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override void Loaded()
    {
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    protected override void Disabled()
    {
    }
}