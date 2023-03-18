# Discarded Task Analyzer

There are many analyzers out there but once I was hunting for a bug and I found this piece of code (simplified):

```c#
public void Method1()
{
    var cc = new CustomClass();
    _ = cc.Method2Async();
}
```

Needless to say this caused subtle problems in the application. Existing analyzers didn't catch this problem so I decided to create a new one for this issue.

This analyzer will find an unawaited discard and will suggest a fix. This fix will place an `await` keyword if the return type is a `Task<T>`. If the return type of the method is a `Task` (non-generic), the discard will be removed (because awaiting a `Task` will return a `void`, and a `void` can't be assigned to a variable).
Also, the fix will try to add the `async` keyword to the containg method, and change the return type to `Task` or `Task<T>` if necessary.

The result will be like this:

```c#
async public Task Method1()
{
    var cc = new CustomClass();
    await cc.Method2Async();
}
```

Known issues:

- Does only provide a partial fix for methods that already return a Task, but are not async
- Does only provide a partial fix for local functions and lambdas

Two analyzers for threading and the async/await pattern which I like and got inspiration from:

[Microsoft.VisualStudio.Threading.Analyzers](https://www.nuget.org/packages/Microsoft.VisualStudio.Threading.Analyzers/)  
[Meziantou.Analyzer](https://www.nuget.org/packages/Meziantou.Analyzer)

The icon for this packages is from flaticon.com. Here is the attribution:
[Syntax icons created by alfanz - Flaticon](https://www.flaticon.com/free-icons/syntax "syntax icons")
