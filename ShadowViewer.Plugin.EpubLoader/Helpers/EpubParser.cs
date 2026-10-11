using System;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using ShadowViewer.Plugin.EpubLoader.Exceptions;
using ShadowViewer.Plugin.EpubLoader.Models;

namespace ShadowViewer.Plugin.EpubLoader.Helpers;

/// <summary>Extracts image-based EPUB books in spine order.</summary>
public static class EpubParser
{
    private static string ResolvePath(string documentPath, string href)
    {
        var root = new Uri("https://epub.invalid/");
        var resolved = new Uri(new Uri(root, documentPath), href);
        if (resolved.Host != root.Host || resolved.Scheme != root.Scheme)
            throw new EpubImportException("EPUB 包含外部资源路径。");
        return Uri.UnescapeDataString(resolved.AbsolutePath.TrimStart('/'));
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new EpubImportException($"未找到 EPUB 文件：{path}");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    public static EpubInfo Parse(string zipPath, string extractFolder, CancellationToken token = default)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            using var archive = ZipFile.OpenRead(zipPath);
            XNamespace containerNs = "urn:oasis:names:tc:opendocument:xmlns:container";
            XNamespace opfNs = "http://www.idpf.org/2007/opf";
            XNamespace dcNs = "http://purl.org/dc/elements/1.1/";
            var container = ReadXml(archive, "META-INF/container.xml");
            var opfPath = container.Descendants(containerNs + "rootfile")
                .Select(x => (string?)x.Attribute("full-path")).FirstOrDefault()
                ?? throw new EpubImportException("container.xml 中未找到 OPF 路径。");
            var opf = ReadXml(archive, opfPath);
            var metadata = opf.Descendants(opfNs + "metadata").FirstOrDefault();
            var title = metadata?.Element(dcNs + "title")?.Value ?? Path.GetFileNameWithoutExtension(zipPath);
            var authors = metadata?.Elements(dcNs + "creator").Select(x => x.Value.Trim())
                .Where(x => x.Length > 0).Distinct().ToList() ?? [];
            var items = opf.Descendants(opfNs + "manifest").Elements(opfNs + "item")
                .Where(x => x.Attribute("id") != null && x.Attribute("href") != null)
                .ToDictionary(x => (string)x.Attribute("id")!);
            var imagePaths = items.Values.Where(x => ((string?)x.Attribute("media-type")) is
                    "image/jpeg" or "image/png" or "image/gif" or "image/webp" or "image/bmp")
                .Select(x => ResolvePath(opfPath, (string)x.Attribute("href")!)).ToHashSet();
            var coverId = metadata?.Elements(opfNs + "meta")
                .FirstOrDefault(x => (string?)x.Attribute("name") == "cover")?.Attribute("content")?.Value;
            var coverItem = items.Values.FirstOrDefault(x =>
                ((string?)x.Attribute("properties"))?.Split(' ').Contains("cover-image") == true)
                ?? (coverId != null && items.TryGetValue(coverId, out var legacyCover) ? legacyCover : null);
            var cover = coverItem == null ? null : ResolvePath(opfPath, (string)coverItem.Attribute("href")!);
            var ordered = new List<string>();
            foreach (var itemRef in opf.Descendants(opfNs + "spine").Elements(opfNs + "itemref"))
            {
                token.ThrowIfCancellationRequested();
                if (!items.TryGetValue((string?)itemRef.Attribute("idref") ?? "", out var item)) continue;
                var path = ResolvePath(opfPath, (string)item.Attribute("href")!);
                if (imagePaths.Contains(path)) ordered.Add(path);
                else if ((string?)item.Attribute("media-type") is "application/xhtml+xml" or "image/svg+xml")
                {
                    foreach (var image in ReadXml(archive, path).Descendants()
                                 .Where(x => x.Name.LocalName is "img" or "image"))
                    {
                        var href = image.Attributes().FirstOrDefault(x => x.Name.LocalName is "src" or "href")?.Value;
                        if (href == null) continue;
                        var imagePath = ResolvePath(path, href);
                        if (imagePaths.Contains(imagePath)) ordered.Add(imagePath);
                    }
                }
            }
            // Some image-only books omit the spine. Preserve manifest order in that case.
            if (ordered.Count == 0) ordered.AddRange(imagePaths);
            ordered = ordered.Distinct().ToList();
            if (ordered.Count == 0) throw new EpubImportException("EPUB 中没有可阅读的图片。");
            Directory.CreateDirectory(extractFolder);
            var extracted = new Dictionary<string, string>();
            string Extract(string source)
            {
                token.ThrowIfCancellationRequested();
                if (extracted.TryGetValue(source, out var cached)) return cached;
                var entry = archive.GetEntry(source) ?? throw new EpubImportException($"未找到图片：{source}");
                // Generated names avoid collisions and never use archive paths as disk paths.
                var destination = Path.Combine(extractFolder, $"{extracted.Count + 1:D6}{Path.GetExtension(source)}");
                entry.ExtractToFile(destination, overwrite: false);
                extracted.Add(source, destination);
                return destination;
            }
            var pages = ordered.Select(Extract).ToList();
            var coverPath = cover != null && imagePaths.Contains(cover) ? Extract(cover) : pages[0];
            return new EpubInfo(title, authors, coverPath, pages);
        }
        catch (OperationCanceledException) { throw; }
        catch (EpubImportException) { throw; }
        catch (Exception ex) { throw new EpubImportException("导入 EPUB 失败。", ex); }
    }
}
