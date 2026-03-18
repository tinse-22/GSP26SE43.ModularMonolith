using Microsoft.EntityFrameworkCore.Query;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Helpers;

/// <summary>
/// Wraps an IEnumerable as an IQueryable that supports EF Core async operations (ToListAsync, etc.).
/// Uses composition to avoid infinite recursion with Provider property.
/// </summary>
internal class TestAsyncEnumerable<T> : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    private readonly EnumerableQuery<T> _inner;
    private readonly TestAsyncQueryProvider<T> _provider;

    public TestAsyncEnumerable(IEnumerable<T> enumerable)
    {
        _inner = new EnumerableQuery<T>(enumerable);
        _provider = new TestAsyncQueryProvider<T>(((IQueryable)_inner).Provider);
    }

    internal TestAsyncEnumerable(Expression expression, IQueryProvider innerProvider)
    {
        _inner = new EnumerableQuery<T>(expression);
        _provider = new TestAsyncQueryProvider<T>(innerProvider);
    }

    public Type ElementType => typeof(T);

    public Expression Expression => ((IQueryable)_inner).Expression;

    public IQueryProvider Provider => _provider;

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_inner).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(GetEnumerator());
    }
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync() => new(_inner.MoveNext());

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return default;
    }
}

internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(Expression expression) =>
        new TestAsyncEnumerable<TEntity>(expression, _inner);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new TestAsyncEnumerable<TElement>(expression, _inner);

    public object Execute(Expression expression) => _inner.Execute(expression)!;

    public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var asyncResultType = typeof(TResult).GetGenericArguments().FirstOrDefault();
        if (asyncResultType == null)
        {
            return _inner.Execute<TResult>(expression);
        }

        var executionResult = typeof(IQueryProvider)
            .GetMethods()
            .Single(method => method.Name == nameof(IQueryProvider.Execute)
                && method.IsGenericMethod
                && method.GetParameters().Length == 1)
            .MakeGenericMethod(asyncResultType)
            .Invoke(_inner, new object[] { expression });

        return (TResult)typeof(Task)
            .GetMethods()
            .Single(method => method.Name == nameof(Task.FromResult) && method.IsGenericMethod)
            .MakeGenericMethod(asyncResultType)
            .Invoke(null, new[] { executionResult })!;
    }
}
