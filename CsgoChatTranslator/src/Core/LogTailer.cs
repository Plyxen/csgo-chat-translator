using System.Text;

namespace CsgoChatTranslator;

/// <summary>Follows a growing log file and returns only complete new lines.</summary>
public sealed class LogTailer(string path)
{
    private const int MaxPendingBytes = 64 * 1024;

    private readonly MemoryStream _pending = new();
    private long _position = -1;
    private bool _wasMissing;

    public string Path { get; } = path;
    public bool FileExists { get; private set; }

    public List<string> Poll()
    {
        var lines = new List<string>();
        FileStream stream;
        try
        {
            stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            FileExists = false;
            _wasMissing = true;
            return lines;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return lines;
        }

        using (stream)
        {
            FileExists = true;
            var length = stream.Length;

            // Skip history on first sight, unless the file was created after we started.
            if (_position < 0) _position = _wasMissing ? 0 : length;
            if (length < _position)
            {
                _position = 0;
                _pending.SetLength(0);
            }
            if (length == _position) return lines;

            stream.Seek(_position, SeekOrigin.Begin);
            var buffer = new byte[Math.Min(length - _position, 256 * 1024)];
            var read = stream.Read(buffer, 0, buffer.Length);
            _position += read;

            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == (byte)'\n')
                {
                    lines.Add(Encoding.UTF8.GetString(_pending.GetBuffer(), 0, (int)_pending.Length).TrimEnd('\r'));
                    _pending.SetLength(0);
                }
                else if (_pending.Length < MaxPendingBytes)
                {
                    _pending.WriteByte(buffer[i]);
                }
            }
        }
        return lines;
    }
}
