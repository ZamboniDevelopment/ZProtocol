using System.Text.Json;
using System.Text.Json.Serialization;

namespace RelayProtocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Id")]
[JsonDerivedType(typeof(ReserveInstanceCommand), typeDiscriminator: (int)RelayProtocol.CommandId.Create)]
[JsonDerivedType(typeof(DestroyInstanceCommand), typeDiscriminator: (int)RelayProtocol.CommandId.Destroy)]
[JsonDerivedType(typeof(AllowFromCommand), typeDiscriminator: (int)RelayProtocol.CommandId.AllowFrom)]
[JsonDerivedType(typeof(ResetAllInstancesCommand), typeDiscriminator: (int)RelayProtocol.CommandId.ResetAllInstances)]
public abstract record CommandPacket
{
    public int Version => RelayProtocol.ProtocolVersion;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Id")]
[JsonDerivedType(typeof(GenericResponse), typeDiscriminator: (int)RelayProtocol.ResponseId.Generic)]
[JsonDerivedType(typeof(ReserveInstanceResponse), typeDiscriminator: (int)RelayProtocol.ResponseId.Create)]
public abstract record ResponsePacket
{
    public int Version => RelayProtocol.ProtocolVersion;
    public RelayProtocol.RelayStatus Status { get; init; }
}

public record ReserveInstanceCommand(string GameProtocolVersion) : CommandPacket;

public record DestroyInstanceCommand(ushort Port) : CommandPacket;

public record AllowFromCommand(ushort RelayPort, string AllowedIp) : CommandPacket;

public record ResetAllInstancesCommand(string[] GameProtocolVersions) : CommandPacket;

public record GenericResponse : ResponsePacket;

public record ReserveInstanceResponse(ushort Port) : ResponsePacket;

public static class RelayProtocol
{
    public const int ProtocolVersion = 1;

    public enum CommandId
    {
        Create = 1,
        Destroy = 2,
        AllowFrom = 3,
        ResetAllInstances = 4
    }

    public enum ResponseId
    {
        Generic = 1,
        Create = 2
    }

    public enum RelayStatus
    {
        Ok = 1,
        Error = 2
    }

    public static Task SendCommandAsync(Stream stream, CommandPacket command, CancellationToken ct = default)
        => WriteFrameAsync(stream, command, ct);

    public static Task SendResponseAsync(Stream stream, ResponsePacket response, CancellationToken ct = default)
        => WriteFrameAsync(stream, response, ct);

    public static Task<CommandPacket?> ReadCommandAsync(Stream stream, CancellationToken ct = default)
        => ReadFrameAsync<CommandPacket>(stream, ct);

    public static Task<ResponsePacket?> ReadResponseAsync(Stream stream, CancellationToken ct = default)
        => ReadFrameAsync<ResponsePacket>(stream, ct);

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
        byte[] oneByte = new byte[1];
        while (true)
        {
            int read = await stream.ReadAsync(oneByte, ct);
            if (read == 0) throw new EndOfStreamException();
            if (oneByte[0] == '\n') break;
            ms.WriteByte(oneByte[0]);
        }

        return JsonSerializer.Deserialize<T>(ms.ToArray());
    }
}