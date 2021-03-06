﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Azure.WebJobs.Script.Description.DotNet.CSharp.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;

namespace Microsoft.Azure.WebJobs.Script.Description
{
    [CLSCompliant(false)]
    public sealed class CSharpCompilation : ICompilation
    {
        private readonly Compilation _compilation;

        public CSharpCompilation(Compilation compilation)
        {
            _compilation = compilation;
        }

        public ImmutableArray<Diagnostic> GetDiagnostics()
        {
            var diagnostics = _compilation.WithAnalyzers(GetAnalyzers()).GetAllDiagnosticsAsync().Result;
            return diagnostics.AddRange(_compilation.GetDiagnostics());
        }

        public FunctionSignature GetEntryPointSignature(IFunctionEntryPointResolver entryPointResolver)
        {
            if (!_compilation.SyntaxTrees.Any())
            {
                throw new InvalidOperationException("The current compilation does not have a syntax tree.");
            }

            var methods = _compilation.ScriptClass
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Select(m => new MethodReference<IMethodSymbol>(m.Name, m.DeclaredAccessibility == Accessibility.Public, m));

            IMethodSymbol entryPointReference = entryPointResolver.GetFunctionEntryPoint(methods).Value;
            bool hasLocalTypeReferences = entryPointReference.Parameters.Any(p => IsOrUsesAssemblyType(p.Type, entryPointReference.ContainingAssembly));

            return new FunctionSignature(entryPointReference.ContainingType.Name, entryPointReference.Name, entryPointReference.Parameters, hasLocalTypeReferences);
        }

        private static bool IsOrUsesAssemblyType(ITypeSymbol typeSymbol, IAssemblySymbol assemblySymbol)
        {
            if (typeSymbol.ContainingAssembly == assemblySymbol)
            {
                return true;
            }

            INamedTypeSymbol namedTypeSymbol = typeSymbol as INamedTypeSymbol;
            return namedTypeSymbol != null && namedTypeSymbol.IsGenericType
                && namedTypeSymbol.TypeArguments.Any(t => IsOrUsesAssemblyType(t, assemblySymbol));
        }

        public void Emit(Stream assemblyStream, Stream pdbStream, CancellationToken cancellationToken)
        {
            var compilationWithAnalyzers = _compilation.WithAnalyzers(GetAnalyzers());
            var diagnostics = compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
            var emitResult = compilationWithAnalyzers.Compilation.Emit(assemblyStream, pdbStream, cancellationToken: cancellationToken);

            diagnostics = diagnostics.AddRange(emitResult.Diagnostics);

            if (diagnostics.Any(di => di.Severity == DiagnosticSeverity.Error))
            {
                throw new CompilationErrorException("Script compilation failed.", diagnostics);
            }            
        }

        private static ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        {
            // Simply getting the built in analyzers for now.
            // This should eventually be enhanced to dynamically discover/load analyzers.
            return ImmutableArray.Create<DiagnosticAnalyzer>(new InvalidFileMetadataReferenceAnalyzer());
        }
    }
}
