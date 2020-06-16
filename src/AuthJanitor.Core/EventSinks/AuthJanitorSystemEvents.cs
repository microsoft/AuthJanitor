// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace AuthJanitor.EventSinks
{

    public enum AuthJanitorSystemEvents
    {
        /// <summary>
        /// Unknown System Event
        /// </summary>
        Unknown,

        /// <summary>
        /// Fired when a Resource is created
        /// </summary>
        ResourceCreated,

        /// <summary>
        /// Fired when a Resource is updated
        /// </summary>
        ResourceUpdated,

        /// <summary>
        /// Fired when a Resource is deleted
        /// </summary>
        ResourceDeleted,

        // -----

        /// <summary>
        /// Fired when a Policy is created
        /// </summary>
        PolicyCreated,

        /// <summary>
        /// Fired when a Policy is updated
        /// </summary>
        PolicyUpdated,

        /// <summary>
        /// Fired when a Policy is deleted
        /// </summary>
        PolicyDeleted,

        // -----

        /// <summary>
        /// Fired when a Secret is created
        /// </summary>
        SecretCreated,

        /// <summary>
        /// Fired when a Secret is updated
        /// </summary>
        SecretUpdated,

        /// <summary>
        /// Fired when a Secret is deleted
        /// </summary>
        SecretDeleted,

        // -----

        /// <summary>
        /// Fired when a RotationTask is completed automatically (not by an administrator)
        /// </summary>
        RotationTaskCompletedAutomatically,

        /// <summary>
        /// Fired when a RotationTask is completed manually (by an administrator)
        /// </summary>
        RotationTaskCompletedManually,

        /// <summary>
        /// Fired when a RotationTask is attempted and failed
        /// </summary>
        RotationTaskAttemptFailed,

        /// <summary>
        /// Fired when a RotationTask expires without being completed
        /// </summary>
        RotationTaskExpired,

        /// <summary>
        /// Fired when a RotationTask is deleted (not approved)
        /// </summary>
        RotationTaskDeleted,

        /// <summary>
        /// Fired when a new RotationTask is created, if the Task requires manual approval
        /// </summary>
        RotationTaskCreatedForApproval,

        /// <summary>
        /// Fired when a new RotationTask is created, if the Task uses a managed identity/service principal
        /// </summary>
        RotationTaskCreatedForAutomation,

        /// <summary>
        /// Fired when a RotationTask is approved; this is fired prior to either "Completed" event
        /// </summary>
        RotationTaskApproved,

        // -----

        /// <summary>
        /// Fired when the AuthJanitor Agent is started
        /// </summary>
        AgentServiceStarted,

        /// <summary>
        /// Fired when the AuthJanitor Agent is stopped
        /// </summary>
        AgentServiceStopped,

        /// <summary>
        /// Fired when the AuthJanitor Administrator tool is started
        /// </summary>
        AdminServiceStarted,

        /// <summary>
        /// Fired when the AuthJanitor Administrator tool is stopped
        /// </summary>
        AdminServiceStopped,

        // -----

        /// <summary>
        /// Fired when a ManagedSecret enters the lead time interval prior to its expiry
        /// </summary>
        SecretAboutToExpire,

        /// <summary>
        /// Fired when a ManagedSecret expires without being rotated
        /// </summary>
        SecretExpired,

        /// <summary>
        /// Fired when a ManagedSecret is successfully rotated by a fully automatic process
        /// </summary>
        SecretRotatedAutomatically,

        /// <summary>
        /// Fired when a ManagedSecret is successfully rotated by a manual process
        /// </summary>
        SecretRotatedManually,

        // -----

        /// <summary>
        /// Fired if an anomalous event occurred. This is fired if no other event fits the log message and something strange has occurred.
        /// </summary>
        AnomalousEventOccurred
    }
}
