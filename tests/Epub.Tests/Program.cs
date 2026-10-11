using System.IO.Compression;
using ShadowViewer.Plugin.EpubLoader.Exceptions;
using ShadowViewer.Plugin.EpubLoader.Helpers;

var root = Path.Combine(Path.GetTempPath(), "EpubTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string Book(string name, string manifest, string spine, Dictionary<string, string> files,
        string metadata = "")
    {
        var path = Path.Combine(root, name + ".epub");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        void Entry(string key, string text)
        {
            using var writer = new StreamWriter(archive.CreateEntry(key).Open());
            writer.Write(text);
        }
        Entry("META-INF/container.xml", """
            <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
              <rootfiles><rootfile full-path="OEBPS/content.opf"/></rootfiles>
            </container>
            """);
        Entry("OEBPS/content.opf", $"""
            <package xmlns="http://www.idpf.org/2007/opf">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>测试书</dc:title><dc:creator>作者</dc:creator>{metadata}
              </metadata><manifest>{manifest}</manifest><spine>{spine}</spine>
            </package>
            """);
        foreach (var file in files) Entry(file.Key, file.Value);
        return path;
    }
    void Check(bool condition, string scenario)
    {
        if (!condition) throw new Exception(scenario);
        Console.WriteLine("PASS " + scenario);
    }
    var book = Book("spine", """
        <item id="a" href="images/a/page.png" media-type="image/png"/>
        <item id="b" href="images/b/page.png" media-type="image/png" properties="cover-image"/>
        <item id="c1" href="text/1.xhtml" media-type="application/xhtml+xml"/>
        <item id="c2" href="text/2.xhtml" media-type="application/xhtml+xml"/>
        """, "<itemref idref='c2'/><itemref idref='c1'/>", new()
        {
            ["OEBPS/images/a/page.png"] = "A",
            ["OEBPS/images/b/page.png"] = "B",
            ["OEBPS/text/1.xhtml"] = "<html><img src='../images/a/page.png'/></html>",
            ["OEBPS/text/2.xhtml"] = "<svg><image href='../images/b/page.png'/></svg>"
        });
    var info = EpubParser.Parse(book, Path.Combine(root, "spine"));
    Check(info.Title == "测试书" && info.Authors.SequenceEqual(["作者"]), "metadata survives import");
    Check(info.ImagePaths.Select(File.ReadAllText).SequenceEqual(["B", "A"]), "nested paths and spine order");
    Check(info.ImagePaths.Distinct().Count() == 2, "duplicate basenames do not overwrite pages");
    Check(info.CoverImagePath == info.ImagePaths[0], "EPUB3 cover and SVG image references");
    var legacy = Book("legacy", "<item id='cover' href='images/my%20cover.jpg' media-type='image/jpeg'/>", "",
        new() { ["OEBPS/images/my cover.jpg"] = "C" }, "<meta name='cover' content='cover'/>");
    var legacyInfo = EpubParser.Parse(legacy, Path.Combine(root, "legacy"));
    Check(File.ReadAllText(legacyInfo.CoverImagePath!) == "C", "EPUB2 cover, URL escapes, and manifest fallback");
    var empty = Book("empty", "", "", new());
    try { EpubParser.Parse(empty, Path.Combine(root, "empty")); throw new Exception("accepted empty EPUB"); }
    catch (EpubImportException) { Check(true, "empty EPUB rejected before database import"); }
    try { EpubParser.Parse(book, Path.Combine(root, "cancel"), new CancellationToken(true)); throw new Exception("ignored cancellation"); }
    catch (OperationCanceledException) { Check(!Directory.Exists(Path.Combine(root, "cancel")), "cancellation does not extract pages"); }
    await ImportChecks.Run(root, book);
}
finally
{
    Directory.Delete(root, true);
}
