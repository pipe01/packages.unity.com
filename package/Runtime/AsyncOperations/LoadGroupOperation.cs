using System;
using System.Collections.Generic;

namespace ResourceManagement.AsyncOperations
{
    internal class LoadGroupOperation<TObject> : AsyncOperationBase<IList<TObject>>
        where TObject : class
    {
        protected int totalToLoad;
        int loadCount;
        bool allStarted;
        Action<IAsyncOperation<TObject>> m_internalOnComplete;
        Action<IAsyncOperation<TObject>> m_action;
        List<IAsyncOperation<TObject>> m_ops;

        protected override void SetResult(IList<TObject> result)
        {
            foreach (var op in m_ops)
                m_result.Add(op.result);
        }

        public LoadGroupOperation() 
        {
            m_internalOnComplete = LoadGroupOperation_completed;
        }

        public virtual LoadGroupOperation<TObject> Start(IList<IResourceLocation> locations, Func<IResourceLocation, IAsyncOperation<TObject>> loadFunc, Action<IAsyncOperation<TObject>> onComplete)
        {
            totalToLoad = locations.Count;
            loadCount = 0;
            allStarted = false;
            m_action = onComplete;
            if(m_result == null)
                m_result = new List<TObject>(locations.Count);
            else
                m_result.Clear();

            if(m_ops == null)
                m_ops = new List<IAsyncOperation<TObject>>(locations.Count);
            else
                m_ops.Clear();

            if (locations != null)
            {
                for(int i = 0; i < locations.Count; i++)
                {
                    var op = loadFunc(locations[i]);
                    m_ops.Add(op);
                    op.completed += m_internalOnComplete;
                }

                allStarted = true;

                if (isDone)
                    InvokeCompletionEvent();
            }
            else
            {
                allStarted = true;
                InvokeCompletionEvent();
            }

            return this;
        }

        public override bool isDone { get { return allStarted && loadCount == totalToLoad; } }

        void LoadGroupOperation_completed(IAsyncOperation<TObject> obj)
        {
            if (m_action != null)
                m_action(obj);

            loadCount++;

            if (isDone)
                InvokeCompletionEvent();
        }
    }
}
