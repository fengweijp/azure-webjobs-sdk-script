﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;

namespace Microsoft.Azure.WebJobs.Script.Description
{
    [CLSCompliant(false)]
    public interface ICompilationServiceFactory
    {
        ImmutableArray<ScriptType> SupportedScriptTypes { get; }

        ICompilationService CreateService(ScriptType scriptType, IFunctionMetadataResolver metadataResolver);
    }
}
