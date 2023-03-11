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

            // TODO: what if the discard is in a lambda or local function?
            var method = root.FindToken(diagnosticSpan.Start).Parent.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            {
                method = null;
            }
            // Register a code action that will invoke the fix.
            var codeAction = CodeAction.Create(
                    title: CodeFixResources.CodeFixTitle,
                    createChangedDocument: cancellationToken => AddAwaitAsync(context.Document, assignment, method, cancellationToken),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)
                );
            context.RegisterCodeFix(codeAction, diagnostic);
        }

        private async Task<Document> AddAwaitAsync(Document document, AssignmentExpressionSyntax assignment, MethodDeclarationSyntax method, CancellationToken cancellationToken)
        {
            var expressionSyntax = assignment.Right;
            var awaitExpressionSyntax = SyntaxFactory.AwaitExpression(expressionSyntax).WithTriviaFrom(assignment);

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);

            // First replace the discard with an await
            var awaitRoot = oldRoot.ReplaceNode(assignment, awaitExpressionSyntax);

            // Check for method modifiers and return type to adjust
            if (method is null)
            {
                // There is no containing method, or RegisterCodeFixesAsync concluded there's already an async modifier
                return document.WithSyntaxRoot(awaitRoot);
            }

            // Find the updated method in the new root
            var location = method.GetLocation().SourceSpan.Start;
            var parentToken = awaitRoot.FindToken(location).Parent;
            var updatedMethod = parentToken.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (updatedMethod is null)
            {
                // This should not happen, but a null check is always prudent
                return document.WithSyntaxRoot(awaitRoot);
            }

            // Add async to the updated method if necessary
            var newModifiers = SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
            ).AddRange(method.Modifiers.Select(m => m.WithoutTrivia())); // Remove the trivia, otherwise it will end up between the async modifier and the next modifier


            // The replace the return type Task or Task<>, if necessary
            MethodDeclarationSyntax newMethod = null;
            TypeSyntax taskSyntax = null;
            var returnType = method.ReturnType;
            if (returnType is PredefinedTypeSyntax predefinedTypeSyntax)
            {
                if (predefinedTypeSyntax.Keyword.IsKind(SyntaxKind.VoidKeyword)) {
                    var a = 1;
                taskSyntax = SyntaxFactory.IdentifierName("Task");
                }
                var lt = method.GetLeadingTrivia();
                newMethod = updatedMethod.WithModifiers(newModifiers).WithReturnType(taskSyntax).WithLeadingTrivia(lt); // Also add the trivia again
            }

            var asyncAwaitRoot = awaitRoot.ReplaceNode(updatedMethod, newMethod);
            //if (typeSyntax?.Parent is MethodDeclarationSyntax methodSyntax)
            // {

            // }

            // var newRoot = oldRoot.ReplaceNode(assignment, awaitExpressionSyntax);

            return document.WithSyntaxRoot(asyncAwaitRoot);
        }
    }
}
