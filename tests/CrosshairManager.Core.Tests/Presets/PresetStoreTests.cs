using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using static CrosshairManager.Core.Tests.Presets.PresetTestData;

namespace CrosshairManager.Core.Tests.Presets;

public class PresetStoreTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "chm-presets-" + Guid.NewGuid().ToString("N"));
    private readonly FixedTimeProvider _time = new(T0);

    private string DataDir => Path.Combine(_root, "data");
    private string PresetsDir => Path.Combine(DataDir, "presets");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private PresetStore NewStore(string? dataDir = null) => new(dataDir ?? DataDir, _time);

    private string WriteRaw(string id, string json)
    {
        Directory.CreateDirectory(PresetsDir);
        string path = Path.Combine(PresetsDir, id + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string[] UserIds(PresetStore store) =>
        store.LoadAll().Where(e => e.FilePath is not null).Select(e => e.Id).ToArray();

    [Fact]
    public void LoadAll_on_missing_folder_lists_built_ins_only_and_creates_nothing()
    {
        var entries = NewStore().LoadAll();

        Assert.Equal(BuiltInPresets.All.Select(p => p.Id), entries.Select(e => e.Id));
        Assert.All(entries, e => Assert.Equal(BuiltInPresets.All.Single(p => p.Id == e.Id), e.Preset));
        Assert.False(Directory.Exists(DataDir));
    }

    [Fact]
    public void Created_preset_is_listed_after_built_ins_and_round_trips()
    {
        var store = NewStore();
        var spec = PresetTestData.RandomSpec(new Random(1));

        var created = store.Create("Mine", spec);

        Assert.True(Guid.TryParse(created.Id, out _));
        Assert.Equal("Mine", created.Name);
        Assert.Equal(T0.UtcDateTime, created.CreatedUtc);
        Assert.Equal(T0.UtcDateTime, created.ModifiedUtc);
        Assert.Equal(spec, created.Crosshair);
        Assert.False(created.IsBuiltIn);
        Assert.True(File.Exists(Path.Combine(PresetsDir, created.Id + ".json")));

        var entries = NewStore().LoadAll();
        Assert.Equal(7, entries.Count);
        Assert.Equal(created, entries[6].Preset);
    }

    [Fact]
    public void User_presets_sort_by_CreatedUtc_then_ordinal_Id()
    {
        var store = NewStore();
        _time.Now = T0.AddHours(2);
        var later = store.Create("later", CrosshairSpec.Default);
        _time.Now = T0;
        var earliest = store.Create("earliest", CrosshairSpec.Default);
        _time.Now = T0.AddHours(5);
        var tieA = store.Create("tieA", CrosshairSpec.Default);
        var tieB = store.Create("tieB", CrosshairSpec.Default);
        var tie = new[] { tieA.Id, tieB.Id }.Order(StringComparer.Ordinal);

        Assert.Equal(new[] { earliest.Id, later.Id }.Concat(tie), UserIds(NewStore()));
    }

    [Fact]
    public void Equal_CreatedUtc_in_files_sorts_by_ordinal_Id()
    {
        const string body = ""","schemaVersion":1,"createdUtc":"2026-01-01T00:00:00Z","crosshair":{}}""";
        WriteRaw("b0000000-0000-0000-0000-000000000000", "{\"name\":\"b\"" + body);
        WriteRaw("B0000000-0000-0000-0000-000000000001", "{\"name\":\"B\"" + body);
        WriteRaw("a0000000-0000-0000-0000-000000000000", "{\"name\":\"a\"" + body);

        Assert.Equal(
            new[] { "B0000000-0000-0000-0000-000000000001", "a0000000-0000-0000-0000-000000000000", "b0000000-0000-0000-0000-000000000000" },
            UserIds(NewStore()));
    }

    [Fact]
    public void NewerVersion_file_is_listed_and_never_rewritten()
    {
        string id = "11111111-1111-1111-1111-111111111111";
        string path = WriteRaw(id, """{"schemaVersion":2,"id":"11111111-1111-1111-1111-111111111111","name":"future","crosshair":{"color":"#FF0000","newThing":{}}}""");
        string hash = Sha256(path);
        var store = NewStore();

        var entry = store.LoadAll().Single(e => e.Id == id);

        Assert.Equal(new PresetLoadResult.NewerVersion(2), entry.Result);
        Assert.Null(entry.Preset);
        Assert.Equal(path, entry.FilePath);
        Assert.Throws<InvalidOperationException>(() => store.Rename(id, "x"));
        Assert.Throws<InvalidOperationException>(() => store.Save(new Preset(id, "x", T0.UtcDateTime, T0.UtcDateTime, CrosshairSpec.Default)));
        Assert.Throws<InvalidOperationException>(() => store.Duplicate(id, "copy"));
        store.LoadAll();
        Assert.Equal(hash, Sha256(path));
        Assert.False(File.Exists(path + ".bak"));
        Assert.Equal(new[] { id + ".json" }, Directory.GetFiles(PresetsDir).Select(Path.GetFileName));
    }

    [Fact]
    public void Broken_file_is_listed_and_never_rewritten()
    {
        string id = "22222222-2222-2222-2222-222222222222";
        string path = WriteRaw(id, "{ \"schemaVersion\": 1, \"crosshair\": ");
        string hash = Sha256(path);
        var store = NewStore();

        var entry = store.LoadAll().Single(e => e.Id == id);

        Assert.IsType<PresetLoadResult.Broken>(entry.Result);
        Assert.Throws<InvalidOperationException>(() => store.Rename(id, "x"));
        Assert.Throws<InvalidOperationException>(() => store.Save(new Preset(id, "x", T0.UtcDateTime, T0.UtcDateTime, CrosshairSpec.Default)));
        Assert.Equal(hash, Sha256(path));
    }

    [Fact]
    public void Unusable_files_sort_after_usable_user_presets()
    {
        var store = NewStore();
        WriteRaw("00000000-0000-0000-0000-000000000000", "broken");
        var good = store.Create("good", CrosshairSpec.Default);

        Assert.Equal(new[] { good.Id, "00000000-0000-0000-0000-000000000000" }, UserIds(store));
    }

    [Fact]
    public void Bak_and_tmp_files_are_not_listed()
    {
        var store = NewStore();
        var p = store.Create("p", CrosshairSpec.Default);
        store.Rename(p.Id, "q");
        File.WriteAllText(Path.Combine(PresetsDir, "x.json.tmp"), "{}");

        Assert.True(File.Exists(Path.Combine(PresetsDir, p.Id + ".json.bak")));
        Assert.Equal(new[] { p.Id }, UserIds(store));
    }

    [Fact]
    public void Built_in_presets_cannot_be_changed()
    {
        var store = NewStore();
        var builtIn = BuiltInPresets.All[0];

        Assert.Throws<InvalidOperationException>(() => store.Delete(builtIn.Id));
        Assert.Throws<InvalidOperationException>(() => store.Rename(builtIn.Id, "x"));
        Assert.Throws<InvalidOperationException>(() => store.Save(builtIn));
        Assert.Throws<InvalidOperationException>(() => store.Save(builtIn with { IsBuiltIn = false }));
        Assert.Throws<InvalidOperationException>(() => store.Save(new Preset("3f2b8c1e-5a7d-4e2f-9b1c-0d6e8a4f7c21", "x", T0.UtcDateTime, T0.UtcDateTime, CrosshairSpec.Default, IsBuiltIn: true)));
        Assert.False(Directory.Exists(PresetsDir));
    }

    [Fact]
    public void Rename_changes_name_and_ModifiedUtc_only()
    {
        var store = NewStore();
        var p = store.Create("old", CrosshairSpec.Default);
        _time.Now = T0.AddMinutes(3);

        var renamed = store.Rename(p.Id, "new");

        Assert.Equal(p with { Name = "new", ModifiedUtc = T0.AddMinutes(3).UtcDateTime }, renamed);
        Assert.Equal(renamed, NewStore().LoadAll().Single(e => e.Id == p.Id).Preset);
    }

    [Fact]
    public void Save_normalizes_spec_and_updates_ModifiedUtc()
    {
        var store = NewStore();
        var p = store.Create("p", CrosshairSpec.Default);
        _time.Now = T0.AddMinutes(1);

        var saved = store.Save(p with { Crosshair = p.Crosshair with { Opacity = 500 } });

        Assert.Equal(100, saved.Crosshair.Opacity);
        Assert.Equal(T0.AddMinutes(1).UtcDateTime, saved.ModifiedUtc);
        Assert.Equal(p.CreatedUtc, saved.CreatedUtc);
        Assert.Equal(saved, NewStore().LoadAll().Single(e => e.Id == p.Id).Preset);
    }

    [Fact]
    public void Duplicate_copies_spec_under_new_id_including_built_ins()
    {
        var store = NewStore();
        var builtIn = BuiltInPresets.All[4];

        var copy = store.Duplicate(builtIn.Id, "copy");
        var copy2 = store.Duplicate(copy.Id, "copy2");

        Assert.True(Guid.TryParse(copy.Id, out _));
        Assert.NotEqual(copy.Id, copy2.Id);
        Assert.Equal(builtIn.Crosshair, copy.Crosshair);
        Assert.Equal(builtIn.Crosshair, copy2.Crosshair);
        Assert.False(copy.IsBuiltIn);
        Assert.Equal("copy", copy.Name);
        Assert.Equal(new[] { copy.Id, copy2.Id }.Order(StringComparer.Ordinal), UserIds(store).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Delete_removes_file_and_bak()
    {
        var store = NewStore();
        var p = store.Create("p", CrosshairSpec.Default);
        store.Rename(p.Id, "p2");
        string path = Path.Combine(PresetsDir, p.Id + ".json");
        Assert.True(File.Exists(path + ".bak"));

        store.Delete(p.Id);

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".bak"));
        Assert.Empty(UserIds(store));
    }

    [Fact]
    public void Delete_removes_broken_file_too()
    {
        string path = WriteRaw("33333333-3333-3333-3333-333333333333", "garbage");

        NewStore().Delete("33333333-3333-3333-3333-333333333333");

        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("..\\evil")]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("")]
    public void Path_like_ids_are_rejected(string id)
    {
        var store = NewStore();

        Assert.ThrowsAny<ArgumentException>(() => store.Delete(id));
        Assert.ThrowsAny<ArgumentException>(() => store.Save(new Preset(id, "x", T0.UtcDateTime, T0.UtcDateTime, CrosshairSpec.Default)));
    }

    [Fact]
    public void Unknown_id_throws_KeyNotFound()
    {
        var store = NewStore();

        Assert.Throws<KeyNotFoundException>(() => store.Rename("44444444-4444-4444-4444-444444444444", "x"));
        Assert.Throws<KeyNotFoundException>(() => store.ExportFile("44444444-4444-4444-4444-444444444444", Path.Combine(_root, "out.json")));
    }

    [Fact]
    public void Export_then_import_into_another_store_round_trips()
    {
        var store = NewStore();
        var p = store.Create("shared", PresetTestData.RandomSpec(new Random(2)));
        string exported = Path.Combine(_root, "export", "shared.json");

        store.ExportFile(p.Id, exported);
        _time.Now = T0.AddDays(1);
        var other = NewStore(Path.Combine(_root, "other"));
        var imported = other.ImportFile(exported);

        Assert.Equal(p, imported);
        Assert.Equal(p, other.LoadAll().Single(e => e.Id == p.Id).Preset);
    }

    [Fact]
    public void Export_with_a_name_override_writes_that_name_and_leaves_the_store_unchanged()
    {
        var store = NewStore();
        string exported = Path.Combine(_root, "named.json");

        store.ExportFile("builtin.cross", exported, "Cross");

        var ok = Assert.IsType<PresetLoadResult.Ok>(PresetFile.Load(File.ReadAllText(exported)));
        Assert.Equal("Cross", ok.Preset.Name);
        Assert.Equal(BuiltInPresets.All.Single(p => p.Id == "builtin.cross").Crosshair, ok.Preset.Crosshair);
        Assert.Empty(UserIds(store));
    }

    [Fact]
    public void Importing_a_conflicting_id_gets_a_new_guid()
    {
        var store = NewStore();
        var p = store.Create("p", PresetTestData.RandomSpec(new Random(3)));
        string exported = Path.Combine(_root, "p.json");
        store.ExportFile(p.Id, exported);

        var imported = store.ImportFile(exported);

        Assert.NotEqual(p.Id, imported.Id);
        Assert.True(Guid.TryParse(imported.Id, out _));
        Assert.Equal(p with { Id = imported.Id }, imported);
        Assert.Equal(2, UserIds(store).Length);
    }

    [Fact]
    public void Importing_an_exported_built_in_gets_a_new_guid()
    {
        var store = NewStore();
        string exported = Path.Combine(_root, "dot.json");
        store.ExportFile("builtin.dot", exported);

        var imported = store.ImportFile(exported);

        Assert.True(Guid.TryParse(imported.Id, out _));
        Assert.False(imported.IsBuiltIn);
        Assert.Equal(BuiltInPresets.All[1].Crosshair, imported.Crosshair);
    }

    [Fact]
    public void Importing_a_path_like_id_stays_inside_presets_folder()
    {
        string source = Path.Combine(_root, "evil.json");
        Directory.CreateDirectory(_root);
        File.WriteAllText(source, """{"schemaVersion":1,"id":"..\\..\\evil","crosshair":{}}""");

        var imported = NewStore().ImportFile(source);

        Assert.True(Guid.TryParse(imported.Id, out _));
        Assert.Equal(new[] { imported.Id + ".json" }, Directory.GetFiles(PresetsDir).Select(Path.GetFileName));
        Assert.False(File.Exists(Path.Combine(_root, "evil.json.json")));
    }

    [Fact]
    public void Importing_out_of_range_file_saves_clamped_values()
    {
        string source = Path.Combine(_root, "wide.json");
        Directory.CreateDirectory(_root);
        File.WriteAllText(source, """{"schemaVersion":1,"crosshair":{"opacity":300}}""");

        var imported = NewStore().ImportFile(source);

        Assert.Equal(100, imported.Crosshair.Opacity);
        Assert.Equal(imported, NewStore().LoadAll().Single(e => e.Id == imported.Id).Preset);
    }

    [Theory]
    [InlineData("""{"schemaVersion":2,"crosshair":{}}""")]
    [InlineData("{oops")]
    public void Importing_unusable_file_throws_and_leaves_source_untouched(string json)
    {
        string source = Path.Combine(_root, "bad.json");
        Directory.CreateDirectory(_root);
        File.WriteAllText(source, json);
        string hash = Sha256(source);

        Assert.Throws<InvalidDataException>(() => NewStore().ImportFile(source));
        Assert.Equal(hash, Sha256(source));
        Assert.False(Directory.Exists(PresetsDir) && Directory.EnumerateFileSystemEntries(PresetsDir).Any());
    }

    [Fact]
    public void Fixture_file_in_presets_folder_loads()
    {
        string fixture = FixturePath("presets", "v1.json");
        Directory.CreateDirectory(PresetsDir);
        string path = Path.Combine(PresetsDir, "3f2b8c1e-5a7d-4e2f-9b1c-0d6e8a4f7c21.json");
        File.Copy(fixture, path);

        var entry = NewStore().LoadAll().Single(e => e.FilePath is not null);

        Assert.Equal("Fixture v1", entry.Preset!.Name);
        Assert.Equal("3f2b8c1e-5a7d-4e2f-9b1c-0d6e8a4f7c21", entry.Id);
    }

    [Fact]
    public void File_name_is_the_identity_when_content_id_differs()
    {
        WriteRaw("55555555-5555-5555-5555-555555555555", """{"schemaVersion":1,"id":"66666666-6666-6666-6666-666666666666","crosshair":{}}""");

        var entry = NewStore().LoadAll().Single(e => e.FilePath is not null);

        Assert.Equal("55555555-5555-5555-5555-555555555555", entry.Id);
        Assert.Equal("55555555-5555-5555-5555-555555555555", entry.Preset!.Id);
    }
}
