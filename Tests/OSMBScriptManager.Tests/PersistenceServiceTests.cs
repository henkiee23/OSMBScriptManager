using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OSMBScriptManager.Services;
using Xunit;

namespace OSMBScriptManager.Tests;

public class PersistenceServiceTests
{
    [Fact]
    public async Task SaveAndLoadState_PersistsData()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "persistence_test_" + Path.GetRandomFileName() + ".json");
        try
        {
            var svc = new PersistenceService(tmp);
            var state = new Dictionary<string, string>
            {
                ["a"] = "1",
                ["b"] = "two"
            };

            await svc.SaveStateAsync(state);

            var loaded = await svc.LoadStateAsync();

            Assert.Equal(2, loaded.Count);
            Assert.Equal("1", loaded["a"]);
            Assert.Equal("two", loaded["b"]);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public async Task LoadState_Nonexistent_ReturnsEmpty()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "persistence_test_nonexistent_" + Path.GetRandomFileName() + ".json");
        try
        {
            if (File.Exists(tmp)) File.Delete(tmp);
            var svc = new PersistenceService(tmp);
            var loaded = await svc.LoadStateAsync();
            Assert.Empty(loaded);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }
}
