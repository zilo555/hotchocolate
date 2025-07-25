using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Channels;
using StrawberryShake.Extensions;

namespace StrawberryShake;

public sealed partial class OperationStore : IOperationStore
{
    private static readonly MethodInfo s_setGeneric = typeof(OperationStore)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .First(t =>
            t.IsGenericMethodDefinition
            && t.Name.Equals(nameof(Set), StringComparison.Ordinal));

    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<OperationRequest, IStoredOperation> _results = new();
    private readonly IEntityStore _entityStore;
    private readonly OperationStoreObservable _operationStoreObservable = new();
    private readonly IDisposable _entityChangeObserverSession;
    private readonly Channel<OperationUpdate> _updates = Channel.CreateUnbounded<OperationUpdate>();
    private bool _disposed;

    public OperationStore(IEntityStore entityStore)
    {
        _entityStore = entityStore ?? throw new ArgumentNullException(nameof(entityStore));
        _entityChangeObserverSession = _entityStore.Watch().Subscribe(OnEntityUpdate);
        BeginProcessOperationUpdates(_cts.Token);
    }

    public void Set<T>(
        OperationRequest operationRequest,
        IOperationResult<T> operationResult)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(operationRequest);
        ArgumentNullException.ThrowIfNull(operationResult);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var storedOperation = GetOrAddStoredOperation<T>(operationRequest);
        storedOperation.SetResult(operationResult);
        OnUpdate(storedOperation, OperationUpdateKind.Updated);
    }

    public void Set(OperationRequest operationRequest, IOperationResult operationResult)
    {
        s_setGeneric
            .MakeGenericMethod(operationResult.DataType)
            .Invoke(this, [operationRequest, operationResult]);
    }

    public void Reset(OperationRequest operationRequest)
    {
        ArgumentNullException.ThrowIfNull(operationRequest);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_results.TryGetValue(operationRequest, out var storedOperation))
        {
            storedOperation.ClearResult();
            CleanEntityStore();
            OnUpdate(storedOperation, OperationUpdateKind.Removed);
        }
    }

    public void Remove(OperationRequest operationRequest)
    {
        ArgumentNullException.ThrowIfNull(operationRequest);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_results.TryRemove(operationRequest, out var storedOperation))
        {
            storedOperation.Complete();
            CleanEntityStore();
            OnUpdate(storedOperation, OperationUpdateKind.Removed);
        }
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var results = _results.Values;
        _results.Clear();

        foreach (var result in results)
        {
            result.Complete();
        }

        CleanEntityStore();
        OnUpdate(results, OperationUpdateKind.Removed);
    }

    private void CleanEntityStore()
    {
        _entityStore.Update(session =>
        {
            session.RemoveEntityRange(
                _entityStore.CurrentSnapshot.GetEntityIds().Except(
                    _results.Values.SelectMany(t => t.EntityIds)));
        });
    }

    public bool TryGet<T>(
        OperationRequest operationRequest,
        [NotNullWhen(true)] out IOperationResult<T>? result)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(operationRequest);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_results.TryGetValue(operationRequest, out var storedOperation)
            && storedOperation is StoredOperation<T> { LastResult: not null } casted)
        {
            result = casted.LastResult!;
            return true;
        }

        result = null;
        return false;
    }

    public IEnumerable<StoredOperationVersion> GetAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _results.Values.Select(
            op => new StoredOperationVersion(
                op.Request,
                op.LastResult,
                op.Subscribers,
                op.LastModified));
    }

    public IReadOnlyList<EntityId> GetUsedEntityIds()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _results.Values.SelectMany(t => t.EntityIds).ToArray();
    }

    public IObservable<IOperationResult<T>> Watch<T>(
        OperationRequest operationRequest)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(operationRequest);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return GetOrAddStoredOperation<T>(operationRequest);
    }

    public IObservable<OperationUpdate> Watch()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _operationStoreObservable;
    }

    private void OnEntityUpdate(EntityUpdate update)
    {
        if (_disposed)
        {
            return;
        }

        var updated = new List<StoredOperationVersion>();

        foreach (var operation in _results.Values)
        {
            if (operation.Version < update.Version
                && update.UpdatedEntityIds.Overlaps(operation.EntityIds))
            {
                operation.UpdateResult(update.Version);
                updated.Add(new(
                    operation.Request,
                    operation.LastResult,
                    operation.Subscribers,
                    operation.LastModified));
            }
        }

        if (updated.Count > 0)
        {
            // The observables will run in the current edit session
            OnUpdate(updated, OperationUpdateKind.Updated);
        }
    }

    private StoredOperation<T> GetOrAddStoredOperation<T>(
        OperationRequest request)
        where T : class
    {
        if (_results.GetOrAdd(request, k => new StoredOperation<T>(k)) is StoredOperation<T> t)
        {
            return t;
        }

        // this should never occur.
        throw new InvalidOperationException();
    }

    private void OnUpdate(
        IStoredOperation operation,
        OperationUpdateKind kind)
        => OnUpdate(
            new[]
            {
                new StoredOperationVersion(
                    operation.Request,
                    operation.LastResult,
                    operation.Subscribers,
                    operation.LastModified)
            },
            kind);

    private void OnUpdate(
        IEnumerable<IStoredOperation> operations,
        OperationUpdateKind kind)
        => OnUpdate(
            operations
                .Select(t => new StoredOperationVersion(
                    t.Request,
                    t.LastResult,
                    t.Subscribers,
                    t.LastModified))
                .ToArray(),
            kind);

    private void OnUpdate(
        IReadOnlyList<StoredOperationVersion> operations,
        OperationUpdateKind kind)
        => _updates.Writer.TryWrite(new OperationUpdate(kind, operations));

    public void Dispose()
    {
        if (!_disposed)
        {
            _updates.Writer.TryComplete();
            _cts.Cancel();
            _cts.Dispose();
            _entityChangeObserverSession.Dispose();
            _disposed = true;
        }
    }
}
