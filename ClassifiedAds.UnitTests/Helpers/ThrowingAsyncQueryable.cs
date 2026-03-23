using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Helpers;

internal sealed class ThrowingAsyncEnumerable<T> : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    private readonly Exception _exception;

    public ThrowingAsyncEnumerable(Exception exception)
        : this(exception, Enumerable.Empty<T>().AsQueryable().Expression)
    {
    }

    private ThrowingAsyncEnumerable(Exception exception, Expression expression)
    {
        _exception = exception;
        Expression = expression;
        Provider = new ThrowingAsyncQueryProvider<T>(exception);
    }

    public Type ElementType => typeof(T);

    public Expression Expression { get; }

    public IQueryProvider Provider { get; }

    public IEnumerator<T> GetEnumerator() => throw _exception;

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new ThrowingAsyncEnumerator<T>(_exception);
    }

    private sealed class ThrowingAsyncQueryProvider<TElement> : IAsyncQueryProvider
    {
        private readonly Exception _exception;

        public ThrowingAsyncQueryProvider(Exception exception)
        {
            _exception = exception;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return new ThrowingAsyncEnumerable<TElement>(_exception, expression);
        }

        public IQueryable<TQueryElement> CreateQuery<TQueryElement>(Expression expression)
        {
            return new ThrowingAsyncEnumerable<TQueryElement>(_exception, expression);
        }

        public object Execute(Expression expression) => throw _exception;

        public TResult Execute<TResult>(Expression expression) => throw _exception;

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var asyncResultType = typeof(TResult).GetGenericArguments().FirstOrDefault();
            if (asyncResultType == null)
            {
                throw _exception;
            }

            return (TResult)typeof(Task)
                .GetMethods()
                .Single(method => method.Name == nameof(Task.FromException) && method.IsGenericMethod)
                .MakeGenericMethod(asyncResultType)
                .Invoke(null, new object[] { _exception })!;
        }
    }

    private sealed class ThrowingAsyncEnumerator<TElement> : IAsyncEnumerator<TElement>
    {
        private readonly Exception _exception;

        public ThrowingAsyncEnumerator(Exception exception)
        {
            _exception = exception;
        }

        public TElement Current => default!;

        public ValueTask DisposeAsync() => default;

        public ValueTask<bool> MoveNextAsync()
        {
            return ValueTask.FromException<bool>(_exception);
        }
    }
}
