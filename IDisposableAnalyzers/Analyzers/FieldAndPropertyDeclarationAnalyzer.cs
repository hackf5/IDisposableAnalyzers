﻿namespace IDisposableAnalyzers
{
    using System;
    using System.Collections.Immutable;
    using Gu.Roslyn.AnalyzerExtensions;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class FieldAndPropertyDeclarationAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            Descriptors.IDISP002DisposeMember,
            Descriptors.IDISP006ImplementIDisposable,
            Descriptors.IDISP008DoNotMixInjectedAndCreatedForMember);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(c => HandleField(c), SyntaxKind.FieldDeclaration);
            context.RegisterSyntaxNodeAction(c => HandleProperty(c), SyntaxKind.PropertyDeclaration);
        }

        private static void HandleField(SyntaxNodeAnalysisContext context)
        {
            if (!context.IsExcludedFromAnalysis() &&
                context.ContainingSymbol is IFieldSymbol { IsStatic: false, IsConst: false } field &&
                context.Node is FieldDeclarationSyntax declaration &&
                Disposable.IsPotentiallyAssignableFrom(field.Type, context.Compilation))
            {
                HandleFieldOrProperty(context, new FieldOrPropertyAndDeclaration(field, declaration));
            }
        }

        private static void HandleProperty(SyntaxNodeAnalysisContext context)
        {
            if (context.IsExcludedFromAnalysis())
            {
                return;
            }

            var property = (IPropertySymbol)context.ContainingSymbol;
            if (property.IsStatic ||
                property.IsIndexer)
            {
                return;
            }

            var declaration = (PropertyDeclarationSyntax)context.Node;
            if (declaration.ExpressionBody != null)
            {
                return;
            }

            if (declaration.TryGetSetter(out var setter) &&
                setter.Body != null)
            {
                // Handle the backing field
                return;
            }

            if (Disposable.IsPotentiallyAssignableFrom(property.Type, context.Compilation))
            {
                HandleFieldOrProperty(context, new FieldOrPropertyAndDeclaration(property, declaration));
            }
        }

        private static void HandleFieldOrProperty(SyntaxNodeAnalysisContext context, FieldOrPropertyAndDeclaration member)
        {
            using var assignedValues = AssignedValueWalker.Borrow(member.FieldOrProperty.Symbol, context.SemanticModel, context.CancellationToken);
            using var recursive = RecursiveValues.Borrow(assignedValues, context.SemanticModel, context.CancellationToken);
            if (Disposable.IsAnyCreation(recursive, context.SemanticModel, context.CancellationToken).IsEither(Result.Yes, Result.AssumeYes))
            {
                if (Disposable.IsAnyCachedOrInjected(recursive, context.SemanticModel, context.CancellationToken).IsEither(Result.Yes, Result.AssumeYes) ||
                    IsMutableFromOutside(member.FieldOrProperty))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.IDISP008DoNotMixInjectedAndCreatedForMember, context.Node.GetLocation()));
                }
                else if (DisposableMember.IsDisposed(member, context.SemanticModel, context.CancellationToken).IsEither(Result.No, Result.AssumeNo) &&
                         !TestFixture.IsAssignedInInitializeAndDisposedInCleanup(member, context.SemanticModel, context.CancellationToken))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.IDISP002DisposeMember, context.Node.GetLocation()));

                    if (!DisposeMethod.TryFindFirst(member.FieldOrProperty.ContainingType, context.Compilation, Search.TopLevel, out _) &&
                        !TestFixture.IsAssignedInInitialize(member, context.SemanticModel, context.CancellationToken, out _, out _))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.IDISP006ImplementIDisposable, context.Node.GetLocation()));
                    }
                }
            }
        }

        private static bool IsMutableFromOutside(FieldOrProperty fieldOrProperty)
        {
            if (fieldOrProperty.Symbol is IFieldSymbol field)
            {
                if (field.IsReadOnly)
                {
                    return false;
                }

                return IsAccessible(field.DeclaredAccessibility, field.ContainingType);
            }

            if (fieldOrProperty.Symbol is IPropertySymbol property)
            {
                return IsAccessible(property.DeclaredAccessibility, property.ContainingType) &&
                       property.SetMethod is { } set &&
                       IsAccessible(set.DeclaredAccessibility, property.ContainingType);
            }

            throw new InvalidOperationException("Should not get here.");

            static bool IsAccessible(Accessibility accessibility, INamedTypeSymbol containingType)
            {
                switch (accessibility)
                {
                    case Accessibility.Private:
                        return false;
                    case Accessibility.Protected:
                        return !containingType.IsSealed;
                    case Accessibility.Internal:
                    case Accessibility.ProtectedOrInternal:
                    case Accessibility.ProtectedAndInternal:
                    case Accessibility.Public:
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(accessibility), accessibility, "Unhandled accessibility");
                }
            }
        }
    }
}
