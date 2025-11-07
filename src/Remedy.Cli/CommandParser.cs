namespace Remedy.Cli;

public class CommandParser
{
    private readonly string[] _args;

    public CommandParser(string[] args)
    {
        _args = args;
    }

    public string? GetCommand()
    {
        if (_args.Length == 0) return null;
        return _args[0].ToLowerInvariant();
    }

    public string? GetSubCommand()
    {
        if (_args.Length < 2) return null;
        return _args[1].ToLowerInvariant();
    }

    public string? GetArgument(int position)
    {
        if (position >= _args.Length) return null;
        var arg = _args[position];
        if (arg.StartsWith("--") || arg.StartsWith("-")) return null;
        return arg;
    }

    public string? GetOption(params string[] names)
    {
        for (int i = 0; i < _args.Length - 1; i++)
        {
            var arg = _args[i].ToLowerInvariant();
            if (names.Contains(arg))
            {
                return _args[i + 1];
            }
        }
        return null;
    }

    public T GetOption<T>(T defaultValue, params string[] names) where T : struct, Enum
    {
        var value = GetOption(names);
        if (value != null && Enum.TryParse<T>(value, true, out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public int GetIntOption(int defaultValue, params string[] names)
    {
        var value = GetOption(names);
        if (value != null && int.TryParse(value, out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public Guid? GetGuidArgument(int position)
    {
        var value = GetArgument(position);
        if (value != null && Guid.TryParse(value, out var result))
        {
            return result;
        }
        return null;
    }

    public bool HasFlag(params string[] names)
    {
        return _args.Any(arg => names.Contains(arg.ToLowerInvariant()));
    }
}
