// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{
    public class ProviderWorkflowAction
    {
        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public bool HasStarted => Start != null;
        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public bool HasCompleted => End != null;
        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public bool HasSucceeded => HasCompleted && Exception == null;

        public int ExecutionOrder { get; set; }

        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }

        public string Exception { get; protected set; }

        public string ContextUserName => Instance != null && Instance.Credential != null ? Instance.Credential.DisplayUserName : string.Empty;
        public string ContextUserEmail => Instance != null && Instance.Credential != null ? Instance.Credential.DisplayEmail : string.Empty;

        public string Log { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public object Result { get; protected set; }
        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public IAuthJanitorProvider Instance { get; protected set; }

        public T ResultAs<T>() => (T)Result;
        public T InstanceAs<T>() where T : IAuthJanitorProvider => (T)Instance;

        public string ProviderType { get; set; }
        public string ProviderConfiguration { get; set; }

        public virtual async Task Execute() => await Task.Yield();
        public virtual async Task Rollback() => await Task.Yield();

        public string Name { get; set; }

        protected const int MAX_RETRIES = 5;

        public ProviderWorkflowAction() => PopulateBoundProperties();
        public ProviderWorkflowAction(string name) : base() => Name = name;

        protected void PopulateBoundProperties()
        {
            if (Instance != null)
            {
                ProviderType = Instance.GetType().AssemblyQualifiedName;
                ProviderConfiguration = Instance.SerializedConfiguration;
                Log = ((ProviderWorkflowActionLogger)Instance.Logger).LogString;
            }
        }

        public static ProviderWorkflowActionWithoutResult<TProvider> Create<TProvider>(string name, TProvider instance, Func<TProvider, Task> action)
            where TProvider : IAuthJanitorProvider => new ProviderWorkflowActionWithoutResult<TProvider>(name, instance, action);
        public static ProviderWorkflowActionWithResult<TProvider, TResult> Create<TProvider, TResult>(string name, TProvider instance, Func<TProvider, Task<TResult>> action)
            where TProvider : IAuthJanitorProvider => new ProviderWorkflowActionWithResult<TProvider, TResult>(name, instance, action);
    }

    public class ProviderWorkflowActionWithResult<TProvider, TResult> : ProviderWorkflowAction
        where TProvider : IAuthJanitorProvider
    {
        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public new TResult Result { get => (TResult)base.Result; set => base.Result = value; }

        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public new TProvider Instance { get => (TProvider)base.Instance; set => base.Instance = value; }
        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public Func<TProvider, Task<TResult>> Action { get; private set; }

        //public ProviderWorkflowActionWithoutResult<TProvider> RollbackAction { get; private set; } = null;

        public ProviderWorkflowActionWithResult() : base() { }
        public ProviderWorkflowActionWithResult(string name, TProvider instance, Func<TProvider, Task<TResult>> action) : base(name)
        {
            Instance = instance;
            Action = action;
        }
        public ProviderWorkflowActionWithResult(string name, TProvider instance, Func<TProvider, Task<TResult>> action, Func<TProvider, Task> rollbackAction) :
            this(name, instance, action)
        {
            //RollbackAction = new ProviderWorkflowActionWithoutResult<TProvider>(instance, rollbackAction);
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
                PopulateBoundProperties();
            }
            catch (Exception ex)
            {
                End = DateTimeOffset.UtcNow;
                Exception = ex.ToString();
                PopulateBoundProperties();
                throw ex;
            }
        }

        public override async Task Rollback()
        {
            await Task.Yield();
            //if (RollbackAction != null)
            //    await RollbackAction.Execute();
        }
    }

    public class ProviderWorkflowActionWithoutResult<TProvider> : ProviderWorkflowAction
        where TProvider : IAuthJanitorProvider
    {
        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public new TProvider Instance { get => (TProvider)base.Instance; set => base.Instance = value; }

        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        public Func<TProvider, Task> Action { get; private set; }

        //public ProviderWorkflowActionWithoutResult<TProvider> RollbackAction { get; private set; } = null;

        public ProviderWorkflowActionWithoutResult() : base() { }
        public ProviderWorkflowActionWithoutResult(string name, TProvider instance, Func<TProvider, Task> action) : base(name)
        {
            Instance = instance;
            Action = action;
        }
        public ProviderWorkflowActionWithoutResult(string name, TProvider instance, Func<TProvider, Task> action, Func<TProvider, Task> rollbackAction) :
            this(name, instance, action)
        {
            //RollbackAction = new ProviderWorkflowActionWithoutResult<TProvider>(instance, rollbackAction);
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
                PopulateBoundProperties();
            }
            catch (Exception ex)
            {
                End = DateTimeOffset.UtcNow;
                Exception = ex.ToString();
                PopulateBoundProperties();
                throw ex;
            }
        }

        public override async Task Rollback()
        {
            await Task.Yield();
            //if (RollbackAction != null)
            //    await RollbackAction.Execute();
        }
    }
}