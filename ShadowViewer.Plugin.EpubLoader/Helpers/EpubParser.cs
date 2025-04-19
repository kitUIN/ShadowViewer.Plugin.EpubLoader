using System;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using ShadowViewer.Plugin.EpubLoader.Exceptions;
using ShadowViewer.Plugin.EpubLoader.Models;

namespace ShadowViewer.Plugin.EpubLoader.Helpers;

/// <summary>
/// 
/// </summary>
public static class EpubParser
{
    /// <summary>
    /// 导出到文件夹
    /// </summary>
    /// <param name="archive"></param>
    /// <param name="relativePath"></param>
    /// <param name="extractFolder"></param>
    /// <returns></returns>
    private static string? ExtractEntry(ZipArchive archive, string relativePath, string extractFolder)
    {
        var entry = archive.GetEntry(relativePath);
        if (entry == null)
            return null;

        string fileName = Path.GetFileName(entry.FullName);
        string destPath = Path.Combine(extractFolder, fileName);

        Directory.CreateDirectory(extractFolder);
        entry.ExtractToFile(destPath, overwrite: true);
        return destPath;
    }
    /// <summary>
    /// 相对路径转换
    /// </summary>
    /// <param name="basePath"></param>
    /// <param name="relativePath"></param>
    /// <returns></returns>
    private static string NormalizeRelativePath(string basePath, string relativePath)
    {
        if (!relativePath.Contains(".."))
        {
            return relativePath;
        }

        string baseDir = Path.GetDirectoryName(basePath) ?? string.Empty;
        string[] baseParts = baseDir.Replace('\\', '/').Split('/');
        string[] relParts = relativePath.Split('/');
        Stack<string> stack = new(baseParts);
        foreach (string part in relParts)
        {
            if (part == ".." && stack.Count > 0)
            {
                stack.Pop();
            }
            else if (part != "." && part != string.Empty)
            {
                stack.Push(part);
            }
        }

        return string.Join("/", stack.Reverse());
    }

    /// <summary>
    /// 读取
    /// </summary>
    /// <param name="zipPath"></param>
    /// <param name="extractFolder"></param>
    /// <returns></returns>
    /// <exception cref="EpubImportException"></exception>
    public static EpubInfo Parse(string zipPath, string extractFolder)
    {
        try
        {
            using (FileStream zipToOpen = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
            {
                // Step 1: 读取 container.xml
                var containerEntry = archive.GetEntry("META-INF/container.xml");
                if (containerEntry == null)
                    throw new EpubImportException("未找到 META-INF/container.xml 文件。");

                string opfPath;
                using (var reader = new StreamReader(containerEntry.Open()))
                {
                    try
                    {
                        var xml = XDocument.Load(reader);
                        XNamespace ns = "urn:oasis:names:tc:opendocument:xmlns:container";
                        var rootfileElement = xml.Element(ns + "container")?
                            .Element(ns + "rootfiles")?
                            .Element(ns + "rootfile");

                        opfPath = rootfileElement?.Attribute("full-path")?.Value
                                  ?? throw new EpubImportException("container.xml 中未找到 OPF 路径。");
                    }
                    catch (Exception ex)
                    {
                        throw new EpubImportException("解析 container.xml 失败。", ex);
                    }
                }

                // Step 2: 读取 OPF 文件
                var opfEntry = archive.GetEntry(opfPath);
                if (opfEntry == null)
                    throw new EpubImportException($"未找到 OPF 文件：{opfPath}");

                string opfContent;
                using (var reader = new StreamReader(opfEntry.Open()))
                {
                    opfContent = reader.ReadToEnd();
                }

                // Step 3: 解析 OPF 内容
                XDocument opfDoc;
                try
                {
                    opfDoc = XDocument.Parse(opfContent);
                }
                catch (Exception ex)
                {
                    throw new EpubImportException("解析 OPF 文件失败。", ex);
                }

                XNamespace nsOpf = "http://www.idpf.org/2007/opf";
                XNamespace nsDc = "http://purl.org/dc/elements/1.1/";

                var metadata = opfDoc.Descendants(nsOpf + "metadata").FirstOrDefault();

                string title = metadata?.Element(nsDc + "title")?.Value ?? "(Unknown)";
                var authors = metadata?.Elements(nsDc + "creator")
                    .Select(c => c.Value.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList() ?? new List<string>();

                // 图片列表与封面
                var items = opfDoc.Descendants(nsOpf + "manifest").Elements(nsOpf + "item").ToList();
                var imageItems = items
                    .Where(item => (string?)item.Attribute("media-type") == "image/jpeg")
                    .ToList();

                var coverItem = imageItems
                    .FirstOrDefault(item => ((string?)item.Attribute("properties"))?.Contains("cover-image") == true);

                if (coverItem != null)
                {
                    imageItems.Remove(coverItem);
                }

                string? coverPath = coverItem?.Attribute("href")?.Value;
                if (!string.IsNullOrEmpty(coverPath)) coverPath = NormalizeRelativePath(opfPath, coverPath);
                List<string> imagePaths = imageItems
                    .Select(item => item.Attribute("href")?.Value)
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Select(x=>NormalizeRelativePath(opfPath, x!))
                    .ToList()!;
                string? extractedCoverPath = null;
                if (coverPath != null)
                {
                    extractedCoverPath = ExtractEntry(archive, coverPath, extractFolder);
                }

                // 解压图片
                var extractedImagePaths = imagePaths.Select(image => ExtractEntry(archive, image, extractFolder)).OfType<string>().ToList();
                return new EpubInfo(title, authors, extractedCoverPath, extractedImagePaths);
            }
        }
        catch (EpubImportException)
        {
            throw; // 保留自定义错误
        }
        catch (Exception ex)
        {
            throw new EpubImportException("导入 EPUB 失败。", ex);
        }
    }
}