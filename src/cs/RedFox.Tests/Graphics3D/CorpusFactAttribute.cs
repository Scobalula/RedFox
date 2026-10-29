namespace RedFox.Tests.Graphics3D;

public sealed class CorpusFactAttribute : FactAttribute
{
    public CorpusFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("REDFOX_RUN_CORPUS") != "1")
            Skip = "Optional corpus: set REDFOX_RUN_CORPUS=1 and provide the reference files.";
    }
}
