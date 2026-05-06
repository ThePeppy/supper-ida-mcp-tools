using System.Buffers.Binary;
using System.Text.Json;

namespace SupperIdaMcp.Center.Protocol.Framing;

public static class LengthPrefixedJsonFraming
{
    private const int HeaderSize = 4;
    private const int MaxPayloadBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static byte[] Encode<T>(T message)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        if (payload.Length > MaxPayloadBytes)
        {
            throw new InvalidOperationException($"Protocol payload exceeds {MaxPayloadBytes} bytes.");
        }

        var buffer = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, HeaderSize), (uint)payload.Length);
        payload.CopyTo(buffer.AsSpan(HeaderSize));
        return buffer;
    }

    public static async ValueTask WriteAsync<T>(
        Stream stream,
        T message,
        CancellationToken cancellationToken = default)
    {
        var buffer = Encode(message);
        await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<T?> ReadAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var header = new byte[HeaderSize];
        var headerRead = await ReadExactlyOrEndAsync(stream, header, cancellationToken).ConfigureAwait(false);
        if (!headerRead)
        {
            return default;
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (length > MaxPayloadBytes)
        {
            throw new InvalidOperationException($"Protocol payload exceeds {MaxPayloadBytes} bytes.");
        }

        var payload = new byte[length];
        var payloadRead = await ReadExactlyOrEndAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        if (!payloadRead)
        {
            throw new EndOfStreamException("Connection closed before a full protocol payload was read.");
        }

        return JsonSerializer.Deserialize<T>(payload, JsonOptions);
    }

    private static async ValueTask<bool> ReadExactlyOrEndAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return offset == 0 ? false : throw new EndOfStreamException("Connection closed mid-frame.");
            }

            offset += read;
        }

        return true;
    }
}
