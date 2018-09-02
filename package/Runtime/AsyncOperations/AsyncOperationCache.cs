using System;
using System.Collections.Generic;

namespace ResourceManagement.AsyncOperations
{
    public class AsyncOperationCache
    {
        public static readonly AsyncOperationCache m_instance = new AsyncOperationCache();

        public static AsyncOperationCache Instance
        {
            get { return m_instance; }
        }

        internal struct CacheKey
        {
            readonly Type opType;
            readonly Type objType;

            public CacheKey(Type opType, Type objType)
            {
                this.opType = opType;
                this.objType = objType;
            }

            public override int GetHashCode()
            {
                var hash = 23 * 37 + opType.GetHashCode();
                return hash * 37 + objType.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (!(obj is CacheKey))
                    return false;

                var test = (CacheKey)obj;

                return (test.opType == opType && test.objType == objType);
            }
        }

        readonly Dictionary<CacheKey, Stack<IAsyncOperation>> cache = new Dictionary<CacheKey, Stack<IAsyncOperation>>();

        public void Release<TObject>(IAsyncOperation op)
            where TObject : class
        {
            var key = new CacheKey(op.GetType(), typeof(TObject));
            Stack<IAsyncOperation> c;
            if (!cache.TryGetValue(key, out c))
                cache.Add(key, c = new Stack<IAsyncOperation>());
            c.Push(op);
        }

        public TAsyncOperation Acquire<TAsyncOperation, TObject>()
            where TAsyncOperation : class, IAsyncOperation, new()
        {
            Stack<IAsyncOperation> c;
            if (cache.TryGetValue(new CacheKey(typeof(TAsyncOperation), typeof(TObject)), out c) && c.Count > 0)
                return c.Pop() as TAsyncOperation;

            return new TAsyncOperation();
        }

        public void Clear()
        {
            cache.Clear();
        }
    }
}
