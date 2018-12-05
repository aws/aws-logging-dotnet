namespace AWS.Logger.AspNetCore
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    [DebuggerDisplay("{" + nameof(CategoryName) + " == null ? \"\" : \"[" + nameof(CategoryName) + "] \",nq}{" + nameof(ToString) + ",nq}")]
    public class AWSLogScope
    {
        private static readonly AsyncLocal<AWSLogScope> Instance = new AsyncLocal<AWSLogScope>();

        public string CategoryName { get; }
        public object State { get; }

        internal AWSLogScope(string categoryName, object state)
        {
            CategoryName = categoryName;
            State = state;
        }

        public AWSLogScope Parent { get; private set; }

        public static AWSLogScope Current
        {
            set => Instance.Value = value;
            get => Instance.Value;
        }

        public static IDisposable Push(string name, object state)
        {
            var current = Current;
            Current = new AWSLogScope(name, state) {Parent = current};
            return new DisposableScope();
        }

        public override string ToString()
        {
            return State?.ToString();
        }

        private class DisposableScope : IDisposable
        {
            public void Dispose()
            {
                Current = Current.Parent;
            }
        }
    }
}