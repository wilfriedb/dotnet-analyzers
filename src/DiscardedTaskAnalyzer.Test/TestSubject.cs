using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cursoriam.Analyzers.Test
{
    internal class TestSubject
    {
        public Task TestMethod()
        {
            return Task.CompletedTask;
        }

        async public Task TestMethodWithAwait()
        {
            await Task.CompletedTask;
        }

        public void MethodWithCodeToAnalyze()
        {
            var t = new TestSubject();
            _ = t.TestMethod();
        }

    }
}
