using DryIoc;
using ShadowPluginLoader.Attributes;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Core.Plugins;
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
        DiFactory.Services.Register<IComicImporter,EpubComicImporter>(Reuse.Singleton,
            made: Parameters.Of.Type(_ => Meta.Id));
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