using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Dispatching;
using ShadowViewer.Plugin.EpubLoader.Services;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Models;
using Windows.Storage;

static class ImportChecks
{
    public static async Task Run(string root, string book)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var factory = new Factory(connection);
        using (var db = factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
            db.Add(new SourcePluginData("ShadowViewer.Plugin.EpubLoader", "0.2.0", "EPUB", "#ffffff", "#000000"));
            db.SaveChanges();
        }
        var importer = new EpubComicImporter(factory, root);
        var preview = await importer.Preview(new StorageFile(book));
        if (!preview.HasChapters || preview.ComicDetail.PageCount != 2) throw new Exception("Invalid import preview");
        await importer.ImportComic(preview, -1, new DispatcherQueue(), CancellationToken.None);
        using (var db = factory.CreateDbContext())
        {
            var comic = db.Set<ComicNode>().Include(x => x.ComicDetail)!.ThenInclude(x => x!.Authors)
                .Include(x => x.ReadingRecord).Single();
            if (comic.ComicDetail!.PageCount != 2 || comic.ComicDetail.Authors!.Single().Name != "作者" ||
                comic.ReadingRecord == null || db.Set<ComicChapter>().Single().ComicId != comic.Id)
                throw new Exception("Incomplete imported graph");
            var pictures = db.Set<ComicPicture>().OrderBy(x => x.Name).ToList();
            if (!pictures.Select(x => File.ReadAllText(x.StoragePath!)).SequenceEqual(["B", "A"]))
                throw new Exception("Reader page order changed during persistence");
        }
        Console.WriteLine("PASS actual importer persists book, source, authors, reading record and ordered pages");
        var folders = Directory.GetDirectories(Path.Combine(root, "comics")).Length;
        try
        {
            var invalid = new EpubComicImporter(factory, root, "missing-source");
            await invalid.ImportComic(preview, -1, new DispatcherQueue(), CancellationToken.None);
            throw new Exception("Expected foreign key failure");
        }
        catch (DbUpdateException) { }
        using (var db = factory.CreateDbContext())
        {
            if (db.Set<ComicNode>().Count() != 1 || db.Set<ComicPicture>().Count() != 2 ||
                db.Set<LocalAuthor>().Count() != 1 || Directory.GetDirectories(Path.Combine(root, "comics")).Length != folders)
                throw new Exception("Failed import left database rows or extracted files");
        }
        Console.WriteLine("PASS import failure rolls back the graph and removes only its extracted directory");
    }

    private sealed class Factory(SqliteConnection connection) : IDbContextFactory<LocalDbContext>
    {
        public LocalDbContext CreateDbContext() => new(new DbContextOptionsBuilder<LocalDbContext>().UseSqlite(connection).Options);
    }
}
