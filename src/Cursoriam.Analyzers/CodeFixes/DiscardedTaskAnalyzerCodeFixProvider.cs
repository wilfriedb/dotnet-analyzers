using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

            var originalRoot = await document.GetSyntaxRootAsync(cancellationToken);

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            // If the return type is Task, the _ discard be removed.
            // For the return type Task<T>, the discard can stay in place.
            var typeInfo = semanticModel.GetTypeInfo(expressionSyntax);
            SyntaxNode rootWithAwaitAdded;
            if (typeInfo.Type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType && !namedTypeSymbol.IsUnboundGenericType)
            {
                //  We keep the discard
                var na = SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, assignment.Left, awaitExpressionSyntax.WithoutTrivia());
                rootWithAwaitAdded = originalRoot.ReplaceNode(assignment, na);
            }
            else
            {
                // We remove the discard
                rootWithAwaitAdded = originalRoot.ReplaceNode(assignment, awaitExpressionSyntax);
            }
            // First replace the discard with an await

            // Check for method modifiers and return type to adjust
            if (method is null)
            {
                // There is no containing method, or RegisterCodeFixesAsync concluded there's already an async modifier
                return document.WithSyntaxRoot(rootWithAwaitAdded);
            }

            // Find the updated method in the new root
            var location = method.GetLocation().SourceSpan.Start;
            var parentToken = rootWithAwaitAdded.FindToken(location).Parent;
            var updatedMethod = parentToken.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (updatedMethod is null)
            {
                // This should not happen, but a null check is always prudent
                return document.WithSyntaxRoot(rootWithAwaitAdded);
            }

            var returnType = method.ReturnType;
            // If the return type of the containing method/function is already a Task, don't add the async keyword, because the we have also change the "return"
            var typeText = returnType.GetText().ToString();
            if (typeText.StartsWith("Task"))
            {
                return document.WithSyntaxRoot(rootWithAwaitAdded);
            }

            // Add async to the updated method
            // Remove the leading trivia from the first modifier, because async will placed before that, and genererally
            // we want to put the trivia before the async
            SyntaxTokenList newModifiers;
            if (method.Modifiers.Any())
            {
                var firstModifier = method.Modifiers.First();
                var tailModifiers = method.Modifiers.Skip(1);

                var newFirstModifier = firstModifier.WithoutTrivia().WithTrailingTrivia(firstModifier.TrailingTrivia);
                newModifiers = SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.AsyncKeyword).WithLeadingTrivia(firstModifier.LeadingTrivia)
                ).Add(newFirstModifier).AddRange(tailModifiers);
            }
            else
            {
                newModifiers = SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
                );
            }

            // The replace the return type Task or Task<>, if necessary
            MethodDeclarationSyntax newMethod;
            IdentifierNameSyntax taskSyntax = SyntaxFactory.IdentifierName("Task");
            if (returnType is PredefinedTypeSyntax predefinedTypeSyntax)
            {
                if (predefinedTypeSyntax.Keyword.IsKind(SyntaxKind.VoidKeyword))
                {
                    newMethod = updatedMethod.WithModifiers(newModifiers).WithReturnType(taskSyntax); // Also add the trivia again
                }
                else
                {
                    // Make a generic Task
                    var syntaxList = SyntaxFactory.SeparatedList<TypeSyntax>(new[] { predefinedTypeSyntax });
                    var genericTaskType = SyntaxFactory.GenericName(taskSyntax.Identifier, SyntaxFactory.TypeArgumentList(syntaxList));
                    newMethod = updatedMethod.WithModifiers(newModifiers).WithReturnType(genericTaskType);
                }
            }
            else
            {
                // Make a generic Task
                var syntaxList = SyntaxFactory.SeparatedList<TypeSyntax>(new[] { returnType });
                var genericTaskType = SyntaxFactory.GenericName(taskSyntax.Identifier, SyntaxFactory.TypeArgumentList(syntaxList));
                newMethod = updatedMethod.WithModifiers(newModifiers).WithReturnType(genericTaskType);
            }

            var asyncAwaitRoot = rootWithAwaitAdded.ReplaceNode(updatedMethod, newMethod);

            return document.WithSyntaxRoot(asyncAwaitRoot);
        }
    }
}
