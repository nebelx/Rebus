﻿using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Threading;

namespace Rebus.Transport
{
    public interface ITransactionContext : IDisposable
    {
        Dictionary<string, object> Items { get; }

        event Action Committed;

        event Action Aborted;

        event Action Cleanup;
   
        void Abort();
    }

    public static class AmbientTransactionContext
    {
        const string TransactionContextKey = "current-transaction-context";

        public static ITransactionContext Current
        {
            get
            {
                return CallContext.LogicalGetData(TransactionContextKey) as ITransactionContext;
            }
            set
            {
                CallContext.LogicalSetData(TransactionContextKey, value);
            }
        }
    }
}