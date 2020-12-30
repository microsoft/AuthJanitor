// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{
    public class ProviderWorkflowActionCollection
    {
        public List<ProviderWorkflowAction> _actions { get; set; } = new List<ProviderWorkflowAction>();
        private readonly IServiceProvider _serviceProvider;

        public ProviderWorkflowActionCollection() { }
        public ProviderWorkflowActionCollection(IServiceProvider serviceProvider) =>
            _serviceProvider = serviceProvider;

        public int CurrentExecutionOrder { get; set; }

        [JsonIgnore]
        public DateTimeOffset StartedExecution => Actions.Min(a => a.Start);
        [JsonIgnore]
        public DateTimeOffset FinishedExecution => Actions.Max(a => a.End);

        public bool HasBeenExecuted { get; private set; }
        public bool HasBeenExecutedSuccessfully { get; private set; }

        public string OrchestrationLog { get; private set; }

        [JsonIgnore]
        public IReadOnlyList<ProviderWorkflowAction> Actions => _actions
            .OrderBy(a => a.ExecutionOrder)
            .ToList()
            .AsReadOnly();

        public IEnumerable<string> GetExceptions() => Actions
            .Where(a => a.Exception != null)
            .Select(a => a.Exception);

        public string GetLastException() => GetExceptions().LastOrDefault();

        private TProvider DuplicateProvider<TProvider>(TProvider provider)
            where TProvider : IAuthJanitorProvider =>
            _serviceProvider.GetRequiredService<ProviderManagerService>()
                            .GetProviderInstance(provider);

        private IAuthJanitorProvider CreateProvider(string providerType, string providerConfiguration) =>
            _serviceProvider.GetRequiredService<ProviderManagerService>()
                            .GetProviderInstance(providerType, providerConfiguration);

        public void EmbedCredentials(AccessTokenCredential credential) =>
            _actions.Where(a => a.Instance.Credential == null).ToList()
                    .ForEach(a => a.Instance.Credential = credential);

        public void Add<TProvider>(params ProviderWorkflowActionWithoutResult<TProvider>[] actions)
            where TProvider : IAuthJanitorProvider
        {
            foreach (var action in actions)
            {
                CurrentExecutionOrder++;
                action.ExecutionOrder = CurrentExecutionOrder;
                _actions.Add(action);
            }
        }

        public void AddWithOneIncrement(params ProviderWorkflowAction[] actions)
        {
            CurrentExecutionOrder++;
            actions.ToList().ForEach(a => a.ExecutionOrder = CurrentExecutionOrder);
            AddWithoutIncrement(actions);
        }

        public void AddWithoutIncrement<TProvider, TResult>(string name, TProvider instance, Func<TProvider, Task<TResult>> action)
            where TProvider : IAuthJanitorProvider => AddWithoutIncrement(ProviderWorkflowAction.Create(name, DuplicateProvider(instance), action));

        public void AddWithoutIncrement<TProvider>(string name, TProvider instance, Func<TProvider, Task> action)
            where TProvider : IAuthJanitorProvider => AddWithoutIncrement(ProviderWorkflowAction.Create(name, DuplicateProvider(instance), action));

        public void AddWithoutIncrement(params ProviderWorkflowAction[] actions)
        {
            actions.ToList().ForEach(a => a.ExecutionOrder = CurrentExecutionOrder);
            _actions.AddRange(actions);
        }

        public IEnumerable<ProviderWorkflowActionWithoutResult<TProvider>> GetActions<TProvider>()
            where TProvider : IAuthJanitorProvider =>
                _actions.Where(a => typeof(TProvider).IsAssignableFrom(a.Instance.GetType()))
                    .OfType<ProviderWorkflowActionWithoutResult<TProvider>>();

        public IEnumerable<ProviderWorkflowActionWithResult<TProvider, TResult>> GetActions<TProvider, TResult>()
            where TProvider : IAuthJanitorProvider =>
                _actions.Where(a => typeof(TProvider).IsAssignableFrom(a.Instance.GetType()))
                    .OfType<ProviderWorkflowActionWithResult<TProvider, TResult>>();

        public async Task Run()
        {
            HasBeenExecuted = true;
            OrchestrationLog += $"Started workflow orchestration execution at {DateTimeOffset.UtcNow}\n";

            var executionOrderIndexes = _actions.Select(a => a.ExecutionOrder).Distinct().ToList();
            var rollbackQueue = new List<ProviderWorkflowAction>();

            for (var i = 0; i < executionOrderIndexes.Count(); i++)
            {
                var executingThisGeneration = _actions.Where(a => a.ExecutionOrder == executionOrderIndexes[i]);
                OrchestrationLog += $"Executing generation {executionOrderIndexes[i]} with {executingThisGeneration.Count()} actions (Gen {i}/{executionOrderIndexes.Count})\n";

                rollbackQueue.AddRange(executingThisGeneration);

                try { await Task.WhenAll(executingThisGeneration.Select(a => a.Execute())); }
                catch (Exception ex)
                {
                    OrchestrationLog += $"Generation {executionOrderIndexes[i]} failed with exception!\n";
                    OrchestrationLog += ex.ToString() + "\n\n";
                    try
                    {
                        OrchestrationLog += $"Performing rollback of {rollbackQueue.Count} actions\n";
                        rollbackQueue
                          .OrderByDescending(r => r.ExecutionOrder)
                          .ToList()
                          .ForEach(async r => await r.Rollback());
                        OrchestrationLog += $"Rollback completed\n";
                    }
                    catch (Exception ex2) 
                    {
                        OrchestrationLog += $"Rollback failed with exception!\n";
                        OrchestrationLog += ex2.ToString() + "\n\n";
                    }
                    throw ex;
                }
                OrchestrationLog += $"Generation {executionOrderIndexes[i]} complete at {DateTimeOffset.UtcNow}\n";
            }

            HasBeenExecutedSuccessfully = true;
        }
    }
}
