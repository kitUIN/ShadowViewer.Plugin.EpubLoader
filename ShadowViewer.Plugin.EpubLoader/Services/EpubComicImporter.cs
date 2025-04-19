using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Dispatching;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Core.Extensions;
using ShadowViewer.Plugin.EpubLoader.Helpers;
using ShadowViewer.Plugin.Local;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Services;
using SqlSugar;

namespace ShadowViewer.Plugin.EpubLoader.Services;

/// <summary>
/// Epub导入类
/// </summary>
[CheckAutowired]
public partial class EpubComicImporter : FolderComicImporter
{
    /// <inheritdoc />
    public override string[] SupportTypes => [".epub"];

    /// <inheritdoc />
    public override bool Check(IStorageItem item)
    {
        return item is StorageFile file && SupportTypes.ContainsIgnoreCase(file.FileType);
    }

    /// <inheritdoc />
    public override async Task ImportComic(IStorageItem item, long parentId, DispatcherQueue dispatcher,
        CancellationToken token)
    {
        var comicId = SnowFlakeSingle.Instance.NextId();
        var path = Path.Combine(Core.Settings.CoreSettings.Instance.ComicsPath, comicId.ToString());
        var info = EpubParser.Parse(item.Path, path);
        var authors = new List<LocalAuthor>();
        foreach (var author in info.Authors)
        {
            var dbAuthor = await Db.Queryable<LocalAuthor>().FirstAsync(x => x.Name == author, token);
            if (dbAuthor == null)
            {
                authors.Add(await Db
                    .Insertable(new LocalAuthor { Id = SnowFlakeSingle.Instance.NextId(), Name = author })
                    .ExecuteReturnEntityAsync());
            }
            else
            {
                authors.Add(dbAuthor);
            }
        }

        await Db.InsertNav(new LocalComic()
            {
                Name = info.Title,
                Thumb = info.CoverImagePath ?? "mx-appx:///default.png",
                Affiliation = LocalPlugin.Meta.Id,
                ParentId = parentId,
                IsFolder = false,
                Link = path,
                Authors = authors,
                Id = comicId,
                ReadingRecord = new LocalReadingRecord()
                {
                    CreatedDateTime = DateTime.Now,
                    UpdatedDateTime = DateTime.Now,
                },
            })
            .Include(z1 => z1.ReadingRecord)
            .Include(z2 => z2.Authors)
            .ExecuteCommandAsync();
    }
}