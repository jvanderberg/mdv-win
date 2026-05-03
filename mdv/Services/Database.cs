using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Mdv.Services;

public sealed record BookmarkRow(
    long Id,
    string Path,
    string Title,
    int SortOrder,
    DateTimeOffset CreatedAt,
    int BlockIndex,
    string BlockFingerprint);

public sealed record ScrollPosition(
    int BlockIndex,
    string BlockFingerprint,
    double FileMtimeUnix);

public sealed record SearchHit(
    string Path,
    string Filename,
    string Snippet);

/// Single SQLite store. Schema is shared verbatim with the macOS app
/// (articles + FTS5, bookmarks, scroll_positions, meta.schema_version=4).
/// FULLMUTEX-equivalent serialization via a .NET lock around the shared connection.
public sealed class Database : IDisposable
{
    private static readonly Lazy<Database> _shared = new(() => new Database());
    public static Database Shared => _shared.Value;

    private readonly SqliteConnection _db;
    private readonly object _lock = new();

    public static string DatabasePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mdv",
        "mdv.db");

    private Database()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        _db = new SqliteConnection($"Data Source={DatabasePath};Mode=ReadWriteCreate;Cache=Default;Pooling=False");
        _db.Open();
        Exec("PRAGMA journal_mode = WAL;");
        Exec("PRAGMA synchronous = NORMAL;");
        Migrate();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private void Migrate()
    {
        lock (_lock)
        {
            Exec("""
            CREATE TABLE IF NOT EXISTS meta (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);
            Exec("""
            CREATE TABLE IF NOT EXISTS articles (
                id          INTEGER PRIMARY KEY,
                path        TEXT NOT NULL UNIQUE,
                filename    TEXT NOT NULL,
                content     TEXT NOT NULL DEFAULT '',
                indexed_at  INTEGER NOT NULL,
                file_mtime  INTEGER NOT NULL DEFAULT 0,
                file_size   INTEGER NOT NULL DEFAULT 0
            );
            """);
            Exec("""
            CREATE VIRTUAL TABLE IF NOT EXISTS articles_fts USING fts5(
                filename,
                content,
                path UNINDEXED,
                content='articles',
                content_rowid='id',
                tokenize='unicode61 remove_diacritics 2'
            );
            """);
            Exec("""
            CREATE TRIGGER IF NOT EXISTS articles_ai AFTER INSERT ON articles BEGIN
                INSERT INTO articles_fts(rowid, filename, content, path)
                VALUES (new.id, new.filename, new.content, new.path);
            END;
            """);
            Exec("""
            CREATE TRIGGER IF NOT EXISTS articles_ad AFTER DELETE ON articles BEGIN
                INSERT INTO articles_fts(articles_fts, rowid, filename, content, path)
                VALUES ('delete', old.id, old.filename, old.content, old.path);
            END;
            """);
            Exec("""
            CREATE TRIGGER IF NOT EXISTS articles_au AFTER UPDATE ON articles BEGIN
                INSERT INTO articles_fts(articles_fts, rowid, filename, content, path)
                VALUES ('delete', old.id, old.filename, old.content, old.path);
                INSERT INTO articles_fts(rowid, filename, content, path)
                VALUES (new.id, new.filename, new.content, new.path);
            END;
            """);
            Exec("""
            CREATE TABLE IF NOT EXISTS bookmarks (
                id                INTEGER PRIMARY KEY,
                path              TEXT NOT NULL,
                title             TEXT NOT NULL,
                sort_order        INTEGER NOT NULL,
                created_at        INTEGER NOT NULL,
                block_index       INTEGER NOT NULL DEFAULT 0,
                block_fingerprint TEXT NOT NULL DEFAULT ''
            );
            """);
            Exec("CREATE INDEX IF NOT EXISTS bookmarks_sort ON bookmarks(sort_order);");
            Exec("""
            CREATE TABLE IF NOT EXISTS scroll_positions (
                path              TEXT PRIMARY KEY,
                block_index       INTEGER NOT NULL,
                block_fingerprint TEXT NOT NULL,
                updated_at        INTEGER NOT NULL,
                file_mtime        INTEGER NOT NULL DEFAULT 0
            );
            """);

            int version = int.TryParse(ScalarString("SELECT value FROM meta WHERE key = 'schema_version';"), out var v) ? v : 0;

            if (version < 2)
            {
                Exec("DROP TABLE IF EXISTS bookmarks;");
                Exec("""
                CREATE TABLE bookmarks (
                    id                INTEGER PRIMARY KEY,
                    path              TEXT NOT NULL,
                    title             TEXT NOT NULL,
                    sort_order        INTEGER NOT NULL,
                    created_at        INTEGER NOT NULL,
                    block_index       INTEGER NOT NULL DEFAULT 0,
                    block_fingerprint TEXT NOT NULL DEFAULT ''
                );
                """);
                Exec("CREATE INDEX IF NOT EXISTS bookmarks_sort ON bookmarks(sort_order);");
            }

            if (version == 3)
            {
                Exec("ALTER TABLE scroll_positions ADD COLUMN file_mtime INTEGER NOT NULL DEFAULT 0;");
            }

            if (version < 4)
            {
                Exec("INSERT INTO meta(key, value) VALUES ('schema_version', '4') ON CONFLICT(key) DO UPDATE SET value = '4';");
            }
        }
    }

    // MARK: - Bookmarks

    public List<BookmarkRow> LoadBookmarks()
    {
        lock (_lock)
        {
            var rows = new List<BookmarkRow>();
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT id, path, title, sort_order, created_at, block_index, block_fingerprint FROM bookmarks ORDER BY sort_order, created_at;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                rows.Add(new BookmarkRow(
                    Id: r.GetInt64(0),
                    Path: r.GetString(1),
                    Title: r.GetString(2),
                    SortOrder: (int)r.GetInt64(3),
                    CreatedAt: DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(4)),
                    BlockIndex: (int)r.GetInt64(5),
                    BlockFingerprint: r.IsDBNull(6) ? "" : r.GetString(6)));
            }
            return rows;
        }
    }

    public BookmarkRow? AddBookmark(string path, string title, int sortOrder, int blockIndex, string blockFingerprint)
    {
        lock (_lock)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using var cmd = _db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO bookmarks (path, title, sort_order, created_at, block_index, block_fingerprint)
                VALUES ($path, $title, $sort_order, $created_at, $block_index, $fp);
                """;
            cmd.Parameters.AddWithValue("$path", path);
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$sort_order", sortOrder);
            cmd.Parameters.AddWithValue("$created_at", now);
            cmd.Parameters.AddWithValue("$block_index", blockIndex);
            cmd.Parameters.AddWithValue("$fp", blockFingerprint);
            if (cmd.ExecuteNonQuery() != 1) return null;

            using var idCmd = _db.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid();";
            long id = (long)idCmd.ExecuteScalar()!;
            return new BookmarkRow(id, path, title, sortOrder, DateTimeOffset.FromUnixTimeSeconds(now), blockIndex, blockFingerprint);
        }
    }

    public void RemoveBookmark(long id)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "DELETE FROM bookmarks WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void SetBookmarkOrder(IReadOnlyList<long> idsInOrder)
    {
        lock (_lock)
        {
            using var tx = _db.BeginTransaction();
            for (int i = 0; i < idsInOrder.Count; i++)
            {
                using var cmd = _db.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE bookmarks SET sort_order = $order WHERE id = $id;";
                cmd.Parameters.AddWithValue("$order", i);
                cmd.Parameters.AddWithValue("$id", idsInOrder[i]);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    // MARK: - Scroll positions

    public ScrollPosition? LoadScrollPosition(string path)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT block_index, block_fingerprint, file_mtime FROM scroll_positions WHERE path = $path;";
            cmd.Parameters.AddWithValue("$path", path);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new ScrollPosition(
                BlockIndex: (int)r.GetInt64(0),
                BlockFingerprint: r.IsDBNull(1) ? "" : r.GetString(1),
                FileMtimeUnix: r.GetInt64(2));
        }
    }

    public void SaveScrollPosition(string path, int blockIndex, string fingerprint, double fileMtimeUnix)
    {
        lock (_lock)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using var cmd = _db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO scroll_positions (path, block_index, block_fingerprint, updated_at, file_mtime)
                VALUES ($path, $idx, $fp, $updated, $mtime)
                ON CONFLICT(path) DO UPDATE SET
                    block_index       = excluded.block_index,
                    block_fingerprint = excluded.block_fingerprint,
                    updated_at        = excluded.updated_at,
                    file_mtime        = excluded.file_mtime;
                """;
            cmd.Parameters.AddWithValue("$path", path);
            cmd.Parameters.AddWithValue("$idx", blockIndex);
            cmd.Parameters.AddWithValue("$fp", fingerprint);
            cmd.Parameters.AddWithValue("$updated", now);
            cmd.Parameters.AddWithValue("$mtime", (long)fileMtimeUnix);
            cmd.ExecuteNonQuery();
        }
    }

    public void ClearScrollPosition(string path)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "DELETE FROM scroll_positions WHERE path = $path;";
            cmd.Parameters.AddWithValue("$path", path);
            cmd.ExecuteNonQuery();
        }
    }

    // MARK: - Indexing

    public Task IndexFileAsync(string path) => Task.Run(() => IndexFile(path));

    public Task ReindexAsync(IEnumerable<string> paths) => Task.Run(() =>
    {
        foreach (var p in paths) IndexFile(p);
    });

    public Task RemoveFileAsync(string path) => Task.Run(() =>
    {
        lock (_lock)
        {
            foreach (var sql in new[]
            {
                "DELETE FROM articles WHERE path = $path;",
                "DELETE FROM scroll_positions WHERE path = $path;",
            })
            {
                using var cmd = _db.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("$path", path);
                cmd.ExecuteNonQuery();
            }
        }
    });

    private void IndexFile(string path)
    {
        FileInfo fi;
        try { fi = new FileInfo(path); if (!fi.Exists) return; }
        catch { return; }

        long mtime = ((DateTimeOffset)fi.LastWriteTimeUtc).ToUnixTimeSeconds();
        long size = fi.Length;

        lock (_lock)
        {
            long? existing = ScalarInt64("SELECT file_mtime FROM articles WHERE path = $path;", ("$path", path));
            if (existing == mtime) return;
        }

        string content;
        try { content = File.ReadAllText(path); }
        catch { return; }

        string filename = Path.GetFileName(path);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO articles (path, filename, content, indexed_at, file_mtime, file_size)
                VALUES ($path, $filename, $content, $indexed, $mtime, $size)
                ON CONFLICT(path) DO UPDATE SET
                    filename   = excluded.filename,
                    content    = excluded.content,
                    indexed_at = excluded.indexed_at,
                    file_mtime = excluded.file_mtime,
                    file_size  = excluded.file_size;
                """;
            cmd.Parameters.AddWithValue("$path", path);
            cmd.Parameters.AddWithValue("$filename", filename);
            cmd.Parameters.AddWithValue("$content", content);
            cmd.Parameters.AddWithValue("$indexed", now);
            cmd.Parameters.AddWithValue("$mtime", mtime);
            cmd.Parameters.AddWithValue("$size", size);
            cmd.ExecuteNonQuery();
        }
    }

    // MARK: - Search

    public Task<List<SearchHit>> SearchAsync(string rawQuery, int limit = 80) =>
        Task.Run(() => Search(rawQuery, limit));

    private List<SearchHit> Search(string rawQuery, int limit)
    {
        var trimmed = rawQuery.Trim();
        if (string.IsNullOrEmpty(trimmed)) return new();
        var ftsQuery = MakeFtsQuery(trimmed);
        if (string.IsNullOrEmpty(ftsQuery)) return new();

        lock (_lock)
        {
            var hits = new List<SearchHit>();
            using var cmd = _db.CreateCommand();
            cmd.CommandText = """
                SELECT
                    a.path,
                    a.filename,
                    snippet(articles_fts, 1, char(2), char(3), '…', 14)
                FROM articles_fts f
                JOIN articles a ON a.id = f.rowid
                WHERE articles_fts MATCH $q
                ORDER BY rank
                LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$q", ftsQuery);
            cmd.Parameters.AddWithValue("$limit", limit);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                hits.Add(new SearchHit(
                    Path: r.GetString(0),
                    Filename: r.GetString(1),
                    Snippet: r.GetString(2)));
            }
            return hits;
        }
    }

    public static string MakeFtsQuery(string input)
    {
        var bad = new HashSet<char> { '"', '(', ')', ':', '*', '^' };
        var parts = new List<string>();
        foreach (var tok in input.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var cleaned = new System.Text.StringBuilder(tok.Length);
            foreach (var ch in tok)
                if (!bad.Contains(ch)) cleaned.Append(ch);
            if (cleaned.Length > 0)
                parts.Add($"\"{cleaned}\"*");
        }
        return string.Join(" ", parts);
    }

    // MARK: - Helpers

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private string? ScalarString(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        var v = cmd.ExecuteScalar();
        return v == null || v == DBNull.Value ? null : v.ToString();
    }

    private long? ScalarInt64(string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in parameters) cmd.Parameters.AddWithValue(n, v);
        var v0 = cmd.ExecuteScalar();
        return v0 == null || v0 == DBNull.Value ? null : Convert.ToInt64(v0);
    }
}

public static class BookmarkAnchor
{
    public static string Fingerprint(string block)
    {
        var collapsed = string.Join(' ',
            block.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
        return collapsed.Length <= 80 ? collapsed : collapsed[..80];
    }

    public static int Resolve(IReadOnlyList<string> blocks, int storedIndex, string fingerprint)
    {
        if (blocks.Count == 0) return 0;
        if (!string.IsNullOrEmpty(fingerprint))
        {
            for (int i = 0; i < blocks.Count; i++)
                if (Fingerprint(blocks[i]) == fingerprint) return i;
        }
        return Math.Max(0, Math.Min(storedIndex, blocks.Count - 1));
    }
}
