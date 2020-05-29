// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;

namespace AuthJanitor.Providers.Azure.Workflows
{
    public class PairedKeyAttribute : Attribute
    {
        public string PairName { get; set; }
        public PairedKeyAttribute(string pairName) => PairName = pairName;
    }
}
