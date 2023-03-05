using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cursoriam.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DiscardedTaskAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CUR0001";

        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
                DiagnosticId,
                Title,
                MessageFormat,
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.SimpleAssignmentExpression);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var assignmentExpression = (AssignmentExpressionSyntax)context.Node;

            var identifierName = assignmentExpression.Left;

            var discardSymbol = context.SemanticModel.GetSymbolInfo(identifierName).Symbol as IDiscardSymbol;
            if (discardSymbol?.Type?.Name != "Task")
            {
                // The assignment is not a discard, or the type is not a Task, no issue for this analyzer.
                return;
            }

            // Find also the containing method, and determine its return type.
            var ancestors = assignmentExpression.Ancestors();
            var method = ancestors.OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method == null)
            {
                // No containg method
                // TODO: what if the discard is in a lambda or local function?
                return;
            }

            var extraLocations = new List<Location>();
            var modifiers = method.Modifiers;
            // Check for an async modifier
            if (!modifiers.Any(s => s.IsKind(SyntaxKind.AsyncKeyword)))
            {
                // There's no async keyword
                if (method.ReturnType is PredefinedTypeSyntax methodReturnTypeSyntax)
                {
                    // If it is void, then we must also update it to async Task.
                    var returnTypeInfo = context.SemanticModel.GetTypeInfo(methodReturnTypeSyntax);
                    if (returnTypeInfo.Type?.SpecialType == SpecialType.System_Void
                        // other type than void? For example int?
                        )
                    {
                        var voidLocation = methodReturnTypeSyntax.GetLocation();
                        extraLocations.Add(voidLocation);
                    }
                }
            }

            // For discards of type Task, produce a diagnostic.
            var assignmentLocation = assignmentExpression.GetLocation();
            var diagnostic = Diagnostic.Create(Rule, assignmentLocation, extraLocations);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
