using Microsoft.CodeAnalysis;
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
    // Baseline test. No diagnostics expected to show up
    [TestMethod]
    async public Task TestEmtpyCode_NoWarningAsync()
    {
        var testCode = "";

        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }


    // Baseline test. No diagnostics expected to show up
    [TestMethod]
    public async Task TestMostSimpleCorrectCode_NoWarningAndFixAsync()
    {
        // Actually this code is still wrong because the method is not awaited, but there are other analyzers for this case.
        var testCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class TestSubject
{
    public Task TestMethodAsync()
    {
        return Task.CompletedTask;
    }

    // Comment1 must stay in place
    public {|#1:void|} MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        // Comment2 must not disappear
        {|#0:var r = t.TestMethodAsync()|};
        // Comment3 must not disappear
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }


    // Diagnostic and CodeFix both triggered and checked for
    [TestMethod]
    public async Task TestMostSimpleVoidCode_OneWarningAndFixAsync()
    {
        var testCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class TestSubject
{
    public Task TestMethodAsync()
    {
        return Task.CompletedTask;
    }

    // Comment1 must stay in place
    public void MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        // Comment2 must not disappear
        {|#0:_ = t.TestMethodAsync()|};
        // Comment3 must not disappear
    }
}";

        var fixedTestCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class TestSubject
{
    public Task TestMethodAsync()
    {
        return Task.CompletedTask;
    }

    // Comment1 must stay in place
    async public Task MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        // Comment2 must not disappear
        await t.TestMethodAsync();
        // Comment3 must not disappear
    }
}";

        var expectedDiagnostic = VerifyCS.Diagnostic(DiscardedTaskAnalyzer.DiagnosticId).WithLocation(0); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedTestCode);
    }


    [TestMethod]
    public async Task TestSimpleVoidGenericTaskDiscardCode_OneWarningAndFixAsync()
    {
        var testCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class TestSubject
{
    async public Task<int> TestMethodAsync()
    {
        return await Task.FromResult(0);
    }

    // Comment1 must stay in place
    public void MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        // Comment2 must not disappear
        {|#0:_ = t.TestMethodAsync()|};
        // Comment3 must not disappear
    }
}";

        var fixedTestCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class TestSubject
{
    async public Task<int> TestMethodAsync()
    {
        return await Task.FromResult(0);
    }

    // Comment1 must stay in place
    async public Task MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        // Comment2 must not disappear
        _ = await t.TestMethodAsync();
        // Comment3 must not disappear
    }
}";

        var expectedDiagnostic = VerifyCS.Diagnostic(DiscardedTaskAnalyzer.DiagnosticId).WithLocation(0); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedTestCode);
    }

    [TestMethod]
    public async Task TestMostSimpleAlreadyAsyncCode_OneWarningAndFixAsync()
    {
        var testCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class TestSubject
{
    public Task TestMethodAsync()
    {
        return Task.CompletedTask;
    }

    // Comment1 must stay in place
    async public Task MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        // Comment2 must not disappear
        {|#0:_ = t.TestMethodAsync()|};
        // Comment3 must not disappear
    }
}";

        var fixedTestCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class TestSubject
{
    public Task TestMethodAsync()
    {
        return Task.CompletedTask;
    }

    // Comment1 must stay in place
    async public Task MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        // Comment2 must not disappear
        await t.TestMethodAsync();
        // Comment3 must not disappear
    }
}";

        var expectedDiagnostic = VerifyCS.Diagnostic(DiscardedTaskAnalyzer.DiagnosticId).WithLocation(0); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedTestCode);
    }

    // For now, we don't touch methods that already returns a Task. Because when making async
    // We must aslo do something with the "return"
    [TestMethod]
    [Ignore] // I can't get this test working now
    public async Task TestMostSimpleAllreadyTaskCode_OneWarningDontFixAsyncAndKeepsTask()
    {
        var testCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class TestSubject
{
    public Task TestMethodAsync()
    {
        return Task.CompletedTask;
    }

    // Comment1 must stay in place
    public Task MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        // Comment2 must not disappear
        {|#0:_ = t.TestMethodAsync()|};
        // Comment3 must not disappear
        return Task.CompletedTask;
    }
}";

        var fixedTestCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class TestSubject
{
    public Task TestMethodAsync()
    {
        return Task.CompletedTask;
    }

    // Comment1 must stay in place
    public Task MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        // Comment2 must not disappear
        await t.TestMethodAsync();
        // Comment3 must not disappear
        return Task.CompletedTask;
    }
}";
        // var diagnosticId = "CS4032"; // error CS4032: The 'await' operator can only be used within an async method. 
        // DiagnosticDescriptor?
        var expectedDiagnostic = VerifyCS.Diagnostic(DiscardedTaskAnalyzer.DiagnosticId).WithLocation(0); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedTestCode);
    }


    [TestMethod]
    public async Task TestMostSimpleIntCode_OneWarningAndFixAsync()
    {
        var testCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class TestSubject
{
    public Task TestMethod()
    {
        return Task.CompletedTask;
    }

    public int MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        {|#0:_ = t.TestMethod()|};

        return 0;
    }
}";

        var fixedTestCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

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
}";

        var expectedDiagnostic = VerifyCS.Diagnostic(DiscardedTaskAnalyzer.DiagnosticId).WithLocation(0); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedTestCode);
    }

    [TestMethod]
    public async Task TestUserDefinedClassCode_OneWarningAndFixAsync()
    {
        var testCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class UserDefinedClass
{
}

internal class TestSubject
{
    public Task<int> TestMethod()
    {
        return Task.FromResult(0);
    }

    public UserDefinedClass MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        {|#0:_ = t.TestMethod()|};

        return new UserDefinedClass();
    }
}";

        var fixedTestCode = @"
using System.Threading.Tasks;

namespace ConsoleApplication1;

internal class UserDefinedClass
{
}

internal class TestSubject
{
    public Task<int> TestMethod()
    {
        return Task.FromResult(0);
    }

    async public Task<UserDefinedClass> MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        _ = await t.TestMethod();

        return new UserDefinedClass();
    }
}";

        var expectedDiagnostic = VerifyCS.Diagnostic(DiscardedTaskAnalyzer.DiagnosticId).WithLocation(0); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedTestCode);
    }

    [TestMethod]
    public async Task TestCodeWithAttributeAndComments_OneWarningAndFixAsync()
    {
        var testCode = @"
using System;
using System.Threading.Tasks;

namespace ConsoleApplication1;

[AttributeUsage(AttributeTargets.Method)]
public class DoNothingAttribute : Attribute
{
}

internal class TestSubject
{
    public Task TestMethodAsync()
    {
        return Task.CompletedTask;
    }

    // Comment1 should stay here
    [DoNothing]
    // Comment2 should stay here
    public void MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        {|#0:_ = t.TestMethodAsync()|};
    }
}";

        var fixedTestCode = @"
using System;
using System.Threading.Tasks;

namespace ConsoleApplication1;

[AttributeUsage(AttributeTargets.Method)]
public class DoNothingAttribute : Attribute
{
}

internal class TestSubject
{
    public Task TestMethodAsync()
    {
        return Task.CompletedTask;
    }

    // Comment1 should stay here
    [DoNothing]
    // Comment2 should stay here
    async public Task MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();
        await t.TestMethodAsync();
    }
}";

        var expectedDiagnostic = VerifyCS.Diagnostic(DiscardedTaskAnalyzer.DiagnosticId).WithLocation(0); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedTestCode);
    }
    [TestMethod]
    [Ignore] // I can't get this test working now
    public async Task TestLocalFunction_OneWarningAndFixAsync()
    {
        var testCode = @"
using System.Threading.Tasks;
namespace ConsoleApplication1;

internal class TestSubject
{
    async public Task<int> TestMethodAsync()
    {
        return await Task.FromResult(0);
    }

    // Comment1 must stay in place
    public void MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();

        // Comment2 must not disappear
        void f()
        {
            {|#0:_ = t.TestMethodAsync()|};
        }

        f();
    }

}";

        var fixedTestCode = @"
using System.Threading.Tasks;
namespace ConsoleApplication1;

internal class TestSubject
{
    async public Task<int> TestMethodAsync()
    {
        return await Task.FromResult(0);
    }

    // Comment1 must stay in place
    public void MethodWithCodeToAnalyze()
    {
        var t = new TestSubject();

        // Comment2 must not disappear
        void f()
        {
            {|#0:_ = await t.TestMethodAsync()|};
        }

        f();
    }
}";

        var expectedDiagnostic = VerifyCS.Diagnostic(DiscardedTaskAnalyzer.DiagnosticId).WithLocation(0); // Location 0 is the {|#0: |} syntax
        await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedTestCode);
    }
}

