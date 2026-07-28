namespace SpiritVale.RuntimeLocalization;

internal static class CjkText
{
    internal static bool ContainsCjk(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        foreach (var character in value)
        {
            if (IsCjk(character))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool IsCjk(char character)
    {
        return (character >= '\u3400' && character <= '\u4DBF') ||
            (character >= '\u4E00' && character <= '\u9FFF') ||
            (character >= '\uF900' && character <= '\uFAFF');
    }
}
