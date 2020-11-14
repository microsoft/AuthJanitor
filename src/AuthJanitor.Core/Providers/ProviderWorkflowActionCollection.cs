// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{
    public class ProviderWorkflowActionCollection
    {
        private List<ProviderWorkflowAction> _actions = new List<ProviderWorkflowAction>();
        private readonly IServiceProvider _serviceProvider;

        public ProviderWorkflowActionCollection(IServiceProvider serviceProvider) =>
            _serviceProvider = serviceProvider;

        public int CurrentExecutionOrder { get; set; }

        public DateTimeOffset StartedExecution => Actions.Min(a => a.Start);
        public DateTimeOffset FinishedExecution => Actions.Max(a => a.End);

        public bool HasBeenExecuted { get; private set; }
        public bool HasBeenExecutedSuccessfully { get; private set; }

        public string OrchestrationLog { get; private set; }

        public IEnumerable<Exception> GetExceptions() => Actions
            .Where(a => a.Exception != null)
            .Select(a => a.Exception);

        public Exception GetLastException() => GetExceptions().Last();

        private TProvider DuplicateProvider<TProvider>(TProvider provider)
            where TProvider : IAuthJanitorProvider =>
            _serviceProvider.GetRequiredService<ProviderManagerService>()
                            .GetProviderInstance(provider);

        public IReadOnlyList<ProviderWorkflowAction> Actions => _actions
            .OrderBy(a => a.ExecutionOrder)
            .ToList()
            .AsReadOnly();

        public void EmbedCredentials(AccessTokenCredential credential) =>
            _actions.ForEach(a => a.Instance.Credential = credential);

        public void Add<TProvider>(params ProviderWorkflowAction<TProvider>[] actions)
            where TProvider : IAuthJanitorProvider
        {
            foreach (var action in actions)
            {
                CurrentExecutionOrder++;
                _actions.Add(action);
            }
        }

        public void AddWithOneIncrement(params ProviderWorkflowAction[] actions)
        {
            CurrentExecutionOrder++;
            AddWithoutIncrement(actions);
        }

        public void AddWithoutIncrement<TProvider, TResult>(TProvider instance, Func<TProvider, Task<TResult>> action)
            where TProvider : IAuthJanitorProvider => AddWithoutIncrement(ProviderWorkflowAction.Create(DuplicateProvider(instance), action));

        public void AddWithoutIncrement<TProvider>(TProvider instance, Func<TProvider, Task> action)
            where TProvider : IAuthJanitorProvider => AddWithoutIncrement(ProviderWorkflowAction.Create(DuplicateProvider(instance), action));

        public void AddWithoutIncrement(params ProviderWorkflowAction[] actions) => _actions.AddRange(actions);

        public IEnumerable<ProviderWorkflowAction<TProvider>> GetActions<TProvider>()
            where TProvider : IAuthJanitorProvider =>
                _actions.Where(a => typeof(TProvider).IsAssignableFrom(a.Instance.GetType()))
                    .Cast<ProviderWorkflowAction<TProvider>>();

        public IEnumerable<ProviderWorkflowAction<TProvider, TResult>> GetActions<TProvider, TResult>()
            where TProvider : IAuthJanitorProvider =>
                _actions.Where(a => typeof(TProvider).IsAssignableFrom(a.Instance.GetType()))
                    .Cast<ProviderWorkflowAction<TProvider, TResult>>();

        public async Task Run()
        {
            OrchestrationLog += $"Started workflow orchestration execution at {DateTimeOffset.UtcNow}\n";

            HasBeenExecuted = true;
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
