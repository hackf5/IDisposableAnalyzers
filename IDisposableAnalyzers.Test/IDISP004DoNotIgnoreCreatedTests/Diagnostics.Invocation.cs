﻿namespace IDisposableAnalyzers.Test.IDISP004DoNotIgnoreCreatedTests
{
    using Gu.Roslyn.Asserts;
    using Microsoft.CodeAnalysis.Diagnostics;
    using NUnit.Framework;

    public static partial class Diagnostics
    {
        public static class Invocation
        {
            private static readonly DiagnosticAnalyzer Analyzer = new CreationAnalyzer();
            private static readonly ExpectedDiagnostic ExpectedDiagnostic = ExpectedDiagnostic.Create(Descriptors.IDISP004DoNotIgnoreCreated);

            private const string DisposableCode = @"
namespace N
{
    using System;

    public class Disposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}";

            [Test]
            public static void FileOpenRead()
            {
                var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C
    {
        public void M()
        {
            ↓File.OpenRead(string.Empty);
        }
    }
}";
                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, code);
            }

            [Test]
            public static void FileOpenReadPassedIntoCtorOfNotDisposing()
            {
                var c1 = @"
namespace N
{
    using System;
    using System.IO;

    public class C1
    {
        private readonly Stream stream;

        public C1(Stream stream)
        {
           this.stream = stream;
        }
    }
}";
                var code = @"
namespace N
{
    using System.IO;

    public sealed class C
    {
        public C1 M()
        {
            return new C1(↓File.OpenRead(string.Empty));
        }
    }
}";
                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, c1, code);
            }

            [Test]
            public static void Generic()
            {
                var iDisposableOfT = @"
namespace N
{
    using System;
 
    public interface IDisposable<T> : IDisposable
    {
    }
}";

                var disposableOfT = @"
namespace N
{
    public sealed class Disposable<T> : IDisposable<T>
    {
        public void Dispose()
        {
        }
    }
}";

                var factoryCode = @"
namespace N
{
    public class Factory
    {
        public static IDisposable<T> Create<T>() => new Disposable<T>();
    }
}";

                var code = @"
namespace N
{
    using System.IO;

    public class C
    {
        public void M()
        {
            ↓Factory.Create<int>();
        }
    }
}";
                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, iDisposableOfT, disposableOfT, factoryCode, code);
            }

            [Test]
            public static void MethodCreatingDisposableExpressionBodyToString()
            {
                var code = @"
namespace N
{
    using System.IO;

    public static class C
    {
        public static Stream Stream() => File.OpenRead(string.Empty);

        public static long M()
        {
            return ↓Stream().Length;
        }
    }
}";
                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, code);
            }

            [TestCase("this.Stream().ReadAsync(null, 0, 0)")]
            [TestCase("this.Stream()?.ReadAsync(null, 0, 0)")]
            [TestCase("Stream().ReadAsync(null, 0, 0)")]
            [TestCase("Stream()?.ReadAsync(null, 0, 0)")]
            public static void MethodCreatingDisposableExpressionBodyAsync(string expression)
            {
                var code = @"
namespace N
{
    using System.IO;
    using System.Threading.Tasks;

    public class C
    {
        public Stream Stream() => File.OpenRead(string.Empty);

        public async Task<int> M() => await ↓Stream().ReadAsync(null, 0, 0);
    }
}".AssertReplace("Stream().ReadAsync(null, 0, 0)", expression);

                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, code);
            }

            [TestCase("Stream.Length")]
            [TestCase("Stream?.Length")]
            [TestCase("this.Stream.Length")]
            [TestCase("this.Stream?.Length")]
            public static void PropertyCreatingDisposableExpressionBody(string expression)
            {
                var code = @"
namespace N
{
    using System.IO;

    public class C
    {
        public Stream Stream => File.OpenRead(string.Empty);

        public long? M() => ↓this.Stream.Length;
    }
}".AssertReplace("this.Stream.Length", expression);

                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, code);
            }

            [TestCase("this.Stream.ReadAsync(null, 0, 0)")]
            [TestCase("this.Stream?.ReadAsync(null, 0, 0)")]
            [TestCase("Stream.ReadAsync(null, 0, 0)")]
            [TestCase("Stream?.ReadAsync(null, 0, 0)")]
            public static void PropertyCreatingDisposableExpressionBodyAsync(string expression)
            {
                var code = @"
namespace N
{
    using System.IO;
    using System.Threading.Tasks;

    public class C
    {
        public Stream Stream => File.OpenRead(string.Empty);

        public async Task<int> M() => await ↓Stream.ReadAsync(null, 0, 0);
    }
}".AssertReplace("Stream.ReadAsync(null, 0, 0)", expression);

                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, code);
            }

            [TestCase("Stream.Length")]
            [TestCase("Stream?.Length")]
            public static void StaticPropertyCreatingDisposableExpressionBody(string expression)
            {
                var code = @"
namespace N
{
    using System.IO;

    public static class C
    {
        public static Stream Stream => File.OpenRead(string.Empty);

        public static long? M() => ↓Stream.Length;
    }
}".AssertReplace("Stream.Length", expression);
                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, code);
            }

            [Test]
            public static void NoFixForArgument()
            {
                var code = @"
namespace N
{
    using System.IO;

    public class C
    {
        internal static string M1()
        {
            return M2(↓File.OpenRead(string.Empty));
        }

        private static string M2(Stream stream) => stream.ToString();
    }
}";

                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, code);
            }

            [Test]
            public static void FactoryMethodNewDisposable()
            {
                var code = @"
namespace N
{
    public sealed class C
    {
        public void M()
        {
            ↓Create();
        }

        private static Disposable Create()
        {
            return new Disposable();
        }
    }
}";
                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, DisposableCode, code);
            }

            [Test]
            public static void FactoryConstrainedGeneric()
            {
                var factoryCode = @"
namespace N
{
    using System;

    public class Factory
    {
        public static T Create<T>() where T : IDisposable, new() => new T();
    }
}";

                var code = @"
namespace N
{
    public class C
    {
        public void M()
        {
            ↓Factory.Create<Disposable>();
        }
    }
}";
                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, factoryCode, DisposableCode, code);
            }

            [Test]
            public static void WithOptionalParameter()
            {
                var code = @"
namespace N
{
    using System;
    using System.Collections.Generic;

    public class C
    {
        public C(IDisposable disposable)
        {
            ↓M(disposable);
        }

        private static IDisposable M(IDisposable disposable, List<IDisposable> list = null)
        {
            if (list == null)
            {
                list = new List<IDisposable>();
            }

            if (list.Contains(disposable))
            {
                return new Disposable();
            }

            list.Add(disposable);
            return M(disposable, list);
        }
    }
}";
                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, DisposableCode, code);
            }

            [Test]
            public static void DiscardFileOpenRead()
            {
                var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C
    {
        public C()
        {
            _ = ↓File.OpenRead(string.Empty);
        }
    }
}";
                RoslynAssert.Diagnostics(Analyzer, ExpectedDiagnostic, code);
            }
        }
    }
}