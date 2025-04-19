using ShadowPluginLoader.Attributes;
using ShadowViewer.Core.Plugins;
using ShadowViewer.Plugin.EpubLoader.I18n;


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