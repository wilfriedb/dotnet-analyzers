using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = DiscardedTaskAnalyzer.Test.CSharpCodeFixVerifier<
    DiscardedTaskAnalyzer.DiscardedTaskAnalyzerAnalyzer,
    DiscardedTaskAnalyzer.DiscardedTaskAnalyzerCodeFixProvider>;

namespace DiscardedTaskAnalyzer.Test;

[TestClass]
public class DiscardedTaskAnalyzerUnitTest
{
    //No diagnostics expected to show up
    [TestMethod]
    async public Task TestMethod1Async()
    {
        var test = @"";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    //Diagnostic and CodeFix both triggered and checked for
    [TestMethod]
    public async Task TestMethod2Async()
    {
        var test = @"
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
            {|#0:_ = t.TestMethod()|};
        }
    }
}";

        var fixtest = @"
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
            await t.TestMethod();
        }
    }
}";

        var expected = VerifyCS.Diagnostic("DiscardedTaskAnalyzer").WithLocation(0).WithLocation(1); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }
}
