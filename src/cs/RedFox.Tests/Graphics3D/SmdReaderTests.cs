using System.Text;
using RedFox.Graphics3D.Formats.StudioMDL;

namespace RedFox.Tests.Graphics3D;

public sealed class SmdReaderTests
{
    [Fact]
    public void SmdReader_ParseTriangles_PreservesMaterialNamesStartingWithDigits()
    {
        string smdText = """
            123material
            0 0 0 0 0 0 1 0 0
            0 1 0 0 0 0 1 0 0
            0 0 1 0 0 0 1 0 0
            end
            """;
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(smdText));
        using StreamReader reader = new(stream);
        Dictionary<string, List<SmdVertex>> groups = new(StringComparer.Ordinal);

        SmdReader.ParseTriangles(reader, groups);

        List<SmdVertex> vertices = Assert.IsType<List<SmdVertex>>(groups.GetValueOrDefault("123material"));
        Assert.Equal(3, vertices.Count);
    }
}
