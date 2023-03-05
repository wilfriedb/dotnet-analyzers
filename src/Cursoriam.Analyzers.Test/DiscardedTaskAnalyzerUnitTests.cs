using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using VerifyCS = Cursoriam.Analyzers.Test.CSharpCodeFixVerifier<
    Cursoriam.Analyzers.DiscardedTaskAnalyzer,
    Cursoriam.Analyzers.CodeFixes.DiscardedTaskAnalyzerCodeFixProvider>;

namespace Cursoriam.Analyzers.Test;

[TestClass]
public class DiscardedTaskAnalyzerUnitTest
{
    // No diagnostics expected to show up
    [TestMethod]
    async public Task TestEmtpyCode_NoWarningAsync()
    {
        var testCode = "";

        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // Diagnostic and CodeFix both triggered and checked for
    [TestMethod]
    public async Task TestMostSimpleVoidCode_OneWarningAndFixAsync()
    {
        var testCode = @"
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    internal class TestSubject
    {
        public Task TestMethod()
        {
            return Task.CompletedTask;
        }

        public {|#1:void|} MethodWithCodeToAnalyze()
        {
            var t = new TestSubject();
            // Comment must not disappear 
            {|#0:_ = t.TestMethod()|};
        }
    }
}";

        var fixedTestCode = @"
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    internal class TestSubject
    {
        public Task TestMethod()
        {
            return Task.CompletedTask;
        }

        async public Task MethodWithCodeToAnalyze()
        {
            var t = new TestSubject();
            // Comment must not disappear 
            await t.TestMethod();
        }
    }
}";

        var expectedDiagnostic = VerifyCS.Diagnostic(DiscardedTaskAnalyzer.DiagnosticId).WithLocation(0).WithLocation(1); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedTestCode);
    }

    [TestMethod]
    public async Task TestMostSimpleIntCode_OneWarningAndFixAsync()
    {
        var testCode = @"
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    internal class TestSubject
    {
        public Task TestMethod()
        {
            return Task.CompletedTask;
        }

        public {|#1:int|} MethodWithCodeToAnalyze()
        {
            var t = new TestSubject();
            {|#0:_ = t.TestMethod()|};

            return 0;
         }
    }
}";

        var fixedTestCode = @"
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    internal class TestSubject
    {
        public Task TestMethod()
        {
            return Task.CompletedTask;
        }

        async public Task<int> MethodWithCodeToAnalyze()
        {
            var t = new TestSubject();
            await t.TestMethod();

            return 0;
        }
    }
}";

        var expectedDiagnostic = VerifyCS.Diagnostic(DiscardedTaskAnalyzer.DiagnosticId).WithLocation(0).WithLocation(1); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedTestCode);
    }

}
