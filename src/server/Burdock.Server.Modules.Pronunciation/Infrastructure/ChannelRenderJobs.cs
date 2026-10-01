using System.Threading.Channels;
using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Modules.Pronunciation.Domain;

namespace Burdock.Server.Modules.Pronunciation.Infrastructure;

/// <summary>In-process queue for the single render worker; the PostgreSQL job table (spec 09 §3.12) takes over with the database.</summary>
public sealed class ChannelRenderJobs : IRenderJobs
{
    private readonly Channel<ClipKey> _channel = Channel.CreateUnbounded<ClipKey>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(ClipKey key, CancellationToken cancellationToken) => _channel.Writer.WriteAsync(key, cancellationToken);

    public IAsyncEnumerable<ClipKey> DequeueAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
