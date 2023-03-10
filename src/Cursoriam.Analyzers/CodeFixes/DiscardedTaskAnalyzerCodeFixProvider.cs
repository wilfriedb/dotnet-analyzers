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
            // The property Parent gets the Node of the Token
            var assignment = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<AssignmentExpressionSyntax>().First();

            // Find the method modifiers
            PredefinedTypeSyntax typeSyntax = default;
            if (diagnostic.AdditionalLocations.Any())
            {
                var location = diagnostic.AdditionalLocations.First();
                var modifiersSpan = diagnostic.AdditionalLocations.First().SourceSpan;
                if (!modifiersSpan.IsEmpty)
                {
                    // Vervang void door Task, voeg async ervoor
                    var syntaxNodes = root.FindToken(modifiersSpan.Start).Parent.AncestorsAndSelf();
                    typeSyntax = syntaxNodes.OfType<PredefinedTypeSyntax>().First(); // Or TypeSyntax?
                }
            }

            var method = root.FindToken(diagnosticSpan.Start).Parent.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            {
                method = null;
            }
            // Register a code action that will invoke the fix.
            var codeAction = CodeAction.Create(
                    title: CodeFixResources.CodeFixTitle,
                    createChangedDocument: cancellationToken => AddAwaitAsync(context.Document, assignment, typeSyntax, method, cancellationToken),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)
                );
            context.RegisterCodeFix(codeAction, diagnostic);
        }

        private async Task<Document> AddAwaitAsync(Document document, AssignmentExpressionSyntax assignment, PredefinedTypeSyntax typeSyntax, MethodDeclarationSyntax method, CancellationToken cancellationToken)
        {
            var expressionSyntax = assignment.Right;
            var awaitExpressionSyntax = SyntaxFactory.AwaitExpression(expressionSyntax).WithTriviaFrom(assignment);

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);

            // First replace the discard with an await
            var awaitRoot = oldRoot.ReplaceNode(assignment, awaitExpressionSyntax);

            // if method is null...

            var location = method.GetLocation().SourceSpan.Start;
            var p = awaitRoot.FindToken(location).Parent;
            var uMethod = p.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (uMethod is null)
            {
                return document.WithSyntaxRoot(awaitRoot);
            }

            // Then add async
            var newModifiers = SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
            ).AddRange(method.Modifiers.Select(m => m.WithoutTrivia()));

            // Then replace void with Task
            var taskSyntax = SyntaxFactory.IdentifierName("Task");
            var lt = method.GetLeadingTrivia();
            var newMethod = uMethod.WithModifiers(newModifiers).WithReturnType(taskSyntax).WithLeadingTrivia(lt);

            var asyncAwaitRoot = awaitRoot.ReplaceNode(uMethod, newMethod);

            //if (typeSyntax?.Parent is MethodDeclarationSyntax methodSyntax)
            // {

            // }

            // var newRoot = oldRoot.ReplaceNode(assignment, awaitExpressionSyntax);

            return document.WithSyntaxRoot(asyncAwaitRoot);
        }
    }
}
