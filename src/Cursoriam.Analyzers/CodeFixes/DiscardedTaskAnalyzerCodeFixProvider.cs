using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cursoriam.Analyzers.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DiscardedTaskAnalyzerCodeFixProvider)), Shared]
    public class DiscardedTaskAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds {
            get { return ImmutableArray.Create(DiscardedTaskAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            // Get the assignment span
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type assignment expression identified by the diagnostic.
            var assignment = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<AssignmentExpressionSyntax>().First();

            // Find the method modifiers
            PredefinedTypeSyntax typeSyntax = default;
            if (diagnostic.AdditionalLocations.Any())
            {
                var modifiersSpan = diagnostic.AdditionalLocations.First().SourceSpan;
                if (!modifiersSpan.IsEmpty)
                {
                    // Vervang void door Task, voeg async ervoor
                    var q = root.FindToken(modifiersSpan.Start).Parent.AncestorsAndSelf();
                    typeSyntax = q.OfType<PredefinedTypeSyntax>().First(); // Of TypeSyntax?
                }
            }

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixTitle,
                    createChangedDocument: c => AddAwaitAsync(context.Document, assignment, typeSyntax, c),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
                diagnostic);
        }

        private async Task<Document> AddAwaitAsync(Document document, AssignmentExpressionSyntax assignment, PredefinedTypeSyntax typeSyntax, CancellationToken cancellationToken)
        {
            var expressionSyntax = assignment.Right;
            var awaitExpressionSyntax = SyntaxFactory.AwaitExpression(expressionSyntax);

            var nodesToReplace = new List<SyntaxNode>();
            var lookUpNodes = new Dictionary<SyntaxNode, SyntaxNode>();

            nodesToReplace.Add(assignment);
            lookUpNodes.Add(assignment, awaitExpressionSyntax);

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);

            if (typeSyntax?.Parent is MethodDeclarationSyntax methodSyntax)
            {
                var taskSyntax = SyntaxFactory.IdentifierName("Task");
                var newModifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.AsyncKeyword)).AddRange(methodSyntax.Modifiers);

                var modifiedMethodSyntax = methodSyntax.ReplaceNode(assignment, awaitExpressionSyntax).WithModifiers(newModifiers).WithReturnType(taskSyntax);

                return document.WithSyntaxRoot(oldRoot.ReplaceNode(methodSyntax, modifiedMethodSyntax));
            }

            var newRoot = oldRoot.ReplaceNode(assignment, awaitExpressionSyntax);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
