// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System;

namespace AuthJanitor.Integrity
{
    public class IntegrityReportExtensibilityType
    {
        public enum ExtensibilityTypes
        {
            CryptographicImplementation,
            EventSink,
            Identity,
            Provider,
            SecureStorage
        }

        public string TypeName { get; set; }
        public ExtensibilityTypes ExtensibilityType { get; set; }

        internal Type Type { get; private set; }
                
        public IntegrityReportExtensibilityType() { }
        public IntegrityReportExtensibilityType(Type type)
        {
            Type = type;
            TypeName = type.Name;
            if (typeof(IAuthJanitorProvider).IsAssignableFrom(type))
            {
                ExtensibilityType = ExtensibilityTypes.Provider;
            }
            else if (typeof(CryptographicImplementations.ICryptographicImplementation).IsAssignableFrom(type))
            {
                ExtensibilityType = ExtensibilityTypes.CryptographicImplementation;
            }
            else if (typeof(EventSinks.IEventSink).IsAssignableFrom(type))
            {
                ExtensibilityType = ExtensibilityTypes.EventSink;
            }
            else if (typeof(SecureStorage.ISecureStorage).IsAssignableFrom(type))
            {
                ExtensibilityType = ExtensibilityTypes.SecureStorage;
            }
        }
    }
}
