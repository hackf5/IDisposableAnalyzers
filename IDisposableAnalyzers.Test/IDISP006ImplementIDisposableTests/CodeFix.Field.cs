namespace IDisposableAnalyzers.Test.IDISP006ImplementIDisposableTests
{
    using Gu.Roslyn.Asserts;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.Diagnostics;
    using NUnit.Framework;

    public partial class CodeFix
    {
        public class Field
        {
            private static readonly DiagnosticAnalyzer Analyzer = new FieldAndPropertyDeclarationAnalyzer();
            private static readonly CodeFixProvider Fix = new ImplementIDisposableCodeFixProvider();
            private static readonly ExpectedDiagnostic ExpectedDiagnostic = ExpectedDiagnostic.Create("IDISP006");

            [Test]
            public void ImplementIDisposableAndMakeSealed()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public class Foo
    {
        ↓private readonly Stream stream = File.OpenRead(string.Empty);

        public Foo()
        {
        }

        public int Value { get; }

        protected virtual void Bar()
        {
        }

        private void Meh()
        {
        }
    }
}";

                var fixedCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo : System.IDisposable
    {
        private readonly Stream stream = File.OpenRead(string.Empty);
        private bool disposed;

        public Foo()
        {
        }

        public int Value { get; }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        private void Bar()
        {
        }

        private void Meh()
        {
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new System.ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}";

                AnalyzerAssert.CodeFix(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode, "Implement IDisposable and make class sealed.");
            }

            [Test]
            public void ImplementIDisposableWithVirtualDisposeMethod()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public class Foo
    {
        ↓private readonly Stream stream = File.OpenRead(string.Empty);

        public Foo()
        {
        }

        public int Value { get; }

        public int this[int value]
        {
            get
            {
                return value;
            }
        }

        protected virtual void Bar()
        {
        }

        private void Meh()
        {
        }
    }
}";

                var fixedCode = @"
namespace RoslynSandbox
{
    using System;
    using System.IO;

    public class Foo : IDisposable
    {
        private readonly Stream stream = File.OpenRead(string.Empty);
        private bool disposed;

        public Foo()
        {
        }

        public int Value { get; }

        public int this[int value]
        {
            get
            {
                return value;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Bar()
        {
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            if (disposing)
            {
            }
        }

        protected virtual void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        private void Meh()
        {
        }
    }
}";
                AnalyzerAssert.CodeFix(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode, "Implement IDisposable with virtual dispose method.");
            }

            [Test]
            public void ImplementIDisposableSealedClassUsingsInside()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo
    {
        ↓private readonly Stream stream = File.OpenRead(string.Empty);
    }
}";

                var fixedCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo : System.IDisposable
    {
        private readonly Stream stream = File.OpenRead(string.Empty);
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new System.ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}";
                AnalyzerAssert.CodeFix(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
                AnalyzerAssert.FixAll(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
            }

            [Test]
            public void ImplementIDisposableSealedClassUsingsOutside()
            {
                var testCode = @"
using System.IO;

namespace RoslynSandbox
{
    public sealed class Foo
    {
        ↓private readonly Stream stream = File.OpenRead(string.Empty);
    }
}";

                var fixedCode = @"
using System.IO;

namespace RoslynSandbox
{
    public sealed class Foo : System.IDisposable
    {
        private readonly Stream stream = File.OpenRead(string.Empty);
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new System.ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}";
                AnalyzerAssert.CodeFix(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
                AnalyzerAssert.FixAll(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
            }

            [Test]
            public void ImplementIDisposableSealedClassUnderscore()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo
    {
        ↓private readonly Stream _stream = File.OpenRead(string.Empty);
    }
}";

                var fixedCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo : System.IDisposable
    {
        private readonly Stream _stream = File.OpenRead(string.Empty);
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new System.ObjectDisposedException(GetType().FullName);
            }
        }
    }
}";
                AnalyzerAssert.CodeFix(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
                AnalyzerAssert.FixAll(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
            }

            [Test]
            public void ImplementIDisposableSealedClassUnderscoreWithConst()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo
    {
        public const int Value = 2;

        ↓private readonly Stream _stream = File.OpenRead(string.Empty);
    }
}";

                var fixedCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo : System.IDisposable
    {
        public const int Value = 2;

        private readonly Stream _stream = File.OpenRead(string.Empty);
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new System.ObjectDisposedException(GetType().FullName);
            }
        }
    }
}";
                AnalyzerAssert.CodeFix(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
                AnalyzerAssert.FixAll(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
            }

            [Test]
            public void ImplementIDisposableAbstractClass()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public abstract class Foo
    {
        ↓private readonly Stream stream = File.OpenRead(string.Empty);
    }
}";

                var fixedCode = @"
