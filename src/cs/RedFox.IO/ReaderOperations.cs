namespace RedFox.IO;

internal static class ReaderOperations
{
    internal static int FindNullTerminator(ReadOnlySpan<byte> buffer, int position, int terminatorSize)
    {
        for (int end = position; end <= buffer.Length - terminatorSize; end += terminatorSize)
        {
            bool isTerminator = true;
            for (int i = 0; i < terminatorSize; i++)
            {
                if (buffer[end + i] != 0)
                {
                    isTerminator = false;
                    break;
                }
            }

            if (isTerminator)
                return end;
        }

        return -1;
    }
}
