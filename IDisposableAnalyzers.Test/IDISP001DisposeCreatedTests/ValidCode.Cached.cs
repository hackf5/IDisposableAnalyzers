namespace IDisposableAnalyzers.Test.IDISP001DisposeCreatedTests
{
    using Gu.Roslyn.Asserts;
    using NUnit.Framework;

    // ReSharper disable once UnusedTypeParameter
    public partial class ValidCode<T>
    {
        [TestCase("out _")]
        [TestCase("out var stream")]
        [TestCase("out var _")]
        [TestCase("out FileStream stream")]
        [TestCase("out FileStream _")]
        public void DictionaryTryGetValue(string expression)
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System.Collections.Generic;
    using System.IO;

    public static class C
    {
        private static readonly Dictionary<int, FileStream> Map = new Dictionary<int, FileStream>();

        public static bool M(int i) => Map.TryGetValue(i, out _);
    }
}".AssertReplace("out _", expression);
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [TestCase("out _")]
        [TestCase("out var temp")]
        [TestCase("out var _")]
        [TestCase("out FileStream temp")]
        [TestCase("out FileStream _")]
        public void CachedOutParameter(string expression)
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System.Collections.Generic;
    using System.IO;

    public static class C
    {
        public static readonly Dictionary<int, FileStream> Map = new Dictionary<int, FileStream>();

        public static bool M(int i) => TryGet(i, out _);

        private static bool TryGet(int i, out FileStream stream)
        {
            if (Map.TryGetValue(i, out stream))
            {
                return true;
            }

            stream = File.OpenRead(string.Empty);
            Map.Add(i, stream);
            return true;
        }
    }
}".AssertReplace("out _", expression);
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [Test]
        public void StaticConcurrentDictionaryGetOrAdd()
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System.Collections.Concurrent;
    using System.IO;

    public static class C
    {
        private static readonly ConcurrentDictionary<string, Stream> Cache = new ConcurrentDictionary<string, Stream>();

        public static long M(string fileName)
        {
            var stream = Cache.GetOrAdd(fileName, x => File.OpenRead(x));
            return stream.Length;
        }
    }
}";
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [Test]
        public void ConcurrentDictionaryGetOrAdd()
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System.Collections.Concurrent;
    using System.IO;

    public class C
    {
        private readonly ConcurrentDictionary<int, Stream> Cache = new ConcurrentDictionary<int, Stream>();

        public long M()
        {
            var stream = Cache.GetOrAdd(1, _ => File.OpenRead(string.Empty));
            return stream.Length;
        }
    }
}";
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [Test]
        public void ConcurrentDictionaryTryGetValue()
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System.Collections.Concurrent;
    using System.IO;

    public static class C
    {
        private static readonly ConcurrentDictionary<int, Stream> Cache = new ConcurrentDictionary<int, Stream>();

        public static long M()
        {
            Stream stream;
            if (Cache.TryGetValue(1, out stream))
            {
                return stream.Length;
            }

            return 0;
        }
    }
}";
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [Test]
        public void ConcurrentDictionaryTryGetValueVarOut()
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System.Collections.Concurrent;
    using System.IO;

    public static class C
    {
        private static readonly ConcurrentDictionary<int, Stream> Cache = new ConcurrentDictionary<int, Stream>();

        public static long M()
        {
            if (Cache.TryGetValue(1, out var stream))
            {
                return stream.Length;
            }

            return 0;
        }
    }
}";
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [Test]
        public void ConditionalWeakTableTryGetValue()
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System.IO;
    using System.Runtime.CompilerServices;

    public static class C
    {
        private static readonly ConditionalWeakTable<string, Stream> Cache = new ConditionalWeakTable<string, Stream>();

        public static long M()
        {
            Stream stream;
            if (Cache.TryGetValue(""1"", out stream))
            {
                return stream.Length;
            }

            return 0;
        }
    }
}";
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [Test]
        public void ConditionalWeakTableTryGetValueVarOut()
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System.IO;
    using System.Runtime.CompilerServices;

    public static class C
    {
        private static readonly ConditionalWeakTable<string, Stream> Cache = new ConditionalWeakTable<string, Stream>();

        public static long M()
        {
            if (Cache.TryGetValue(""1"", out var stream))
            {
                return stream.Length;
            }

            return 0;
        }
    }
}";
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [Test]
        public void CustomCacheWrappingDictionary()
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System;
    using System.Collections.Generic;

    internal sealed class C : IDisposable
    {
        private readonly Cache cache = new Cache();

        private C()
        {
        }

        public void Dispose()
        {
            this.cache.Clear();
        }

        public string M(int location)
        {
            if (this.cache.TryGetValue(location, out var foo))
            {
                return foo.ToString();
            }

            return null;
        }

        private class Cache
        {
            private readonly Dictionary<int, C> map = new Dictionary<int, C>();

            public bool TryGetValue(int location, out C walker)
            {
                return this.map.TryGetValue(location, out walker);
            }

            public void Clear()
            {
                foreach (var value in this.map.Values)
                {
                    value.Dispose();
                }

                this.map.Clear();
            }
        }
    }
}";
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [Test]
        public void PooledConcurrentQueueTryDequeue()
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System;

    using System.Collections.Concurrent;
    using System.Diagnostics;

    internal class C : IDisposable
    {
        private static readonly ConcurrentQueue<C> Cache = new ConcurrentQueue<C>();
        private int refCount;

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected static C Borrow(Func<C> create)
        {
            if (!Cache.TryDequeue(out var walker))
            {
                walker = create();
            }

            walker.refCount = 1;
            return walker;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.refCount--;
                Debug.Assert(this.refCount >= 0, ""refCount>= 0"");
                if (this.refCount == 0)
                {
                    Cache.Enqueue(this);
                }
            }
        }

        [Conditional(""DEBUG"")]
        protected void ThrowIfDisposed()
        {
            if (this.refCount <= 0)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}";
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [Test]
        public void PooledConcurrentQueueTryDequeue2()
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System;

    using System.Collections.Concurrent;
    using System.Diagnostics;

    internal class C : IDisposable
    {
        private static readonly ConcurrentQueue<C> Cache = new ConcurrentQueue<C>();
        private int refCount;

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected static C BorrowAndVisit(Func<C> create)
        {
            var walker = Borrow(create);
            return walker;
        }

        protected static C Borrow(Func<C> create)
        {
            if (!Cache.TryDequeue(out var walker))
            {
                walker = create();
            }

            walker.refCount = 1;
            return walker;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.refCount--;
                Debug.Assert(this.refCount >= 0, ""refCount>= 0"");
                if (this.refCount == 0)
                {
                    Cache.Enqueue(this);
                }
            }
        }

        [Conditional(""DEBUG"")]
        protected void ThrowIfDisposed()
        {
            if (this.refCount <= 0)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}";
            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [Test]
        public void TryGetRecursive()
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System;
    using System.Collections.Generic;

    internal sealed class C : IDisposable
    {
        private readonly RecursiveCs recursiveCs = new RecursiveCs();

        private C()
        {
        }

        public void Dispose()
        {
        }

        public bool Try(int location)
        {
            return this.TryGetRecursive(location, out var walker);
        }

        private bool TryGetRecursive(int location, out C walker)
        {
            if (this.recursiveCs.TryGetValue(location, out walker))
            {
                return false;
            }

            walker = new C();
            this.recursiveCs.Add(location, walker);
            return true;
        }

        private class RecursiveCs
        {
            private readonly Dictionary<int, C> map = new Dictionary<int, C>();

            public void Add(int location, C walker)
            {
                this.map.Add(location, walker);
            }

            public bool TryGetValue(int location, out C walker)
            {
                return this.map.TryGetValue(location, out walker);
            }
        }
    }
}";
            AnalyzerAssert.Valid(Analyzer, testCode);
        }
    }
}