namespace RoslynSandbox
{
    using System;
    using System.IO;

    public abstract class Foo : IDisposable
    {
        private readonly Stream stream = File.OpenRead(string.Empty);
        private bool disposed;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            if (disposing)
            {
            }
        }

        protected virtual void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}";
                AnalyzerAssert.CodeFix(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
                AnalyzerAssert.FixAll(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
            }

            [Test]
            public void ImplementIDisposableAbstractClassUnderscore()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public abstract class Foo
    {
        ↓private readonly Stream _stream = File.OpenRead(string.Empty);
    }
}";

                var fixedCode = @"
namespace RoslynSandbox
{
    using System;
    using System.IO;

    public abstract class Foo : IDisposable
    {
        private readonly Stream _stream = File.OpenRead(string.Empty);
        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (disposing)
            {
            }
        }

        protected virtual void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}";
                AnalyzerAssert.CodeFix(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
                AnalyzerAssert.FixAll(Analyzer, Fix, ExpectedDiagnostic, testCode, fixedCode);
            }

            [Test]
            public void WhenInterfaceIsMissing()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public class Foo
    {
        ↓private readonly Stream stream = File.OpenRead(string.Empty);

        public void Dispose()
        {
        }
    }
}";

                AnalyzerAssert.NoFix(Analyzer, Fix, ExpectedDiagnostic, testCode);
            }

            [Test]
            public void FactoryMethodCallingPrivateCtorWithCreatedDisposable()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System;

    public sealed class Foo
    {
        ↓private readonly IDisposable value;

        private Foo(IDisposable value)
        {
            this.value = value;
        }

        public static Foo Create() => new Foo(new Disposable());
    }
}";

                var fixedCode = @"
namespace RoslynSandbox
{
    using System;

    public sealed class Foo : IDisposable
    {
        private readonly IDisposable value;
        private bool disposed;

        private Foo(IDisposable value)
        {
            this.value = value;
        }

        public static Foo Create() => new Foo(new Disposable());

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}";
                AnalyzerAssert.CodeFix(Analyzer, Fix, ExpectedDiagnostic, new[] { DisposableCode, testCode }, fixedCode);
                AnalyzerAssert.FixAll(Analyzer, Fix, ExpectedDiagnostic, new[] { DisposableCode, testCode }, fixedCode);
            }

            [Test]
            public void Issue111PartialUserControl()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.Windows.Controls;

    public partial class CodeTabView : UserControl
    {
        ↓private readonly RoslynSandbox.Disposable disposable = new RoslynSandbox.Disposable();
    }
}";

                var fixedCode = @"
namespace RoslynSandbox
{
    using System.Windows.Controls;

    public partial sealed class CodeTabView : UserControl, System.IDisposable
    {
        private readonly RoslynSandbox.Disposable disposable = new RoslynSandbox.Disposable();
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new System.ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}";
                AnalyzerAssert.CodeFix(Analyzer, Fix, ExpectedDiagnostic, new[] { DisposableCode, testCode }, fixedCode, "Implement IDisposable and make class sealed.");
            }
        }
    }
}
