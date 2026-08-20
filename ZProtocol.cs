using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZProtocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Id")]
[JsonDerivedType(typeof(ReserveInstanceCommand), typeDiscriminator: (int)CommandId.Reserve)]
[JsonDerivedType(typeof(DestroyInstanceCommand), typeDiscriminator: (int)CommandId.Destroy)]
[JsonDerivedType(typeof(ResetAllInstancesCommand), typeDiscriminator: (int)CommandId.ResetAll)]
public abstract record CommandPacket
{
    public int Version => ZProtocol.ProtocolVersion;
}

public record ReserveInstanceCommand(ReserveRequest Request) : CommandPacket;

public record DestroyInstanceCommand(Guid Guid) : CommandPacket;

public record ResetAllInstancesCommand(string[] GameProtocolVersions) : CommandPacket;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Id")]
[JsonDerivedType(typeof(GenericResponse), typeDiscriminator: (int)ResponseId.Generic)]
[JsonDerivedType(typeof(ReserveInstanceResponse), typeDiscriminator: (int)ResponseId.Reserve)]
public abstract record ResponsePacket
{
    public int Version => ZProtocol.ProtocolVersion;
    public Status Status { get; init; }
}

public record GenericResponse : ResponsePacket;

public record ReserveInstanceResponse(GameInstanceInfo GameInstanceInfo) : ResponsePacket;

public sealed record ReserveRequest(
    ulong GameId,
    Guid Guid,
    ZamboniTopology Topology,
    string GameProtocolVersion,
    int MaxPlayers);

public sealed record GameInstanceInfo(
    string Host,
    ushort Port
);

public enum CommandId
{
    Reserve = 1,
    Destroy = 2,
    ResetAll = 3
}

public enum ResponseId
{
    Generic = 1,
    Reserve = 2
}

public enum Status
{
    Ok = 1,
    Error = 2,
    NoCapacity = 3
}

public static class ZProtocol
{
    public const int ProtocolVersion = 2;

    public static Task SendCommandAsync(Stream stream, CommandPacket command, CancellationToken ct = default)
    {
        return WriteFrameAsync(stream, command, ct);
    }

    public static Task SendResponseAsync(Stream stream, ResponsePacket response, CancellationToken ct = default)
    {
        return WriteFrameAsync(stream, response, ct);
    }

    public static Task<CommandPacket?> ReadCommandAsync(Stream stream, CancellationToken ct = default)
    {
        return ReadFrameAsync<CommandPacket>(stream, ct);
    }

    public static Task<ResponsePacket?> ReadResponseAsync(Stream stream, CancellationToken ct = default)
    {
        return ReadFrameAsync<ResponsePacket>(stream, ct);
    }

    private static async Task WriteFrameAsync<T>(Stream stream, T packet, CancellationToken ct)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(packet);
        byte[] frame = new byte[json.Length + 1];
        json.CopyTo(frame, 0);
        frame[^1] = (byte)'\n';
        await stream.WriteAsync(frame, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<T?> ReadFrameAsync<T>(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var oneByte = new byte[1];
        while (true)
        {
            int read = await stream.ReadAsync(oneByte, ct);
            if (read == 0) throw new EndOfStreamException();
            if (oneByte[0] == (byte)'\n') break;
            ms.WriteByte(oneByte[0]);
        }

        return JsonSerializer.Deserialize<T>(ms.ToArray());
    }
}