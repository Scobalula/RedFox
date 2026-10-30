namespace RedFox.Tests.Graphics3D;

public sealed class CorpusTheoryAttribute : TheoryAttribute
{
    public CorpusTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("REDFOX_RUN_CORPUS") != "1")
            Skip = "Optional corpus: set REDFOX_RUN_CORPUS=1 and provide the reference files.";
    }
}
