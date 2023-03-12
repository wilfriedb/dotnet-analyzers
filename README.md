# Discarded Task Analyzer

There are many analyzer out there but once I was hunting for a bug and I found this nice piece of code (simplified):

```c#
public void Method1()
{
    var cc = new CustomClass();
    _ = cc.Method2Async();
}
```

Needless to say this caused subtle problems in the application.

Icon: <a href="https://www.flaticon.com/free-icons/syntax" title="syntax icons">Syntax icons created by alfanz - Flaticon</a>
