// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{
    public abstract class ProviderWorkflowAction
    {
        public bool HasStarted => Start != null;
        public bool HasCompleted => End != null;
        public bool HasSucceeded => HasCompleted && Exception == null;

        public int ExecutionOrder { get; protected set; }

        public DateTimeOffset Start { get; protected set; }
        public DateTimeOffset End { get; protected set; }

        public Exception Exception { get; protected set; }

        public string ContextUserName => Instance.Credential.DisplayUserName;
        public string ContextUserEmail => Instance.Credential.DisplayEmail;
        public string Log => (Instance.Logger as ProviderWorkflowActionLogger).LogString;

        public object Result { get; protected set; }
        public IAuthJanitorProvider Instance { get; protected set; }

        public abstract Task Execute();
        public abstract Task Rollback();

        protected const int MAX_RETRIES = 5;

        public static ProviderWorkflowAction<TProvider> Create<TProvider>(TProvider instance, Func<TProvider, Task> action)
            where TProvider : IAuthJanitorProvider => new ProviderWorkflowAction<TProvider>(instance, action);
        public static ProviderWorkflowAction<TProvider, TResult> Create<TProvider, TResult>(TProvider instance, Func<TProvider, Task<TResult>> action)
            where TProvider : IAuthJanitorProvider => new ProviderWorkflowAction<TProvider, TResult>(instance, action);
    }

    public class ProviderWorkflowAction<TProvider, TResult> : ProviderWorkflowAction
        where TProvider : IAuthJanitorProvider
    {
        public new TResult Result { get => (TResult)base.Result; set => base.Result = value; }

        public new TProvider Instance { get => (TProvider)base.Instance; set => base.Instance = value; }

        public Func<TProvider, Task<TResult>> Action { get; private set; }

        public ProviderWorkflowAction<TProvider> RollbackAction { get; private set; }

        public ProviderWorkflowAction(TProvider instance, Func<TProvider, Task<TResult>> action)
        {
            Instance = instance;
            Action = action;
        }
        public ProviderWorkflowAction(TProvider instance, Func<TProvider, Task<TResult>> action, Func<TProvider, Task> rollbackAction) :
            this(instance, action)
        {
            RollbackAction = new ProviderWorkflowAction<TProvider>(instance, rollbackAction);
        }

        public override async Task Execute()
        {
            try
            {
                Start = DateTimeOffset.UtcNow;
                for (var i = 0; i < MAX_RETRIES; i++)
                {
                    try
                    {
                        Result = await Action(Instance);
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (i == MAX_RETRIES - 1)
                            throw ex; // rethrow if at end of retries
                    }
                }
                End = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                End = DateTimeOffset.UtcNow;
                Exception = ex;
                throw ex;
            }
        }

        public override async Task Rollback()
        {
            if (RollbackAction != null)
                await RollbackAction.Execute();
        }
    }

    public class ProviderWorkflowAction<TProvider> : ProviderWorkflowAction
        where TProvider : IAuthJanitorProvider
    {
        public new TProvider Instance { get => (TProvider)base.Instance; set => base.Instance = value; }
        public Func<TProvider, Task> Action { get; private set; }

        public ProviderWorkflowAction<TProvider> RollbackAction { get; private set; }

        public ProviderWorkflowAction(TProvider instance, Func<TProvider, Task> action)
        {
            Instance = instance;
            Action = action;
        }
        public ProviderWorkflowAction(TProvider instance, Func<TProvider, Task> action, Func<TProvider, Task> rollbackAction) :
            this(instance, action)
        {
            RollbackAction = new ProviderWorkflowAction<TProvider>(instance, rollbackAction);
        }

        public override async Task Execute()
        {
            try
            {
                Start = DateTimeOffset.UtcNow;
                for (var i = 0; i < MAX_RETRIES; i++)
                {
                    try
                    {
                        await Action(Instance);
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (i == MAX_RETRIES - 1)
                            throw ex; // rethrow if at end of retries
                    }
                }
                End = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                End = DateTimeOffset.UtcNow;
                Exception = ex;
                throw ex;
            }
        }

        public override async Task Rollback()
        {
            if (RollbackAction != null)
                await RollbackAction.Execute();
        }
    }
}