using System.Collections.Generic;

namespace ShadowViewer.Plugin.EpubLoader.Models;

/// <summary>
/// 文件信息
/// </summary>
/// <param name="Title"></param>
/// <param name="Authors"></param>
/// <param name="CoverImagePath"></param>
/// <param name="ImagePaths"></param>
public record EpubInfo(
    string Title,
    List<string> Authors,
    string? CoverImagePath,
    List<string> ImagePaths
);