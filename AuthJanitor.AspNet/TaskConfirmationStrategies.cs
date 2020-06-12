// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;

namespace AuthJanitor.UI.Shared
{
    [Serializable]
    [Flags]
    public enum TaskConfirmationStrategies : int
    {
        None = 0, // fa fa-close
        AdminSignsOffJustInTime = 1, // fa fa-pencil
        AdminCachesSignOff = 2, // fa fa-sticky-note-o
        AutomaticRekeyingAsNeeded = 4, // fa fa-rotate-left
        AutomaticRekeyingScheduled = 8, // fa fa-clock-o
        ExternalSignal = 16 // fa fa-flag
    }

    public static class TaskConfirmationStrategiesExtensions
    {
        public static bool UsesOBOTokens(this TaskConfirmationStrategies confirmationStrategies) =>
            confirmationStrategies.HasFlag(TaskConfirmationStrategies.AdminCachesSignOff) ||
            confirmationStrategies.HasFlag(TaskConfirmationStrategies.AdminSignsOffJustInTime);
        public static bool UsesServicePrincipal(this TaskConfirmationStrategies confirmationStrategies) =>
            confirmationStrategies.HasFlag(TaskConfirmationStrategies.AutomaticRekeyingAsNeeded) ||
            confirmationStrategies.HasFlag(TaskConfirmationStrategies.AutomaticRekeyingScheduled) ||
            confirmationStrategies.HasFlag(TaskConfirmationStrategies.ExternalSignal);
    }
}
