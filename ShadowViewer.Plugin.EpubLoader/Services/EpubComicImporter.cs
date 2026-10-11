using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Dispatching;
using ShadowPluginLoader.Attributes;
using ShadowPluginLoader.WinUI.Config;
using ShadowViewer.Plugin.EpubLoader.Helpers;
using ShadowViewer.Plugin.EpubLoader.Models;
using ShadowViewer.Plugin.Local.Configs;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Services;
using ShadowViewer.Sdk.Database;

namespace ShadowViewer.Plugin.EpubLoader.Services;

/// <summary>Imports EPUB images into the local reader's folder storage.</summary>
[CheckAutowired]
public partial class EpubComicImporter : FolderComicImporter
{
    [Autowired] private BaseSdkConfig BaseSdkConfig { get; }
    [Autowired] private LocalPluginConfig LocalPluginConfig { get; }

    public override string Name => "EpubToFolder";
    public override string Description => "导入 EPUB 图片漫画";
    public override string[] SupportTypes => [".epub"];
    public override bool Check(IStorageItem item) => item is StorageFile file &&
        string.Equals(file.FileType, ".epub", StringComparison.OrdinalIgnoreCase);

    public override async Task<ComicImportPreview> Preview(IStorageItem item)
    {
        if (!Check(item)) throw new ArgumentException("请选择 EPUB 文件。", nameof(item));
        var folder = Path.Combine(BaseSdkConfig.TempFolderPath, "Epub", Guid.NewGuid().ToString("N"));
        try
        {
            var info = await Task.Run(() => EpubParser.Parse(item.Path, folder));
            return CreatePreview(item, folder, info);
        }
        catch
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            throw;
        }
    }

    private static ComicImportPreview CreatePreview(IStorageItem item, string folder, EpubInfo info)
    {
        var chapter = new ComicChapter
        {
            Id = DatabaseIds.Next(), Name = info.Title, Order = 1,
            PageCount = info.ImagePaths.Count, CreatedDateTime = DateTime.Now
        };
        var pictures = info.ImagePaths.Select(path => new ComicPicture
        {
            Id = DatabaseIds.Next(), Name = Path.GetFileName(path), ChapterId = chapter.Id,
            StoragePath = path, Size = new FileInfo(path).Length, CreatedDateTime = DateTime.Now
        }).ToList();
        chapter.Size = pictures.Sum(x => x.Size);
        return new ComicImportPreview
        {
            Name = info.Title, SourceItem = item, Thumb = info.CoverImagePath ?? info.ImagePaths[0],
            ComicDetail = new ComicDetail
            {
                ProcessMode = "Folder", StoragePath = folder, ChapterCount = 1, PageCount = pictures.Count,
                Authors = info.Authors.Select(name => new LocalAuthor { Name = name }).ToList()
            },
            PreviewChapters = [chapter], PreviewImages = new() { [chapter] = pictures }
        };
    }

    public override async Task ImportComic(ComicImportPreview preview, long parentId, DispatcherQueue dispatcher,
        CancellationToken token, IProgress<double>? progress = null)
    {
        if (preview.SourceItem is not StorageFile file || !Check(file))
            throw new ArgumentException("EPUB 预览缺少源文件。", nameof(preview));
        token.ThrowIfCancellationRequested();
        var folder = Path.Combine(LocalPluginConfig.ComicFolderPath, Guid.NewGuid().ToString("N"));
        try
        {
            var info = await Task.Run(() => EpubParser.Parse(file.Path, folder, token), token);
            var imported = CreatePreview(file, folder, info);
            imported.Name = preview.Name;
            using var db = DbFactory.CreateDbContext();
            using var transaction = await db.Database.BeginTransactionAsync(token);
            var authors = new List<LocalAuthor>();
            foreach (var author in imported.ComicDetail.Authors!)
            {
                var existing = await db.Set<LocalAuthor>().FirstOrDefaultAsync(x => x.Name == author.Name, token);
                authors.Add(existing ?? author);
            }
            imported.ComicDetail.Authors = authors;
            var comic = new ComicNode
            {
                Name = imported.Name, Thumb = imported.Thumb, ParentId = parentId, NodeType = "Comic",
                Size = imported.PreviewChapters.Sum(x => x.Size), ComicDetail = imported.ComicDetail,
                ReadingRecord = new LocalReadingRecord(), SourcePluginDataId = PluginId + Version
            };
            db.Add(comic);
            foreach (var chapter in imported.PreviewChapters)
            {
                chapter.ComicId = comic.Id;
                db.Add(chapter);
                foreach (var picture in imported.PreviewImages[chapter])
                {
                    picture.ComicId = comic.Id;
                    db.Add(picture);
                }
            }
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        catch
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            throw;
        }
        progress?.Report(100);
    }
}
